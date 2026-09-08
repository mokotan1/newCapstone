from __future__ import annotations

import asyncio
import re
import socket
import subprocess
import time
from pathlib import Path
from typing import IO

import httpx

from config import Settings
from providers.base import AIProvider
from providers.cuda_provider import CudaCompatProvider
from providers.litert_provider import LiteRTProvider
from services.local_ai.engine import OwnedEngine, WarmupResult
from services.local_ai.gate1 import (
    CUDA_MODEL_FILE,
    CUDA_SERVER_FILE,
    build_cuda_serve_command,
)
from services.local_ai.paths import default_local_ai_dir, resolve_cuda_dir
from services.local_ai.runtime_config import write_game_runtime_config
from services.local_ai.types import EngineKind


class ProcessEngineHost:
    """Owns direct children; readiness requires an actual text completion."""

    def __init__(self, settings: Settings) -> None:
        self._settings = settings
        self._proc: subprocess.Popen | None = None
        self._log: IO[bytes] | None = None
        self._log_path = default_local_ai_dir() / "runtime.log"

    def is_port_open(self, port: int) -> bool:
        try:
            with socket.create_connection(("127.0.0.1", port), timeout=0.3):
                return True
        except OSError:
            return False

    def make_provider(self, kind: EngineKind, port: int) -> AIProvider:
        provider_type = CudaCompatProvider if kind == "cuda" else LiteRTProvider
        return provider_type(
            base_url=f"http://127.0.0.1:{port}", model=self._settings.local_ai_model,
            num_ctx=self._settings.local_ai_num_ctx, think=False,
            top_p=self._settings.dialogue_top_p, top_k=self._settings.dialogue_top_k,
        )

    def start(self, kind: EngineKind, port: int) -> OwnedEngine:
        if self.is_port_open(port):
            raise RuntimeError("runtime_already_running")
        if self._proc is not None:
            raise RuntimeError("owned_runtime_still_running")
        directory = default_local_ai_dir()
        directory.mkdir(parents=True, exist_ok=True)
        if kind == "cuda":
            root = resolve_cuda_dir(self._settings.local_ai_cuda_dir)
            executable = root / CUDA_SERVER_FILE
            model = root / CUDA_MODEL_FILE
            if not executable.is_file() or not model.is_file():
                raise RuntimeError("cuda_artifacts_missing")
            args = build_cuda_serve_command(
                root,
                port=port,
                model_alias=self._settings.local_ai_model,
                num_ctx=self._settings.local_ai_num_ctx,
            )
        elif kind == "litert_cpu":
            python = self._settings.local_ai_litert_python
            if not python or not Path(python).is_file():
                raise RuntimeError("local_ai_litert_python_not_configured")
            config = write_game_runtime_config(
                directory / "runtime-config.json", backend="cpu",
                model_id=self._settings.local_ai_model, max_num_tokens=self._settings.local_ai_num_ctx,
            )
            args = [python, "-m", "litert_lm_cli.main", "serve", "--host", "127.0.0.1",
                    "--port", str(port), "--config", str(config)]
        else:
            raise RuntimeError("gpu_runtime_not_validated")
        self._log = self._log_path.open("wb")
        try:
            self._proc = subprocess.Popen(args, stdin=subprocess.DEVNULL, stdout=self._log,
                                          stderr=subprocess.STDOUT,
                                          creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        except OSError:
            self._log.close()
            self._log = None
            raise
        return OwnedEngine(kind, self._proc.pid, port, self.make_provider(kind, port), True)

    def stop(self, engine: OwnedEngine) -> None:
        proc = self._proc
        if not engine.started_by_manager or proc is None or proc.pid != engine.pid:
            return
        if proc.poll() is None:
            proc.terminate()
            try:
                proc.wait(timeout=5)
            except subprocess.TimeoutExpired:
                proc.kill()
                proc.wait(timeout=5)
        self._proc = None
        if self._log is not None:
            self._log.close()
            self._log = None

    async def warmup(self, engine: OwnedEngine) -> WarmupResult:
        deadline = time.monotonic() + 90
        async with httpx.AsyncClient(timeout=2) as client:
            while True:
                if ((self._proc is not None and self._proc.poll() is not None)
                        or time.monotonic() > deadline):
                    return WarmupResult(False, "unknown", "engine_start_failed")
                try:
                    response = await client.get(f"http://127.0.0.1:{engine.port}/v1/models")
                    if response.status_code == 200:
                        break
                except httpx.HTTPError:
                    pass
                await asyncio.sleep(0.25)
        started = time.perf_counter()
        first: float | None = None
        received = False
        async for event in engine.provider.stream_chat(
            [{"role": "user", "content": "짧게 한 문장으로 인사해."}], temperature=0, max_tokens=32,
        ):
            if event.type == "error":
                return WarmupResult(False, "unknown", "inference_failed")
            if event.type == "text_delta" and event.content:
                received = True
                if first is None:
                    first = (time.perf_counter() - started) * 1000
        elapsed = (time.perf_counter() - started) * 1000
        if not received:
            return WarmupResult(False, "unknown", "empty_warmup")
        backend = "unknown"
        if engine.started_by_manager:
            backend = "cpu"
            if engine.kind == "cuda":
                log = self._log_path.read_text(encoding="utf-8", errors="replace")
                backend = "gpu" if cuda_offload_confirmed(log) else "unknown"
        return WarmupResult(True, backend, ttft_ms=first, total_ms=elapsed)


def cuda_offload_confirmed(log: str) -> bool:
    """Discovery alone is insufficient: require GPU model buffers and layers."""
    layers = re.search(r"offloaded\s+(\d+)/(\d+)\s+layers to GPU", log)
    return bool(layers and int(layers[1]) > 0 and "CUDA0" in log
                and re.search(r"CUDA0.*model buffer size", log))

"""Measure LiteRT GPU for Gate 0. Does not start a CUDA sidecar.

Refuses to kill an already-open LiteRT port. From backend_ai/:

    python -m tests.evals.run_gate0_litert_gpu
    python -m tests.evals.run_gate0_litert_gpu --dry-run
"""

from __future__ import annotations

import argparse
import asyncio
import json
import os
import socket
import subprocess
import threading
import time
from collections.abc import Sequence
from dataclasses import asdict
from pathlib import Path

from config import get_settings
from providers.litert_provider import LiteRTProvider
from services.local_ai.gate0 import (
    PINNED_LITERT_PACKAGE,
    build_litert_gpu_serve_command,
    evaluate_gate0,
    gate0_block_reason,
)
from services.local_ai.gpu_evidence import parse_runtime_log
from services.local_ai.hardware import probe_nvidia
from services.local_ai.paths import default_local_ai_dir
from services.local_ai.runtime_config import write_game_runtime_config

_DEFAULT_HOST = "127.0.0.1"
_DEFAULT_PORT = 9379
_SAMPLE_COUNT = 5
_PORT_WAIT_S = 180.0
_WARMUP_PROMPT = "안녕, 체셔. 한 문장으로 인사해."


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--samples", type=int, default=_SAMPLE_COUNT)
    parser.add_argument("--port", type=int, default=_DEFAULT_PORT)
    return parser.parse_args()


def _port_open(port: int) -> bool:
    try:
        with socket.create_connection((_DEFAULT_HOST, port), timeout=0.3):
            return True
    except OSError:
        return False


def _wait_for_port(port: int, timeout_s: float) -> bool:
    deadline = time.monotonic() + timeout_s
    while time.monotonic() < deadline:
        if _port_open(port):
            return True
        time.sleep(0.5)
    return False


def _percentile(values: Sequence[float], fraction: float) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    if fraction >= 0.95:
        index = max(0, int(len(ordered) * 0.95) - 1)
    else:
        index = len(ordered) // 2
    return ordered[index]


async def _time_completion(provider: LiteRTProvider, prompt: str) -> tuple[float | None, float]:
    started = time.perf_counter()
    first_delta_ms: float | None = None
    async for event in provider.stream_chat(
        [{"role": "user", "content": prompt}],
        tools=None,
        temperature=0.8,
        max_tokens=64,
    ):
        if event.type == "text_delta" and event.content and first_delta_ms is None:
            first_delta_ms = (time.perf_counter() - started) * 1000
    complete_ms = (time.perf_counter() - started) * 1000
    return first_delta_ms, complete_ms


def _write_result(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def _print_payload(payload: dict[str, object]) -> None:
    print(json.dumps(payload, ensure_ascii=True, indent=2))


def _store_log(path: Path, log_text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(log_text, encoding="utf-8", errors="replace")


def _stop_owned(proc: subprocess.Popen[str]) -> None:
    if proc.poll() is not None:
        return
    if os.name == "nt":
        subprocess.run(
            ["taskkill", "/PID", str(proc.pid), "/T", "/F"],
            capture_output=True,
            check=False,
        )
        return
    proc.terminate()
    try:
        proc.wait(timeout=10)
    except subprocess.TimeoutExpired:
        proc.kill()


def main() -> None:
    args = _parse_args()
    settings = get_settings()
    hardware = probe_nvidia()
    config_path = default_local_ai_dir() / "runtime-config.json"
    result_path = default_local_ai_dir() / "gate0-result.json"
    command = build_litert_gpu_serve_command(config_path, host=_DEFAULT_HOST, port=args.port)
    block = gate0_block_reason(
        nvidia_available=hardware.nvidia_available,
        port_open=_port_open(args.port),
    )
    payload: dict[str, object] = {
        "runtime": PINNED_LITERT_PACKAGE,
        "model_id": settings.local_ai_model,
        "hardware": asdict(hardware),
        "command": command,
        "block_reason": block,
        "dry_run": bool(args.dry_run),
    }
    if args.dry_run or block is not None:
        payload["verdict"] = asdict(
            evaluate_gate0(
                hardware,
                parse_runtime_log(""),
                None,
            )
        )
        _write_result(result_path, payload)
        _print_payload(payload)
        raise SystemExit(0 if args.dry_run and block is None else 2)

    write_game_runtime_config(
        config_path,
        backend="gpu",
        model_id=settings.local_ai_model,
        max_num_tokens=settings.local_ai_num_ctx,
    )
    log_chunks: list[str] = []
    proc = subprocess.Popen(
        command,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="replace",
    )

    def _pump() -> None:
        stream = proc.stdout
        if stream is None:
            return
        log_chunks.extend(stream)

    pump = threading.Thread(target=_pump, daemon=True)
    pump.start()
    try:
        if not _wait_for_port(args.port, _PORT_WAIT_S):
            log_text = "".join(log_chunks)
            log_path = default_local_ai_dir() / "gate0-runtime.log"
            _store_log(log_path, log_text)
            payload["runtime_log_path"] = str(log_path)
            payload["runtime_log_tail"] = log_text[-4000:]
            payload["verdict"] = asdict(
                evaluate_gate0(hardware, parse_runtime_log(log_text), None)
            )
            payload["block_reason"] = "serve_timeout"
            _write_result(result_path, payload)
            _print_payload(payload)
            raise SystemExit(2)

        provider = LiteRTProvider(
            base_url=f"http://{_DEFAULT_HOST}:{args.port}",
            model=settings.local_ai_model,
            num_ctx=settings.local_ai_num_ctx,
            think=settings.local_ai_think,
            top_p=settings.dialogue_top_p,
            top_k=settings.dialogue_top_k,
        )

        async def _measure() -> dict[str, object]:
            warmup_ttft, warmup_complete = await _time_completion(provider, _WARMUP_PROMPT)
            completes: list[float] = []
            ttfts: list[float] = []
            for _ in range(max(1, args.samples)):
                ttft, complete = await _time_completion(provider, _WARMUP_PROMPT)
                completes.append(complete)
                if ttft is not None:
                    ttfts.append(ttft)
            await provider.aclose()
            return {
                "warmup_ttft_ms": warmup_ttft,
                "warmup_complete_ms": warmup_complete,
                "complete_p50_ms": _percentile(completes, 0.5),
                "complete_p95_ms": _percentile(completes, 0.95),
                "ttft_p50_ms": _percentile(ttfts, 0.5),
                "ttft_p95_ms": _percentile(ttfts, 0.95),
                "sample_count": len(completes),
            }

        latency = asyncio.run(_measure())
        log_text = "".join(log_chunks)
        log_path = default_local_ai_dir() / "gate0-runtime.log"
        _store_log(log_path, log_text)
        evidence = parse_runtime_log(log_text)
        complete_p50 = latency.get("complete_p50_ms")
        complete_ms = complete_p50 if isinstance(complete_p50, (int, float)) else None
        verdict = evaluate_gate0(hardware, evidence, complete_ms)
        payload.update(
            {
                "owned_process": True,
                "evidence": asdict(evidence),
                "latency_ms": latency,
                "verdict": asdict(verdict),
                "runtime_log_path": str(log_path),
                "runtime_log_tail": log_text[-4000:],
            }
        )
        _write_result(result_path, payload)
        _print_payload(payload)
        raise SystemExit(0 if verdict.passed else 2)
    finally:
        _stop_owned(proc)


if __name__ == "__main__":
    main()

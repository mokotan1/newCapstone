from __future__ import annotations

import asyncio
import logging
from contextlib import suppress
from dataclasses import dataclass
from pathlib import Path
from uuid import uuid4

from fastapi import HTTPException

from providers.base import AIProvider
from services.local_ai.engine import HardwareInfo, OwnedEngine, WarmupResult
from services.local_ai.selection import select_engine_kind
from services.local_ai.settings_store import (
    LocalAiSettings,
    load_settings,
    save_settings,
)
from services.local_ai.types import (
    ConfiguredBackend,
    EffectiveBackend,
    EngineKind,
    GpuOffload,
    RequestedMode,
    RuntimeState,
)

DEFAULT_HEALTH_POLL_INTERVAL_S = 2.0


def _retry_unavailable(detail: str) -> HTTPException:
    return HTTPException(
        status_code=503,
        detail=detail,
        headers={"Retry-After": "2"},
    )


@dataclass(frozen=True)
class RuntimeSnapshot:
    requested_mode: RequestedMode
    configured_backend: ConfiguredBackend
    effective_backend: EffectiveBackend
    state: RuntimeState
    inference_ready: bool
    managed: bool
    gpu_offload: GpuOffload
    fallback_reason: str | None
    operation_id: str | None
    model_available: bool


class ProviderLease:
    def __init__(self, manager: LocalRuntimeManager, provider: AIProvider) -> None:
        self._manager = manager
        self.provider = provider
        self._closed = False

    async def aclose(self) -> None:
        if self._closed:
            return
        self._closed = True
        await self._manager.release_lease()


class LocalRuntimeManager:
    def __init__(
        self,
        settings_path: Path,
        host: object,
        hardware: HardwareInfo,
        *,
        gate0_passed: bool = False,
        cuda_manifest_pinned: bool = False,
        litert_port: int = 9379,
        cuda_port: int = 9380,
    ) -> None:
        self._settings_path = settings_path
        self._host = host
        self._hardware = hardware
        self._gate0_passed = gate0_passed
        self._cuda_manifest_pinned = cuda_manifest_pinned
        self._litert_port = litert_port
        self._cuda_port = cuda_port
        self._settings = load_settings(settings_path)
        self._state: RuntimeState = "stopped"
        self._configured: ConfiguredBackend = "cpu"
        self._effective: EffectiveBackend = "unknown"
        self._managed = False
        self._fallback_reason: str | None = None
        self._operation_id: str | None = None
        self._owned: OwnedEngine | None = None
        self._failure_fingerprint: str | None = None
        self._leases = 0
        self._lease_zero = asyncio.Event()
        self._lease_zero.set()
        self._transition = asyncio.Lock()
        self._apply_in_flight = False
        self._health_task: asyncio.Task[None] | None = None
        self._health_stop: asyncio.Event | None = None
        self._apply_task: asyncio.Task[None] | None = None

    def snapshot(self) -> RuntimeSnapshot:
        return RuntimeSnapshot(
            requested_mode=self._settings.mode,
            configured_backend=self._configured,
            effective_backend=self._effective,
            state=self._state,
            inference_ready=self._state in {"ready", "externally_managed"},
            managed=self._managed,
            gpu_offload=self._settings.gpu_offload,
            fallback_reason=self._fallback_reason,
            operation_id=self._operation_id,
            model_available=self._state in {"ready", "externally_managed"},
        )

    async def attach_existing_if_present(self) -> None:
        if self._owned is not None:
            return
        if not self._host.is_port_open(self._litert_port):
            return
        self._state = "externally_managed"
        self._managed = False
        self._configured = "cpu"
        self._effective = "unknown"
        self._owned = OwnedEngine(
            kind="litert_cpu",
            pid=0,
            port=self._litert_port,
            provider=self._host.make_provider("litert_cpu", self._litert_port),
            started_by_manager=False,
        )

    async def apply_settings(
        self,
        mode: RequestedMode,
        gpu_offload: GpuOffload | None = None,
    ) -> str:
        if self._state == "externally_managed":
            raise HTTPException(status_code=409, detail="externally_managed")
        if self._apply_in_flight or self._transition.locked():
            raise HTTPException(status_code=409, detail="transition_in_progress")

        offload = gpu_offload or self._settings.gpu_offload
        self._settings = LocalAiSettings(
            mode=mode,
            gpu_offload=offload,
            runtime_id=self._settings.runtime_id,
            model_id=self._settings.model_id,
        )
        save_settings(self._settings_path, self._settings)
        operation_id = uuid4().hex
        self._operation_id = operation_id
        try:
            async with self._transition:
                await self._run_transition(mode)
        finally:
            self._operation_id = None
        return operation_id

    async def queue_apply(
        self,
        mode: RequestedMode,
        gpu_offload: GpuOffload | None = None,
    ) -> str:
        if self._state == "externally_managed":
            raise HTTPException(status_code=409, detail="externally_managed")
        if self._apply_in_flight or self._transition.locked():
            raise HTTPException(status_code=409, detail="transition_in_progress")
        offload = gpu_offload or self._settings.gpu_offload
        self._settings = LocalAiSettings(
            mode=mode,
            gpu_offload=offload,
            runtime_id=self._settings.runtime_id,
            model_id=self._settings.model_id,
        )
        save_settings(self._settings_path, self._settings)
        operation_id = uuid4().hex
        self._operation_id = operation_id
        self._apply_in_flight = True
        self._state = "draining"
        self._apply_task = asyncio.create_task(self._background_apply(mode, operation_id))
        return operation_id

    async def _background_apply(self, mode: RequestedMode, operation_id: str) -> None:
        try:
            async with self._transition:
                await self._run_transition(mode)
        except Exception:
            logging.getLogger(__name__).exception("Local runtime transition failed")
            self._state = "failed"
            self._effective = "unknown"
            self._fallback_reason = "runtime_transition_failed"
        finally:
            self._apply_in_flight = False
            if self._operation_id == operation_id:
                self._operation_id = None

    async def run_benchmark(self) -> dict[str, object]:
        if self._state != "ready" or self._leases > 0 or self._owned is None:
            raise HTTPException(status_code=409, detail="not_idle")
        lease = await self.admit_and_acquire()
        try:
            result = await asyncio.wait_for(self._host.warmup(self._owned), timeout=120)
        finally:
            await lease.aclose()
        return {
            "ok": result.ok,
            "effective_backend": result.effective_backend,
            "ttft_ms": result.ttft_ms,
            "total_ms": result.total_ms,
        }

    async def admit_and_acquire(self) -> ProviderLease:
        if self._state in {"draining", "starting", "warming", "failed", "stopped"}:
            raise _retry_unavailable("runtime_unavailable")
        if self._owned is None:
            raise _retry_unavailable("runtime_unavailable")
        self._leases += 1
        self._lease_zero.clear()
        return ProviderLease(self, self._owned.provider)

    async def release_lease(self) -> None:
        self._leases = max(0, self._leases - 1)
        if self._leases == 0:
            self._lease_zero.set()

    async def recover_if_engine_died(self) -> None:
        if self._owned is None or not self._managed:
            return
        if self._state != "ready" or self._effective != "gpu":
            return
        if self._host.is_port_open(self._owned.port):
            return
        if self._transition.locked():
            return
        async with self._transition:
            self._state = "draining"
            await self._lease_zero.wait()
            await self._stop_owned()
            self._failure_fingerprint = self._hardware.fingerprint or "gpu"
            await self._start_cpu_fallback(reason="gpu_process_exited")

    async def start_health_poll(
        self,
        interval_s: float = DEFAULT_HEALTH_POLL_INTERVAL_S,
    ) -> None:
        if self._health_task is not None and not self._health_task.done():
            return
        self._health_stop = asyncio.Event()
        self._health_task = asyncio.create_task(self._health_loop(interval_s))

    async def stop_health_poll(self) -> None:
        stop = self._health_stop
        if stop is not None:
            stop.set()
        task = self._health_task
        self._health_task = None
        if task is None:
            return
        try:
            await asyncio.wait_for(task, timeout=1.0)
        except (asyncio.TimeoutError, asyncio.CancelledError):
            task.cancel()
            with suppress(asyncio.CancelledError):
                await task

    async def aclose(self) -> None:
        await self.stop_health_poll()
        if self._apply_task is not None:
            self._apply_task.cancel()
            with suppress(asyncio.CancelledError):
                await self._apply_task
        self._state = "draining"
        # ASGI lifespan shutdown runs after active requests have drained.
        await self._lease_zero.wait()
        await self._stop_owned()
        self._state = "stopped"

    async def _health_loop(self, interval_s: float) -> None:
        stop = self._health_stop
        if stop is None:
            return
        while not stop.is_set():
            await self.recover_if_engine_died()
            try:
                await asyncio.wait_for(stop.wait(), timeout=interval_s)
            except asyncio.TimeoutError:
                continue

    async def _run_transition(self, mode: RequestedMode) -> None:
        self._state = "draining"
        self._fallback_reason = None
        await self._lease_zero.wait()
        await self._stop_owned()
        kind = select_engine_kind(
            mode,
            nvidia_available=self._hardware.nvidia_available,
            vram_mib=self._hardware.vram_mib,
            failure_fingerprint=self._failure_fingerprint,
            current_fingerprint=self._hardware.fingerprint,
            gate0_passed=self._gate0_passed,
            cuda_manifest_pinned=self._cuda_manifest_pinned,
        )
        if kind in {"litert_gpu", "cuda"}:
            if await self._start_kind(kind):
                return
            await self._start_cpu_fallback(reason="gpu_initialization_failed")
            return
        if mode == "gpu":
            self._fallback_reason = "gpu_runtime_not_validated"
        await self._start_kind("litert_cpu")

    async def _start_kind(self, kind: EngineKind) -> bool:
        self._state = "starting"
        self._effective = "unknown"
        port = self._cuda_port if kind == "cuda" else self._litert_port
        try:
            engine = self._host.start(kind, port)
        except (OSError, RuntimeError):
            self._state = "failed"
            return False
        self._owned = engine
        self._managed = engine.started_by_manager
        self._configured = "gpu" if kind in {"litert_gpu", "cuda"} else "cpu"
        self._state = "warming"
        try:
            warmup: WarmupResult = await asyncio.wait_for(self._host.warmup(engine), timeout=120)
        except Exception:
            warmup = WarmupResult(False, "unknown", "warmup_failed")
        if not warmup.ok:
            await self._stop_owned()
            self._state = "failed"
            if kind in {"litert_gpu", "cuda"}:
                self._failure_fingerprint = self._hardware.fingerprint or "gpu"
            return False
        if kind in {"litert_gpu", "cuda"}:
            if warmup.effective_backend != "gpu":
                await self._stop_owned()
                self._state = "failed"
                self._failure_fingerprint = self._hardware.fingerprint or "gpu"
                return False
            self._effective = "gpu"
        else:
            self._effective = "cpu"
        self._state = "ready"
        return True

    async def _start_cpu_fallback(self, reason: str) -> None:
        self._fallback_reason = reason
        ok = await self._start_kind("litert_cpu")
        if not ok:
            self._state = "failed"
            self._effective = "unknown"

    async def _stop_owned(self) -> None:
        engine = self._owned
        self._owned = None
        if engine is not None and engine.started_by_manager:
            self._host.stop(engine)
        if engine is not None:
            closer = getattr(engine.provider, "aclose", None)
            if closer is not None:
                await closer()
        self._managed = False
        self._effective = "unknown"

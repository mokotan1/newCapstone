from __future__ import annotations

import asyncio
from pathlib import Path

import pytest
from fastapi import HTTPException

from services.local_ai.engine import HardwareInfo, WarmupResult
from services.local_ai.runtime_manager import LocalRuntimeManager
from services.local_ai.settings_store import load_settings
from tests.local_ai_fakes import FakeEngineHost


def _manager(tmp_path: Path, host: FakeEngineHost | None = None, **kwargs: object) -> LocalRuntimeManager:
    hardware = kwargs.pop("hardware", HardwareInfo())
    return LocalRuntimeManager(
        settings_path=tmp_path / "settings.json",
        host=host or FakeEngineHost(),
        hardware=hardware,  # type: ignore[arg-type]
        gate0_passed=bool(kwargs.pop("gate0_passed", False)),
        cuda_manifest_pinned=bool(kwargs.pop("cuda_manifest_pinned", False)),
        litert_port=9379,
        cuda_port=9380,
    )


@pytest.mark.asyncio
async def test_cpu_apply_does_not_start_gpu(tmp_path: Path) -> None:
    host = FakeEngineHost()
    manager = _manager(tmp_path, host, gate0_passed=True)
    await manager.apply_settings(mode="cpu")
    assert host.calls.starts == ["litert_cpu"]
    snap = manager.snapshot()
    assert snap.state == "ready"
    assert snap.requested_mode == "cpu"
    assert snap.configured_backend == "cpu"
    assert snap.effective_backend == "cpu"
    assert snap.managed is True


@pytest.mark.asyncio
async def test_gpu_without_gate0_or_cuda_uses_cpu(tmp_path: Path) -> None:
    host = FakeEngineHost()
    hw = HardwareInfo(nvidia_available=True, vram_mib=8192, fingerprint="gpu-a")
    manager = _manager(tmp_path, host, hardware=hw, gate0_passed=False)
    await manager.apply_settings(mode="gpu")
    assert host.calls.starts == ["litert_cpu"]
    assert manager.snapshot().configured_backend == "cpu"


@pytest.mark.asyncio
async def test_gpu_with_cuda_pin_starts_cuda_sidecar(tmp_path: Path) -> None:
    host = FakeEngineHost()
    host.warmup_backend = "gpu"
    hw = HardwareInfo(nvidia_available=True, vram_mib=8188, fingerprint="gpu-a")
    manager = _manager(tmp_path, host, hardware=hw, cuda_manifest_pinned=True)
    await manager.apply_settings(mode="gpu")
    assert host.calls.starts == ["cuda"]
    snap = manager.snapshot()
    assert snap.configured_backend == "gpu"
    assert snap.effective_backend == "gpu"
    assert snap.state == "ready"
    assert snap.fallback_reason is None


@pytest.mark.asyncio
async def test_gpu_warmup_failure_falls_back_to_cpu_once(tmp_path: Path) -> None:
    host = FakeEngineHost()
    hw = HardwareInfo(nvidia_available=True, vram_mib=8192, fingerprint="gpu-a")
    manager = _manager(tmp_path, host, hardware=hw, gate0_passed=True)

    async def _warmup(engine):
        if engine.kind == "litert_gpu":
            return WarmupResult(ok=False, effective_backend="unknown", error="gpu_warmup")
        return WarmupResult(ok=True, effective_backend="cpu")

    host._warmup_override = _warmup
    await manager.apply_settings(mode="gpu")
    assert host.calls.starts == ["litert_gpu", "litert_cpu"]
    snap = manager.snapshot()
    assert snap.requested_mode == "gpu"
    assert snap.effective_backend == "cpu"
    assert snap.fallback_reason == "gpu_initialization_failed"
    assert snap.state == "ready"


@pytest.mark.asyncio
async def test_external_port_refuses_device_switch(tmp_path: Path) -> None:
    host = FakeEngineHost()
    host.port_open = True
    manager = _manager(tmp_path, host)
    await manager.attach_existing_if_present()
    assert manager.snapshot().state == "externally_managed"
    try:
        await manager.apply_settings(mode="gpu")
    except HTTPException as exc:
        assert exc.status_code == 409
    else:
        raise AssertionError("expected 409")
    assert host.calls.stops == []


@pytest.mark.asyncio
async def test_draining_rejects_new_chat_lease(tmp_path: Path) -> None:
    host = FakeEngineHost()
    manager = _manager(tmp_path, host)
    await manager.apply_settings(mode="cpu")
    lease = await manager.admit_and_acquire()
    apply_task = asyncio.create_task(manager.apply_settings(mode="cpu"))
    await asyncio.sleep(0.05)
    try:
        await manager.admit_and_acquire()
    except HTTPException as exc:
        assert exc.status_code == 503
        assert exc.headers.get("Retry-After") == "2"
    else:
        raise AssertionError("expected 503")
    await lease.aclose()
    await apply_task


@pytest.mark.asyncio
async def test_lease_blocks_apply_until_released(tmp_path: Path) -> None:
    host = FakeEngineHost()
    manager = _manager(tmp_path, host)
    await manager.apply_settings(mode="cpu")
    lease = await manager.admit_and_acquire()
    apply_task = asyncio.create_task(manager.apply_settings(mode="cpu"))
    await asyncio.sleep(0.05)
    assert manager.snapshot().state == "draining"
    await lease.aclose()
    await apply_task
    assert manager.snapshot().state == "ready"


@pytest.mark.asyncio
async def test_settings_persist_requested_mode(tmp_path: Path) -> None:
    host = FakeEngineHost()
    manager = _manager(tmp_path, host)
    await manager.apply_settings(mode="auto")
    stored = load_settings(tmp_path / "settings.json")
    assert stored.mode == "auto"


@pytest.mark.asyncio
async def test_idle_gpu_death_schedules_cpu_recovery(tmp_path: Path) -> None:
    host = FakeEngineHost()
    hw = HardwareInfo(nvidia_available=True, vram_mib=8192, fingerprint="gpu-a")
    manager = _manager(tmp_path, host, hardware=hw, gate0_passed=True)
    host.warmup_backend = "gpu"
    await manager.apply_settings(mode="gpu")
    assert manager.snapshot().effective_backend == "gpu"
    host.port_open = False
    await manager.recover_if_engine_died()
    assert "litert_cpu" in host.calls.starts
    assert manager.snapshot().effective_backend == "cpu"
    assert manager.snapshot().fallback_reason == "gpu_process_exited"


@pytest.mark.asyncio
async def test_health_poll_recovers_idle_gpu_without_chat(tmp_path: Path) -> None:
    host = FakeEngineHost()
    hw = HardwareInfo(nvidia_available=True, vram_mib=8192, fingerprint="gpu-a")
    manager = _manager(tmp_path, host, hardware=hw, gate0_passed=True)
    host.warmup_backend = "gpu"
    await manager.apply_settings(mode="gpu")
    host.port_open = False
    await manager.start_health_poll(interval_s=0.05)
    try:
        for _ in range(20):
            if manager.snapshot().fallback_reason == "gpu_process_exited":
                break
            await asyncio.sleep(0.05)
        snap = manager.snapshot()
        assert snap.fallback_reason == "gpu_process_exited"
        assert snap.effective_backend == "cpu"
        assert "litert_cpu" in host.calls.starts
    finally:
        await manager.stop_health_poll()

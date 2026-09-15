from __future__ import annotations

import asyncio
from pathlib import Path

import pytest
from fastapi import HTTPException

from services.local_ai.engine import HardwareInfo
from services.local_ai.runtime_manager import LocalRuntimeManager
from tests.local_ai_fakes import FakeEngineHost


@pytest.mark.asyncio
async def test_queue_closes_admission_before_return(tmp_path: Path) -> None:
    manager = LocalRuntimeManager(tmp_path / "settings.json", FakeEngineHost(), HardwareInfo())
    await manager.apply_settings("cpu")
    await manager.queue_apply("cpu")
    with pytest.raises(HTTPException) as error:
        await manager.admit_and_acquire()
    assert error.value.status_code == 503
    await manager.aclose()


@pytest.mark.asyncio
async def test_cpu_warmup_exception_is_terminal_and_closes_process(tmp_path: Path) -> None:
    host = FakeEngineHost()

    async def broken(engine: object) -> None:
        raise RuntimeError("warmup failed")

    host._warmup_override = broken
    manager = LocalRuntimeManager(tmp_path / "settings.json", host, HardwareInfo())
    await manager.apply_settings("cpu")
    assert manager.snapshot().state == "failed"
    assert manager.snapshot().effective_backend == "unknown"
    assert len(host.calls.stops) == 1


@pytest.mark.asyncio
async def test_gpu_unavailable_explains_cpu_fallback(tmp_path: Path) -> None:
    manager = LocalRuntimeManager(tmp_path / "settings.json", FakeEngineHost(), HardwareInfo())
    await manager.apply_settings("gpu")
    assert manager.snapshot().fallback_reason == "gpu_runtime_not_validated"
    await manager.aclose()


@pytest.mark.asyncio
async def test_benchmark_holds_lease_until_probe_finishes(tmp_path: Path) -> None:
    host = FakeEngineHost()
    manager = LocalRuntimeManager(tmp_path / "settings.json", host, HardwareInfo())
    await manager.apply_settings("cpu")
    entered, release = asyncio.Event(), asyncio.Event()

    async def probe(engine: object):
        from services.local_ai.engine import WarmupResult
        entered.set()
        await release.wait()
        return WarmupResult(True, "cpu", ttft_ms=10, total_ms=20)

    host._warmup_override = probe
    task = asyncio.create_task(manager.run_benchmark())
    await entered.wait()
    await manager.queue_apply("cpu")
    await asyncio.sleep(0)
    assert host.calls.stops == []
    release.set()
    assert (await task)["total_ms"] == 20
    await manager.aclose()


def test_local_chat_service_uses_leased_provider_without_cloud_fallback() -> None:
    import main
    from tests.local_ai_fakes import FakeProvider
    provider = FakeProvider("new-device")
    service = main.service_for_provider(provider)
    assert service._primary is provider
    assert service._fallback is None

from __future__ import annotations

from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from models.responses import ChatResponse
from services.local_ai.engine import HardwareInfo
from services.local_ai.runtime_manager import LocalRuntimeManager
from tests.local_ai_fakes import FakeEngineHost


@pytest.fixture
def local_manager(tmp_path: Path) -> LocalRuntimeManager:
    return LocalRuntimeManager(
        settings_path=tmp_path / "settings.json",
        host=FakeEngineHost(),
        hardware=HardwareInfo(),
        gate0_passed=False,
        cuda_manifest_pinned=False,
    )


def _headers() -> dict[str, str]:
    return {"Authorization": "Bearer secret-token"}


@pytest.mark.asyncio
async def test_status_requires_control_token(
    monkeypatch: pytest.MonkeyPatch,
    local_manager: LocalRuntimeManager,
) -> None:
    import main as main_mod

    await local_manager.apply_settings(mode="cpu")
    monkeypatch.setattr(main_mod, "runtime_manager", local_manager)
    monkeypatch.setattr(main_mod.settings, "local_ai_control_token", "secret-token")
    monkeypatch.setattr(main_mod.settings, "rate_limit_enabled", False)
    with TestClient(main_mod.app) as client:
        denied = client.get("/local-ai/status")
        assert denied.status_code == 401
        ok = client.get("/local-ai/status", headers=_headers())
        assert ok.status_code == 200
        body = ok.json()
        assert body["state"] == "ready"
        assert body["requested_mode"] == "cpu"


@pytest.mark.asyncio
async def test_status_triggers_idle_gpu_recovery(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    import main as main_mod

    host = FakeEngineHost()
    manager = LocalRuntimeManager(
        settings_path=tmp_path / "settings.json",
        host=host,
        hardware=HardwareInfo(
            nvidia_available=True,
            vram_mib=8192,
            fingerprint="gpu-a",
        ),
        gate0_passed=True,
        cuda_manifest_pinned=False,
    )
    host.warmup_backend = "gpu"
    await manager.apply_settings(mode="gpu")
    host.port_open = False
    monkeypatch.setattr(main_mod, "runtime_manager", manager)
    monkeypatch.setattr(main_mod.settings, "local_ai_control_token", "secret-token")
    monkeypatch.setattr(main_mod.settings, "rate_limit_enabled", False)
    with TestClient(main_mod.app) as client:
        body = client.get("/local-ai/status", headers=_headers()).json()
    assert body["fallback_reason"] == "gpu_process_exited"
    assert body["effective_backend"] == "cpu"


@pytest.mark.asyncio
async def test_settings_put_accepted(
    monkeypatch: pytest.MonkeyPatch,
    local_manager: LocalRuntimeManager,
) -> None:
    import main as main_mod

    await local_manager.apply_settings(mode="cpu")
    monkeypatch.setattr(main_mod, "runtime_manager", local_manager)
    monkeypatch.setattr(main_mod.settings, "local_ai_control_token", "secret-token")
    monkeypatch.setattr(main_mod.settings, "rate_limit_enabled", False)
    with TestClient(main_mod.app) as client:
        resp = client.put(
            "/local-ai/settings",
            headers=_headers(),
            json={"mode": "cpu"},
        )
    assert resp.status_code == 202
    assert resp.json()["operation_id"]


class _StubChatService:
    async def chat(self, payload: object) -> ChatResponse:
        return ChatResponse(response="ok", function_calls=[])


@pytest.mark.asyncio
async def test_chat_503_when_runtime_unavailable(
    monkeypatch: pytest.MonkeyPatch,
    local_manager: LocalRuntimeManager,
) -> None:
    import main as main_mod

    monkeypatch.setattr(main_mod, "runtime_manager", local_manager)
    monkeypatch.setattr(main_mod, "chat_service", _StubChatService())
    monkeypatch.setattr(main_mod.settings, "chat_api_token", "")
    monkeypatch.setattr(main_mod.settings, "rate_limit_enabled", False)
    with TestClient(main_mod.app) as client:
        resp = client.post("/chat", json={"prompt": "hi", "locale": "en"})
    assert resp.status_code == 503
    assert resp.headers.get("Retry-After") == "2"

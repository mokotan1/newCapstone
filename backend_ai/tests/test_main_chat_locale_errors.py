"""Localized HTTP 500 detail strings for /chat by request.locale."""

from __future__ import annotations

from models.responses import ChatResponse


class _EmptyChatService:
    async def chat(self, payload):  # noqa: ANN001
        return ChatResponse(response="", function_calls=[])


def test_chat_api_key_missing_detail_en(monkeypatch) -> None:
    import main as main_mod
    from fastapi.testclient import TestClient

    monkeypatch.setattr(main_mod, "chat_service", None)
    monkeypatch.setattr(main_mod.settings, "chat_api_token", "")
    monkeypatch.setattr(main_mod.settings, "rate_limit_enabled", False)

    with TestClient(main_mod.app) as client:
        resp = client.post("/chat", json={"prompt": "hi", "locale": "en"})

    assert resp.status_code == 500
    detail = resp.json()["detail"]
    assert "API" in detail or "key" in detail.lower()
    assert "API 키" not in detail


def test_chat_all_engines_failed_detail_en(monkeypatch) -> None:
    import main as main_mod
    from fastapi.testclient import TestClient

    monkeypatch.setattr(main_mod, "chat_service", _EmptyChatService())
    monkeypatch.setattr(main_mod.settings, "chat_api_token", "")
    monkeypatch.setattr(main_mod.settings, "rate_limit_enabled", False)

    with TestClient(main_mod.app) as client:
        resp = client.post("/chat", json={"prompt": "hi", "locale": "en"})

    assert resp.status_code == 500
    detail = resp.json()["detail"]
    assert "fail" in detail.lower()
    assert "모든 AI" not in detail

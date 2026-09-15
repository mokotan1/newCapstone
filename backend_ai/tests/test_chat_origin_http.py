from __future__ import annotations

import pytest
from fastapi.testclient import TestClient


def _isolate(monkeypatch: pytest.MonkeyPatch, main_mod: object) -> None:
    monkeypatch.setattr(main_mod.settings, "ai_provider", "test")
    monkeypatch.setattr(main_mod.settings, "rate_limit_enabled", False)


def test_root_rejects_browser_origin(monkeypatch: pytest.MonkeyPatch) -> None:
    import main as main_mod

    _isolate(monkeypatch, main_mod)
    monkeypatch.setattr(main_mod.settings, "chat_api_token", "secret-token")
    with TestClient(main_mod.app) as client:
        denied = client.get(
            "/",
            headers={
                "Origin": "https://evil.example",
                "Authorization": "Bearer secret-token",
            },
        )
    assert denied.status_code == 403
    assert denied.json()["detail"] == "origin_not_allowed"


def test_tutor_grade_requires_chat_token(monkeypatch: pytest.MonkeyPatch) -> None:
    import main as main_mod

    _isolate(monkeypatch, main_mod)
    monkeypatch.setattr(main_mod.settings, "chat_api_token", "secret-token")
    payload = {
        "question_id": "Q001",
        "user_answer": "x",
        "correct_count_before": 0,
        "quiz_target": 5,
        "locale": "ko",
    }
    with TestClient(main_mod.app) as client:
        denied = client.post("/tutor/grade", json=payload)
        assert denied.status_code == 401
        ok = client.post(
            "/tutor/grade",
            json=payload,
            headers={"Authorization": "Bearer secret-token"},
        )
    assert ok.status_code == 200

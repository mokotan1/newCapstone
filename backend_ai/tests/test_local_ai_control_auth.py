from __future__ import annotations

from fastapi import HTTPException

from services.local_ai.control_auth import verify_local_ai_control


class DummyRequest:
    def __init__(
        self,
        headers: dict[str, str] | None = None,
        host: str = "127.0.0.1",
    ) -> None:
        self.headers = headers or {}
        self.client = type("Client", (), {"host": host})()


def test_control_auth_rejects_non_loopback() -> None:
    try:
        verify_local_ai_control(DummyRequest(host="10.0.0.8"), "secret")
    except HTTPException as exc:
        assert exc.status_code == 403
        assert exc.detail == "loopback_only"
    else:
        raise AssertionError("expected HTTPException")


def test_control_auth_rejects_browser_origin() -> None:
    try:
        verify_local_ai_control(
            DummyRequest({"origin": "https://evil.example", "authorization": "Bearer secret"}),
            "secret",
        )
    except HTTPException as exc:
        assert exc.status_code == 403
        assert exc.detail == "origin_not_allowed"
    else:
        raise AssertionError("expected HTTPException")


def test_control_auth_requires_nonempty_bearer() -> None:
    try:
        verify_local_ai_control(DummyRequest(), "")
    except HTTPException as exc:
        assert exc.status_code == 401
        assert exc.detail == "control_token_required"
    else:
        raise AssertionError("expected HTTPException")


def test_control_auth_allows_loopback_bearer() -> None:
    verify_local_ai_control(
        DummyRequest({"authorization": "Bearer secret"}),
        "secret",
    )

from __future__ import annotations

from fastapi import HTTPException

from services.chat_auth import verify_chat_api_token


class DummyRequest:
    def __init__(self, headers: dict[str, str] | None = None) -> None:
        self.headers = headers or {}


def test_verify_chat_api_token_allows_when_token_unset() -> None:
    verify_chat_api_token(DummyRequest(), "")


def test_verify_chat_api_token_allows_bearer_token() -> None:
    verify_chat_api_token(
        DummyRequest({"authorization": "Bearer secret-token"}),
        "secret-token",
    )


def test_verify_chat_api_token_allows_x_header_token() -> None:
    verify_chat_api_token(
        DummyRequest({"x-chat-api-token": "secret-token"}),
        "secret-token",
    )


def test_verify_chat_api_token_rejects_missing_token() -> None:
    try:
        verify_chat_api_token(DummyRequest(), "secret-token")
    except HTTPException as exc:
        assert exc.status_code == 401
        assert exc.detail == "chat_api_token_required"
    else:
        raise AssertionError("expected HTTPException")


def test_verify_chat_api_token_rejects_wrong_token() -> None:
    try:
        verify_chat_api_token(
            DummyRequest({"authorization": "Bearer wrong"}),
            "secret-token",
        )
    except HTTPException as exc:
        assert exc.status_code == 401
        assert exc.detail == "chat_api_token_required"
    else:
        raise AssertionError("expected HTTPException")

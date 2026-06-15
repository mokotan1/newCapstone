from __future__ import annotations

import secrets

from fastapi import HTTPException, Request


def _header_value(request: Request, name: str) -> str:
    value = request.headers.get(name, "")
    return value.strip() if isinstance(value, str) else ""


def _bearer_token(authorization: str) -> str:
    parts = authorization.split(None, 1)
    if len(parts) != 2:
        return ""
    scheme, token = parts
    if scheme.lower() != "bearer":
        return ""
    return token.strip()


def verify_chat_api_token(request: Request, expected_token: str) -> None:
    """Require a shared chat API token only when one is configured."""
    expected = (expected_token or "").strip()
    if not expected:
        return

    provided = _bearer_token(_header_value(request, "authorization"))
    if not provided:
        provided = _header_value(request, "x-chat-api-token")

    if not provided or not secrets.compare_digest(provided, expected):
        raise HTTPException(status_code=401, detail="chat_api_token_required")

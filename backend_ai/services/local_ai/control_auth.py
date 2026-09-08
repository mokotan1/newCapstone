from __future__ import annotations

import secrets

from fastapi import HTTPException, Request

_LOOPBACK = frozenset({"127.0.0.1", "::1", "localhost", "testclient"})


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


def verify_local_ai_control(request: Request, expected_token: str) -> None:
    """Loopback-only control API: bearer token required, browser Origin forbidden."""
    client = getattr(request, "client", None)
    host = (getattr(client, "host", None) or "").strip().lower()
    if host not in _LOOPBACK:
        raise HTTPException(status_code=403, detail="loopback_only")

    if "origin" in request.headers:
        raise HTTPException(status_code=403, detail="origin_not_allowed")

    expected = (expected_token or "").strip()
    provided = _bearer_token(_header_value(request, "authorization"))
    if not expected or not provided or not secrets.compare_digest(provided, expected):
        raise HTTPException(status_code=401, detail="control_token_required")

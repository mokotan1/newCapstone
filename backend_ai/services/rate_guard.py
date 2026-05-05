from __future__ import annotations

import hashlib

from fastapi import HTTPException, Request

from config import Settings
from models.requests import ChatRequest
from services.rate_limit import NullRateLimiter, RateLimiter

_limiter: RateLimiter = NullRateLimiter()
_settings: Settings | None = None


def configure_rate_guard(settings: Settings, limiter: RateLimiter) -> None:
    global _limiter, _settings
    _limiter = limiter
    _settings = settings


def _client_ip(request: Request) -> str:
    xff = (request.headers.get("x-forwarded-for") or "").strip()
    if xff:
        part = xff.split(",")[0].strip()
        if part:
            return part
    if request.client and request.client.host:
        return request.client.host
    return "unknown"


async def enforce_chat_rate_limits(request: Request, chat: ChatRequest) -> None:
    """Raises HTTPException 429 when chat limits are exceeded."""
    if _settings is None or not _settings.rate_limit_enabled:
        return
    ip = _client_ip(request)
    allowed, retry_ms = await _limiter.hit(
        f"ip:{ip}",
        limit=_settings.rate_limit_ip_per_minute,
        window_seconds=60.0,
    )
    if not allowed:
        raise _rate_exceeded(retry_ms, "ip")
    uid = (chat.user_id or "").strip()
    if uid and _settings.rate_limit_user_per_minute > 0:
        h = hashlib.sha256(uid.encode("utf-8")).hexdigest()[:24]
        allowed_u, retry_u = await _limiter.hit(
            f"uid:{h}",
            limit=_settings.rate_limit_user_per_minute,
            window_seconds=60.0,
        )
        if not allowed_u:
            raise _rate_exceeded(retry_u, "user")


def _rate_exceeded(retry_ms: int, dimension: str) -> HTTPException:
    retry_s = max(1, (retry_ms + 999) // 1000)
    return HTTPException(
        status_code=429,
        detail=f"rate_limited:{dimension}",
        headers={"Retry-After": str(retry_s)},
    )

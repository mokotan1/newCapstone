from __future__ import annotations

import asyncio
import logging
import random
import time
from pathlib import Path
from typing import Protocol

logger = logging.getLogger(__name__)

_SCRIPT_PATH = Path(__file__).resolve().parent.parent / "redis_scripts" / "sliding_window_rate_limit.lua"


class RateLimiter(Protocol):
    async def hit(self, redis_key_suffix: str, *, limit: int, window_seconds: float) -> tuple[bool, int]:
        """Return (allowed, retry_after_ms_if_denied)."""


class NullRateLimiter:
    async def close(self) -> None:
        return None

    async def hit(self, redis_key_suffix: str, *, limit: int, window_seconds: float) -> tuple[bool, int]:
        return True, 0


class MemorySlidingWindowLimiter:
    """Per-process sliding window — safe for tests and single-worker only."""

    def __init__(self) -> None:
        self._lock = asyncio.Lock()
        self._events: dict[str, list[float]] = {}

    async def close(self) -> None:
        return None

    async def hit(self, redis_key_suffix: str, *, limit: int, window_seconds: float) -> tuple[bool, int]:
        if limit <= 0:
            return True, 0
        now = time.monotonic()
        window = float(window_seconds)
        key = redis_key_suffix
        async with self._lock:
            timestamps = self._events.setdefault(key, [])
            cutoff = now - window
            while timestamps and timestamps[0] < cutoff:
                timestamps.pop(0)
            if len(timestamps) >= limit:
                oldest = timestamps[0]
                retry_ms = max(0, int((window - (now - oldest)) * 1000) + 1)
                return False, retry_ms
            timestamps.append(now)
            return True, 0


class RedisSlidingWindowLimiter:
    """Atomic sliding window via Redis + Lua."""

    def __init__(self, redis_url: str, *, key_prefix: str = "rl:capstone") -> None:
        try:
            import redis.asyncio as redis  # type: ignore[import-untyped]
        except ImportError as e:
            raise RuntimeError("redis package required for RedisSlidingWindowLimiter") from e

        self._redis_mod = redis
        self._client = redis.from_url(redis_url, encoding="utf-8", decode_responses=True)
        self._prefix = key_prefix
        raw = _SCRIPT_PATH.read_text(encoding="utf-8")
        self._sha: str | None = None
        self._script = raw

    async def _ensure_script(self) -> None:
        if self._sha is None:
            self._sha = await self._client.script_load(self._script)

    async def close(self) -> None:
        aclose = getattr(self._client, "aclose", None)
        if aclose is not None:
            await aclose()
            return
        close = getattr(self._client, "close", None)
        if close is not None:
            res = close()
            if asyncio.iscoroutine(res):
                await res

    async def hit(self, redis_key_suffix: str, *, limit: int, window_seconds: float) -> tuple[bool, int]:
        if limit <= 0:
            return True, 0
        await self._ensure_script()
        key = f"{self._prefix}:{redis_key_suffix}"
        now_ms = int(time.time() * 1000)
        window_ms = int(float(window_seconds) * 1000)
        member = f"{now_ms}:{random.randint(10**6, 10**9)}"
        try:
            result = await self._client.evalsha(
                self._sha,
                1,
                key,
                str(now_ms),
                str(window_ms),
                str(limit),
                member,
            )
        except self._redis_mod.RedisError as e:
            logger.error("Redis rate limit error (fail-closed): %s", e)
            return False, 30_000

        if not result or int(result[0]) != 1:
            retry = int(result[1]) if len(result) > 1 else window_ms
            return False, retry
        return True, 0


def build_rate_limiter(
    *,
    enabled: bool,
    redis_url: str,
    key_prefix: str,
) -> RateLimiter:
    if not enabled:
        return NullRateLimiter()
    stripped = redis_url.strip()
    if stripped:
        try:
            return RedisSlidingWindowLimiter(stripped, key_prefix=key_prefix)
        except Exception as e:
            logger.warning("Redis rate limiter unavailable (%s) — falling back to in-process limiter.", e)
    return MemorySlidingWindowLimiter()

from __future__ import annotations

import pytest

from services.rate_limit import MemorySlidingWindowLimiter


@pytest.mark.asyncio
async def test_sliding_window_blocks_after_limit() -> None:
    lim = MemorySlidingWindowLimiter()
    ok = True
    for _ in range(3):
        allowed, _ = await lim.hit("k1", limit=3, window_seconds=60.0)
        ok = ok and allowed
    assert ok
    allowed4, retry = await lim.hit("k1", limit=3, window_seconds=60.0)
    assert allowed4 is False
    assert retry > 0


@pytest.mark.asyncio
async def test_separate_keys_independent() -> None:
    lim = MemorySlidingWindowLimiter()
    a, _ = await lim.hit("a", limit=1, window_seconds=60.0)
    b, _ = await lim.hit("b", limit=1, window_seconds=60.0)
    assert a and b
    await lim.close()

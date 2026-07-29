"""Preflight: compare room requiredCapabilities to a live DeveloperQa registry."""

from __future__ import annotations

from typing import Iterable, Sequence


def missing_required_capabilities(
    required_capabilities: Sequence[str] | Iterable[str],
    live_capability_ids: Iterable[str],
) -> list[str]:
    """
    Return required capability ids that are absent from the live registry.

    Pure Python helper — does not require Unity. Callers supply live ids from
    DeveloperQaService.ListCapabilities() (or a test fixture).
    """
    live = {str(item) for item in live_capability_ids}
    missing: list[str] = []
    for cap in required_capabilities:
        cap_id = str(cap)
        if cap_id not in live:
            missing.append(cap_id)
    return missing

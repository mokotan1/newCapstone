"""Console delta classification (design AC11)."""

from __future__ import annotations

from collections.abc import Mapping, Sequence
from typing import Any

RELATEDNESS_RELATED = "related"


def classify_console_delta(
    *,
    baseline_fingerprints: Sequence[str],
    collected: bool,
    entries: Sequence[Mapping[str, Any]],
) -> dict[str, Any]:
    """콘솔 delta를 baseline / 신규관련 / 미분류 / 미수집으로 나눈다. 미수집은 PASS가 아니다."""
    if not collected:
        return {
            "classification": "missing",
            "reasonCodes": ["console-missing"],
        }

    baseline = set(baseline_fingerprints)
    novel = [
        dict(entry)
        for entry in entries
        if str(entry.get("fingerprint") or "") not in baseline
    ]
    if any(entry.get("relatedness") == RELATEDNESS_RELATED for entry in novel):
        return {
            "classification": "new-related",
            "reasonCodes": ["console-new-related"],
        }
    if novel:
        return {
            "classification": "unclassified",
            "reasonCodes": ["console-unclassified"],
        }
    if entries:
        return {"classification": "baseline", "reasonCodes": []}
    return {"classification": "clean", "reasonCodes": []}

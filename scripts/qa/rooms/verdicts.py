"""Runtime scenario verdicts vs catalog implementation statuses (design §10)."""

from __future__ import annotations

RUNTIME_VERDICTS: frozenset[str] = frozenset({"PASS", "FAIL", "BLOCKED", "NOT_RUN"})

CATALOG_STATUSES: frozenset[str] = frozenset(
    {"NOT_IMPLEMENTED", "SPEC_MISMATCH", "PARTIAL"}
)

# Full set used on manifests / catalog rows (includes IMPLEMENTED).
CATALOG_IMPLEMENTATION_STATUSES: frozenset[str] = CATALOG_STATUSES | {"IMPLEMENTED"}


def is_runtime_verdict(value: str) -> bool:
    return value in RUNTIME_VERDICTS


def is_catalog_status(value: str) -> bool:
    """True for non-PASS coverage gap statuses (never gameplay PASS/FAIL)."""
    return value in CATALOG_STATUSES


def is_gameplay_failure(value: str) -> bool:
    """Only FAIL counts as a gameplay failure; catalog gaps never do."""
    return value == "FAIL"

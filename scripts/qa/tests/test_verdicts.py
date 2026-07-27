"""Runtime vs catalog verdict helpers (design §10)."""

from __future__ import annotations

from scripts.qa.rooms.verdicts import (
    CATALOG_STATUSES,
    RUNTIME_VERDICTS,
    is_catalog_status,
    is_gameplay_failure,
    is_runtime_verdict,
)


def test_runtime_verdicts_are_pass_fail_blocked_not_run() -> None:
    assert RUNTIME_VERDICTS == frozenset({"PASS", "FAIL", "BLOCKED", "NOT_RUN"})


def test_catalog_statuses_never_equal_runtime_pass_fail() -> None:
    assert "PASS" not in CATALOG_STATUSES
    assert "FAIL" not in CATALOG_STATUSES
    for status in CATALOG_STATUSES:
        assert not is_runtime_verdict(status)
        assert is_catalog_status(status)
        assert not is_gameplay_failure(status)


def test_fail_is_gameplay_failure_but_blocked_is_not() -> None:
    assert is_gameplay_failure("FAIL")
    assert not is_gameplay_failure("BLOCKED")
    assert not is_gameplay_failure("NOT_IMPLEMENTED")

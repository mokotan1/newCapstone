"""Test adapter must not treat exit 0 or unknown counts as passed (design AC21)."""

from __future__ import annotations

from scripts.qa.tool.normalize import normalize_test_adapter_result


def test_zero_executed_tests_cannot_be_passed() -> None:
    result = normalize_test_adapter_result(
        {
            "operation": "test",
            "nativeExitCode": 0,
            "matched": 0,
            "executed": 0,
            "passed": 0,
            "failed": 0,
            "skipped": 0,
        }
    )
    assert result["verificationStatus"] != "passed"
    assert result["resultKind"] == "zero-matched"
    assert result["matched"] == 0
    assert result["executed"] == 0


def test_exit_zero_with_failed_tests_cannot_be_passed() -> None:
    result = normalize_test_adapter_result(
        {
            "operation": "test",
            "nativeExitCode": 0,
            "matched": 2,
            "executed": 2,
            "passed": 1,
            "failed": 1,
            "skipped": 0,
        }
    )
    assert result["verificationStatus"] != "passed"
    assert result["resultKind"] == "test-failed"
    assert result["failed"] == 1


def test_unknown_counts_are_not_coerced_to_zero() -> None:
    result = normalize_test_adapter_result(
        {
            "operation": "test",
            "nativeExitCode": 0,
        }
    )
    assert result["matched"] is None
    assert result["executed"] is None
    assert result["passed"] is None
    assert result["failed"] is None
    assert result["skipped"] is None
    assert result["verificationStatus"] != "passed"
    assert result["resultKind"] == "unknown-counts"

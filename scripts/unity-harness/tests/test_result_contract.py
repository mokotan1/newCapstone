"""Result contract for Unity harness adapters (design AC2, AC3, AC7)."""

from __future__ import annotations

from scripts.unity_harness.result_contract import (
    classify_execution,
    classify_test_counts,
    derive_from_native_exit,
    refuse_if_invalid_project,
    refuse_if_lease_conflict,
    select_backend,
)


def test_native_exit_zero_does_not_become_passed() -> None:
    result = derive_from_native_exit(
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


def test_compile_failure_is_distinct_from_test_failure() -> None:
    compile_failed = classify_execution(
        operation="compile",
        execution_status="failed",
        error_category="compilation",
    )
    test_failed = classify_test_counts(
        matched=2,
        executed=2,
        passed=1,
        failed=1,
        skipped=0,
    )
    assert compile_failed["resultKind"] == "compile-failed"
    assert test_failed["resultKind"] == "test-failed"
    assert compile_failed["errorCategory"] == "compilation"
    assert test_failed["errorCategory"] == "test-failure"
    assert compile_failed["verificationStatus"] == "failed"
    assert test_failed["verificationStatus"] == "failed"


def test_zero_matched_is_distinct_from_test_failure() -> None:
    zero = classify_test_counts(
        matched=0,
        executed=0,
        passed=0,
        failed=0,
        skipped=0,
    )
    failed = classify_test_counts(
        matched=3,
        executed=3,
        passed=2,
        failed=1,
        skipped=0,
    )
    assert zero["resultKind"] == "zero-matched"
    assert failed["resultKind"] == "test-failed"
    assert zero["verificationStatus"] == "failed"
    assert failed["verificationStatus"] == "failed"


def test_timeout_is_distinct_from_compile_and_test_failures() -> None:
    timed_out = classify_execution(
        operation="compile",
        execution_status="timed-out",
        error_category="timeout",
    )
    assert timed_out["resultKind"] == "timed-out"
    assert timed_out["executionStatus"] == "timed-out"
    assert timed_out["errorCategory"] == "timeout"
    assert timed_out["verificationStatus"] == "blocked"


def test_wrong_project_path_is_blocked_before_mutation() -> None:
    result = refuse_if_invalid_project(
        requested_path="D:/OtherProject",
        expected_path="disputatio",
    )
    assert result["executionStatus"] == "blocked"
    assert result["verificationStatus"] == "blocked"
    assert result["errorCategory"] == "invalid-input"


def test_foreign_lease_is_blocked_before_mutation() -> None:
    result = refuse_if_lease_conflict(
        current_owner="qa-playtester",
        requested_owner="implementer",
    )
    assert result["executionStatus"] == "blocked"
    assert result["errorCategory"] == "ownership"


def test_official_backend_stays_blocked_until_verified() -> None:
    result = select_backend(
        {
            "activeBackend": "legacy-unity-cli",
            "backends": {
                "legacy-unity-cli": {"connectorPackage": "com.youngwoocho02.unity-cli-connector"},
                "official-unity-cli": {"status": "not-verified"},
            },
        },
        requested="official-unity-cli",
    )
    assert result["executionStatus"] == "blocked"
    assert result["verificationStatus"] == "blocked"
    assert result["backend"] == "official-unity-cli"

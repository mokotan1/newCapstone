"""Unity harness result contract.

Derives verificationStatus from counts and execution state. Native exit code 0
is never enough to mark a check passed.
"""

from __future__ import annotations

import re
import xml.etree.ElementTree as ET
from typing import Any, Mapping, MutableMapping

_QA_ACTIONS = {
    "status": "qa_status",
    "list": "qa_list",
    "run": "qa_run",
    "start": "qa_run",
    "cancel": "qa_cancel",
    "capture": "qa_capture",
    "recover": "qa_recover",
}

_STDOUT_COUNTS = re.compile(
    r"Passed:\s*(\d+)\s+Failed:\s*(\d+)\s+Skipped:\s*(\d+)",
    re.IGNORECASE,
)

RESULT_FIELDS = (
    "schemaVersion",
    "operation",
    "backend",
    "toolVersion",
    "connectorVersion",
    "projectPath",
    "editorPid",
    "startedAt",
    "finishedAt",
    "executionStatus",
    "verificationStatus",
    "nativeExitCode",
    "artifacts",
    "errorCategory",
)

_BASE: dict[str, Any] = {
    "schemaVersion": "1",
    "backend": "legacy-unity-cli",
    "toolVersion": None,
    "connectorVersion": None,
    "editorPid": None,
    "startedAt": None,
    "finishedAt": None,
    "nativeExitCode": None,
    "artifacts": [],
}


def _result(**fields: Any) -> dict[str, Any]:
    payload = dict(_BASE)
    payload.update(fields)
    return payload


def parse_nunit_xml(text: str) -> dict[str, int]:
    root = ET.fromstring(text.strip())
    total = int(root.get("total") or root.get("testcasecount") or 0)
    passed = int(root.get("passed") or 0)
    failed = int(root.get("failed") or root.get("failures") or 0)
    skipped = int(root.get("skipped") or root.get("ignored") or 0)
    executed = passed + failed + skipped
    if executed == 0:
        executed = total
    return {
        "matched": total,
        "executed": executed,
        "passed": passed,
        "failed": failed,
        "skipped": skipped,
    }


def parse_test_stdout(text: str) -> dict[str, int]:
    match = _STDOUT_COUNTS.search(text)
    if match is None:
        return {
            "matched": 0,
            "executed": 0,
            "passed": 0,
            "failed": 0,
            "skipped": 0,
        }
    passed = int(match.group(1))
    failed = int(match.group(2))
    skipped = int(match.group(3))
    total = passed + failed + skipped
    return {
        "matched": total,
        "executed": total,
        "passed": passed,
        "failed": failed,
        "skipped": skipped,
    }


def map_qa_action(action: str) -> str:
    key = action.strip().lower()
    if key not in _QA_ACTIONS:
        raise ValueError(f"unknown qa action: {action}")
    return _QA_ACTIONS[key]


def classify_test_counts(
    *,
    matched: int,
    executed: int,
    passed: int,
    failed: int,
    skipped: int,
) -> dict[str, Any]:
    counts = {
        "matched": matched,
        "executed": executed,
        "passed": passed,
        "failed": failed,
        "skipped": skipped,
    }
    if matched == 0 or executed == 0:
        payload = _result(
            operation="test",
            executionStatus="succeeded",
            verificationStatus="failed",
            errorCategory="test-failure",
            resultKind="zero-matched",
        )
        payload.update(counts)
        return payload
    if failed > 0:
        payload = _result(
            operation="test",
            executionStatus="succeeded",
            verificationStatus="failed",
            errorCategory="test-failure",
            resultKind="test-failed",
        )
        payload.update(counts)
        return payload
    payload = _result(
        operation="test",
        executionStatus="succeeded",
        verificationStatus="passed",
        errorCategory="none",
        resultKind="ok",
    )
    payload.update(counts)
    return payload


def classify_execution(
    *,
    operation: str,
    execution_status: str,
    error_category: str,
) -> dict[str, Any]:
    if execution_status == "timed-out" or error_category == "timeout":
        return _result(
            operation=operation,
            executionStatus="timed-out",
            verificationStatus="blocked",
            errorCategory="timeout",
            resultKind="timed-out",
        )
    if error_category == "compilation":
        return _result(
            operation=operation,
            executionStatus="failed",
            verificationStatus="failed",
            errorCategory="compilation",
            resultKind="compile-failed",
        )
    return _result(
        operation=operation,
        executionStatus=execution_status,
        verificationStatus="failed",
        errorCategory=error_category,
        resultKind="failed",
    )


def derive_from_native_exit(raw: Mapping[str, Any]) -> dict[str, Any]:
    """Exit code 0 does not imply passed; test counts decide."""
    operation = str(raw.get("operation", "test"))
    if operation == "test":
        result = classify_test_counts(
            matched=int(raw.get("matched") or 0),
            executed=int(raw.get("executed") or 0),
            passed=int(raw.get("passed") or 0),
            failed=int(raw.get("failed") or 0),
            skipped=int(raw.get("skipped") or 0),
        )
        result["nativeExitCode"] = raw.get("nativeExitCode")
        return result
    if int(raw.get("nativeExitCode") or 0) != 0:
        return classify_execution(
            operation=operation,
            execution_status="failed",
            error_category="compilation" if operation == "compile" else "none",
        )
    return _result(
        operation=operation,
        executionStatus="succeeded",
        verificationStatus="passed",
        errorCategory="none",
        resultKind="ok",
        nativeExitCode=raw.get("nativeExitCode"),
    )


def refuse_if_invalid_project(
    *,
    requested_path: str,
    expected_path: str,
) -> dict[str, Any]:
    requested = requested_path.replace("\\", "/").rstrip("/")
    expected = expected_path.replace("\\", "/").rstrip("/")
    if requested == expected or requested.endswith("/" + expected):
        return _result(
            operation="probe",
            projectPath=requested_path,
            executionStatus="succeeded",
            verificationStatus="not-applicable",
            errorCategory="none",
            resultKind="ok",
        )
    return _result(
        operation="probe",
        projectPath=requested_path,
        executionStatus="blocked",
        verificationStatus="blocked",
        errorCategory="invalid-input",
        resultKind="invalid-project",
    )


def refuse_if_lease_conflict(
    *,
    current_owner: str | None,
    requested_owner: str | None,
) -> dict[str, Any]:
    if current_owner and requested_owner and current_owner != requested_owner:
        return _result(
            operation="qa",
            executionStatus="blocked",
            verificationStatus="blocked",
            errorCategory="ownership",
            resultKind="lease-conflict",
        )
    return _result(
        operation="qa",
        executionStatus="succeeded",
        verificationStatus="not-applicable",
        errorCategory="none",
        resultKind="ok",
    )


def select_backend(
    toolchain: Mapping[str, Any],
    *,
    requested: str,
) -> dict[str, Any]:
    backends: Mapping[str, Any] = toolchain.get("backends", {})
    official = backends.get("official-unity-cli", {})
    if requested == "official-unity-cli":
        status = str(official.get("status", "not-verified"))
        if status != "verified":
            return _result(
                operation="probe",
                backend="official-unity-cli",
                executionStatus="blocked",
                verificationStatus="blocked",
                errorCategory="version-mismatch",
                resultKind="backend-not-verified",
            )
    active = str(toolchain.get("activeBackend", "legacy-unity-cli"))
    chosen = requested if requested in backends else active
    return _result(
        operation="probe",
        backend=chosen,
        executionStatus="succeeded",
        verificationStatus="not-applicable",
        errorCategory="none",
        resultKind="ok",
    )


def ensure_required_fields(payload: MutableMapping[str, Any]) -> None:
    for key in RESULT_FIELDS:
        payload.setdefault(key, None)

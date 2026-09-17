"""Normalize test-adapter results without treating exit 0 as passed (design AC21)."""

from __future__ import annotations

from collections.abc import Mapping
from typing import Any

from scripts.unity_harness.result_contract import derive_from_native_exit

_COUNT_KEYS: tuple[str, ...] = (
    "matched",
    "executed",
    "passed",
    "failed",
    "skipped",
)


def normalize_test_adapter_result(raw: Mapping[str, Any]) -> dict[str, Any]:
    """테스트 어댑터 결과를 정규화한다. 실행 0건과 실제 실패는 passed가 될 수 없다."""
    missing = [key for key in _COUNT_KEYS if raw.get(key) is None]
    if missing:
        payload: dict[str, Any] = {
            "operation": raw.get("operation", "test"),
            "nativeExitCode": raw.get("nativeExitCode"),
            "executionStatus": "succeeded",
            "verificationStatus": "failed",
            "errorCategory": "test-failure",
            "resultKind": "unknown-counts",
        }
        for key in _COUNT_KEYS:
            payload[key] = raw.get(key, None)
        return payload

    return derive_from_native_exit(dict(raw))

"""Classify structured QA failure evidence into repair categories (design §6)."""

from __future__ import annotations

from enum import Enum
from typing import Any, Mapping


class FailureClass(str, Enum):
    MISSING_QA_CAPABILITY = "MissingQaCapability"
    PRODUCT_DEFECT = "ProductDefect"
    ENVIRONMENT_BLOCKED = "EnvironmentBlocked"
    INVALID_SCENARIO = "InvalidScenario"


_MISSING_RESULT_CODES = frozenset(
    {
        "MissingCapability",
        "MissingProbe",
        "MissingPreset",
        "UnsupportedSceneAdapter",
    }
)

_ENV_FLAGS = frozenset(
    {
        "unity_unavailable",
        "compile_unavailable",
        "backend_unavailable",
        "corrupted_external_fixture",
    }
)


def classify(evidence: Mapping[str, Any]) -> str:
    """Return a FailureClass value string from structured evidence.

    Priority: EnvironmentBlocked → InvalidScenario → ProductDefect (executed +
    assert failed) → MissingQaCapability. Assertion failures after a capability
    executed must never be reclassified as MissingQaCapability.
    """
    if not isinstance(evidence, Mapping):
        raise TypeError("evidence must be a mapping")

    if _is_environment_blocked(evidence):
        return FailureClass.ENVIRONMENT_BLOCKED.value

    if _is_invalid_scenario(evidence):
        return FailureClass.INVALID_SCENARIO.value

    if _is_product_defect(evidence):
        return FailureClass.PRODUCT_DEFECT.value

    if _is_missing_qa_capability(evidence):
        return FailureClass.MISSING_QA_CAPABILITY.value

    raise ValueError(f"unable to classify evidence keys={sorted(evidence.keys())}")


def _truthy(evidence: Mapping[str, Any], key: str) -> bool:
    return bool(evidence.get(key))


def _is_environment_blocked(evidence: Mapping[str, Any]) -> bool:
    if any(_truthy(evidence, key) for key in _ENV_FLAGS):
        return True
    result_code = str(evidence.get("result_code", ""))
    return result_code in {"UnityUnavailable", "CompileUnavailable", "EnvironmentBlocked"}


def _is_invalid_scenario(evidence: Mapping[str, Any]) -> bool:
    if _truthy(evidence, "invalid_schema"):
        return True
    result_code = str(evidence.get("result_code", ""))
    return result_code in {"InvalidCommand", "InvalidScenario", "InvalidTarget"}


def _is_product_defect(evidence: Mapping[str, Any]) -> bool:
    result_code = str(evidence.get("result_code", ""))
    if result_code == "AssertionFailed" and _truthy(evidence, "capability_executed"):
        return True
    if _truthy(evidence, "capability_executed") and _truthy(evidence, "assert_failed"):
        return True
    return False


def _is_missing_qa_capability(evidence: Mapping[str, Any]) -> bool:
    result_code = str(evidence.get("result_code", ""))
    if result_code in _MISSING_RESULT_CODES:
        return True
    return any(
        evidence.get(key)
        for key in (
            "missing_capability_id",
            "missing_probe_id",
            "missing_preset_id",
        )
    )

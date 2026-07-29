"""Failure classification rules (design §6)."""

from __future__ import annotations

from autorun.classify import classify


def test_unknown_capability_is_missing_qa_capability() -> None:
    evidence = {
        "result_code": "MissingCapability",
        "missing_capability_id": "studyroom.mirror.place-bookmark",
    }
    assert classify(evidence) == "MissingQaCapability"


def test_missing_probe_is_missing_qa_capability() -> None:
    evidence = {
        "result_code": "MissingProbe",
        "missing_probe_id": "studyroom.mirror.state",
    }
    assert classify(evidence) == "MissingQaCapability"


def test_assert_failed_after_capability_executed_is_product_defect() -> None:
    evidence = {
        "result_code": "AssertionFailed",
        "capability_executed": True,
        "capability_id": "studyroom.mirror.place-bookmark",
    }
    assert classify(evidence) == "ProductDefect"


def test_unity_unavailable_is_environment_blocked() -> None:
    evidence = {
        "result_code": "Error",
        "unity_unavailable": True,
    }
    assert classify(evidence) == "EnvironmentBlocked"


def test_compile_unavailable_is_environment_blocked() -> None:
    evidence = {
        "result_code": "Error",
        "compile_unavailable": True,
    }
    assert classify(evidence) == "EnvironmentBlocked"


def test_invalid_schema_is_invalid_scenario() -> None:
    evidence = {
        "result_code": "InvalidCommand",
        "invalid_schema": True,
    }
    assert classify(evidence) == "InvalidScenario"


def test_assertion_failure_not_reclassified_as_missing_capability() -> None:
    evidence = {
        "result_code": "AssertionFailed",
        "capability_executed": True,
        "missing_capability_id": "should-not-matter",
    }
    assert classify(evidence) == "ProductDefect"

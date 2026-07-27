"""Orchestrator retry budget and state machine skeleton."""

from __future__ import annotations

from autorun.orchestrator import (
    MAX_ATTEMPTS_PER_SIGNATURE,
    AutorunOrchestrator,
    OrchestratorState,
)


def test_explicit_states_include_repair_pipeline() -> None:
    names = {state.value for state in OrchestratorState}
    expected = {
        "PREFLIGHT",
        "RUNNING",
        "CLASSIFYING",
        "PATCHING_QA",
        "PATCHING_PRODUCT",
        "BLOCKED",
        "COMPILING",
        "FOCUSED_TEST",
        "REGRESSION_TEST",
        "COMMITTING",
        "RESUMING",
        "PASS",
        "FAIL",
    }
    assert expected.issubset(names)


def test_start_moves_preflight_to_running() -> None:
    orch = AutorunOrchestrator()
    assert orch.state == OrchestratorState.PREFLIGHT
    orch.start()
    assert orch.state == OrchestratorState.RUNNING


def test_missing_capability_routes_to_patching_qa() -> None:
    orch = AutorunOrchestrator()
    orch.start()
    next_state = orch.handle_failure(
        classification="MissingQaCapability",
        signature="missing:studyroom.mirror.place-bookmark",
    )
    assert next_state == OrchestratorState.PATCHING_QA
    assert orch.state == OrchestratorState.PATCHING_QA
    assert orch.attempt_count("missing:studyroom.mirror.place-bookmark") == 1


def test_product_defect_routes_to_patching_product() -> None:
    orch = AutorunOrchestrator()
    orch.start()
    next_state = orch.handle_failure(
        classification="ProductDefect",
        signature="assert:mirror-placed",
    )
    assert next_state == OrchestratorState.PATCHING_PRODUCT


def test_environment_blocked_ends_as_blocked() -> None:
    orch = AutorunOrchestrator()
    orch.start()
    next_state = orch.handle_failure(
        classification="EnvironmentBlocked",
        signature="env:unity-unavailable",
    )
    assert next_state == OrchestratorState.BLOCKED


def test_fourth_occurrence_of_same_signature_is_blocked() -> None:
    orch = AutorunOrchestrator()
    orch.start()
    signature = "missing:studyroom.mirror.place-bookmark"
    for _ in range(MAX_ATTEMPTS_PER_SIGNATURE):
        state = orch.handle_failure(
            classification="MissingQaCapability",
            signature=signature,
        )
        assert state == OrchestratorState.PATCHING_QA
        orch.resume_after_patch()

    blocked = orch.handle_failure(
        classification="MissingQaCapability",
        signature=signature,
    )
    assert blocked == OrchestratorState.BLOCKED
    assert orch.attempt_count(signature) == MAX_ATTEMPTS_PER_SIGNATURE + 1


def test_normalize_failure_signature_is_stable() -> None:
    orch = AutorunOrchestrator()
    evidence = {
        "result_code": "MissingCapability",
        "missing_capability_id": "studyroom.mirror.place-bookmark",
        "scene": "StudyRoom",
        "transient_run_id": "should-ignore",
    }
    sig_a = orch.normalize_failure_signature(evidence)
    sig_b = orch.normalize_failure_signature(evidence)
    assert sig_a == sig_b
    assert "should-ignore" not in sig_a
    assert "studyroom.mirror.place-bookmark" in sig_a

"""Scenario and aggregate verdicts (design AC09, AC17)."""

from __future__ import annotations

from scripts.qa.tool.verdict import aggregate_run, judge_scenario


def _passing_record(**overrides: object) -> dict[str, object]:
    record: dict[str, object] = {
        "scenarioId": "qa.tool.hall-to-kitchen",
        "started": True,
        "cancelled": False,
        "requiredStepIds": ["navigate", "assert-kitchen", "capture"],
        "executedStepIds": ["navigate", "assert-kitchen", "capture"],
        "requiredInputLayers": ["api", "event-system"],
        "executedInputLayers": ["api", "event-system"],
        "assertions": [
            {
                "id": "scene.destination",
                "expected": "Kitchen",
                "observed": "Kitchen",
                "verdict": "PASS",
            }
        ],
        "evidenceValid": True,
        "cleanupStatus": "restored",
        "consoleClassification": "clean",
        "requiredArtifactKinds": ["screenshot"],
        "validatedArtifactKinds": ["screenshot"],
    }
    record.update(overrides)
    return record


def test_failed_assertion_is_fail_even_if_cleanup_failed() -> None:
    result = judge_scenario(
        _passing_record(
            assertions=[
                {
                    "id": "scene.destination",
                    "expected": "Kitchen",
                    "observed": "Hall_playerble",
                    "verdict": "FAIL",
                }
            ],
            cleanupStatus="failed",
            evidenceValid=False,
        )
    )
    assert result["scenarioVerdict"] == "FAIL"
    assert "assertion-failed" in result["reasonCodes"]
    assert "cleanup-failed" in result["reasonCodes"]


def test_missing_required_step_is_blocked_not_pass() -> None:
    result = judge_scenario(
        _passing_record(
            executedStepIds=["navigate"],
            validatedArtifactKinds=[],
            evidenceValid=False,
        )
    )
    assert result["scenarioVerdict"] == "BLOCKED"
    assert "missing-required-step" in result["reasonCodes"]
    assert result["scenarioVerdict"] != "PASS"


def test_never_started_is_not_run() -> None:
    result = judge_scenario(_passing_record(started=False, executedStepIds=[]))
    assert result["scenarioVerdict"] == "NOT_RUN"


def test_partial_success_cannot_make_run_pass() -> None:
    failed = judge_scenario(
        _passing_record(
            assertions=[
                {
                    "id": "scene.destination",
                    "expected": "Kitchen",
                    "observed": "Hall_playerble",
                    "verdict": "FAIL",
                }
            ]
        )
    )
    blocked = judge_scenario(_passing_record(executedStepIds=["navigate"]))
    skipped = {
        "scenarioId": "room.basement.entry",
        "scenarioVerdict": "NOT_RUN",
        "required": False,
        "excluded": True,
        "exclusionReason": "phase-3",
    }
    summary = aggregate_run(
        [
            {**failed, "required": True, "excluded": False},
            {**blocked, "required": True, "excluded": False},
            skipped,
        ]
    )
    assert summary["runVerdict"] == "FAIL"
    assert summary["counts"] == {
        "required": 2,
        "executed": 2,
        "passed": 0,
        "failed": 1,
        "blocked": 1,
        "notRun": 0,
        "excluded": 1,
    }


def test_legacy_manifest_pass_without_new_fields_is_blocked() -> None:
    result = judge_scenario(
        {
            "scenarioId": "legacy.hall",
            "started": True,
            "legacyManifest": True,
            "legacyVerdict": "Pass",
        }
    )
    assert result["scenarioVerdict"] == "BLOCKED"
    assert "legacy-schema" in result["reasonCodes"]


def test_isolation_violation_blocks_pass() -> None:
    result = judge_scenario(_passing_record(isolationPreserved=False))
    assert result["scenarioVerdict"] == "BLOCKED"
    assert "unauthorized-player-change" in result["reasonCodes"]


def test_failed_recovery_blocks_pass() -> None:
    result = judge_scenario(_passing_record(recoveryStatus="failed"))
    assert result["scenarioVerdict"] == "BLOCKED"
    assert "recovery-failed" in result["reasonCodes"]


def test_transport_down_blocks_pass() -> None:
    result = judge_scenario(_passing_record(transportStatus="down"))
    assert result["scenarioVerdict"] == "BLOCKED"
    assert "transport-down" in result["reasonCodes"]


def test_unsettled_dialogue_blocks_pass() -> None:
    result = judge_scenario(_passing_record(dialogueStatus="unsettled"))
    assert result["scenarioVerdict"] == "BLOCKED"
    assert "dialogue-unsettled" in result["reasonCodes"]


def test_passing_record_with_new_fields_still_passes() -> None:
    result = judge_scenario(
        _passing_record(
            isolationPreserved=True,
            recoveryStatus="restored",
            transportStatus="up",
            dialogueStatus="settled",
        )
    )
    assert result["scenarioVerdict"] == "PASS"

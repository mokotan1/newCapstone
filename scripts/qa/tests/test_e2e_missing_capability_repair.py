"""E2E vertical slice: missing StudyRoom capability → repair → resume → PASS (Task 12)."""

from __future__ import annotations

from pathlib import Path

from autorun.capability_fixture import (
    PLACE_BOOKMARK_CAPABILITY_ID,
    InMemoryCapabilityRegistry,
    apply_fixture_capability_patch,
)
from autorun.checkpoint import Checkpoint, load_checkpoint, save_checkpoint
from autorun.classify import classify
from autorun.orchestrator import AutorunOrchestrator, OrchestratorState
from autorun.report import render_report


def test_e2e_missing_place_bookmark_repair_loop_reaches_pass(tmp_path: Path) -> None:
    """Intentionally missing capability → classify → patch → focused validate → resume → PASS."""
    # Fixture: StudyRoom-ish catalog missing place-bookmark only.
    registry = InMemoryCapabilityRegistry(
        initial_ids={
            "studyroom.mirror.preset.before-placement",
            "studyroom.mirror.grant-bookmark",
            "studyroom.mirror.probe",
        }
    )
    assert not registry.has(PLACE_BOOKMARK_CAPABILITY_ID)

    orch = AutorunOrchestrator()
    orch.start()
    assert orch.state == OrchestratorState.RUNNING

    # --- RUNNING: invoke missing capability ---
    evidence = registry.invoke(PLACE_BOOKMARK_CAPABILITY_ID)
    assert evidence["result_code"] == "MissingCapability"
    assert evidence["missing_capability_id"] == PLACE_BOOKMARK_CAPABILITY_ID
    assert evidence.get("capability_executed") is not True

    classification = classify(evidence)
    assert classification == "MissingQaCapability"

    signature = orch.normalize_failure_signature(evidence)
    assert PLACE_BOOKMARK_CAPABILITY_ID in signature

    checkpoint = Checkpoint(
        scenario_step="studyroom.mirror.place-bookmark",
        git_commit="fixture-base-commit",
        active_scene="StudyRoom",
        qa_profile_id="qa-e2e-profile",
        snapshot={"registry_ids": sorted(registry.list_ids())},
        console_cursor=0,
        capability_registry_version=registry.version,
    )
    checkpoint_path = tmp_path / "checkpoint.json"
    save_checkpoint(checkpoint_path, checkpoint)

    next_state = orch.handle_failure(classification, signature)
    assert next_state == OrchestratorState.PATCHING_QA

    # --- PATCHING_QA: simulate capability patch (temp file + in-memory register) ---
    patch_dir = tmp_path / "patches"
    patch_meta = apply_fixture_capability_patch(
        registry,
        capability_id=PLACE_BOOKMARK_CAPABILITY_ID,
        patch_dir=patch_dir,
    )
    assert registry.has(PLACE_BOOKMARK_CAPABILITY_ID)
    assert patch_meta["capability_id"] == PLACE_BOOKMARK_CAPABILITY_ID
    assert Path(patch_meta["patch_path"]).is_file()

    # --- COMPILING → FOCUSED_TEST → REGRESSION → COMMITTING ---
    assert orch.begin_compile() == OrchestratorState.COMPILING
    assert orch.begin_focused_test() == OrchestratorState.FOCUSED_TEST

    focused_ok = registry.has(PLACE_BOOKMARK_CAPABILITY_ID)
    assert focused_ok is True
    assert orch.complete_focused_test(passed=True) == OrchestratorState.REGRESSION_TEST
    assert orch.complete_regression_test(passed=True) == OrchestratorState.COMMITTING

    # --- RESUMING → RUNNING ---
    resumed = orch.resume_after_patch()
    assert resumed == OrchestratorState.RUNNING
    assert OrchestratorState.RESUMING in orch.transitions

    restored = load_checkpoint(checkpoint_path)
    assert restored.scenario_step == "studyroom.mirror.place-bookmark"
    assert restored.active_scene == "StudyRoom"

    # --- Resume invoke succeeds (evidence verdict path) ---
    resume_evidence = registry.invoke(PLACE_BOOKMARK_CAPABILITY_ID)
    assert resume_evidence["result_code"] == "Ok"
    assert resume_evidence["capability_executed"] is True
    assert resume_evidence["capability_id"] == PLACE_BOOKMARK_CAPABILITY_ID

    assert orch.mark_pass() == OrchestratorState.PASS

    report = render_report(
        {
            "run_id": "e2e-missing-place-bookmark",
            "verdict": "PASS",
            "state": orch.state.value,
            "classification": classification,
            "failure_signature": signature,
            "attempts": orch.attempt_count(signature),
            "patched_capability": PLACE_BOOKMARK_CAPABILITY_ID,
        }
    )
    assert "Verdict: `PASS`" in report
    assert "MissingQaCapability" in report
    assert orch.attempt_count(signature) == 1

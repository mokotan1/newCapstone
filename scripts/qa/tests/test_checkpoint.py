"""Checkpoint serialization for repair resume."""

from __future__ import annotations

from pathlib import Path

from autorun.checkpoint import Checkpoint, load_checkpoint, save_checkpoint


def test_checkpoint_round_trip(tmp_path: Path) -> None:
    checkpoint = Checkpoint(
        scenario_step="place-bookmark",
        git_commit="abc123def",
        active_scene="StudyRoom",
        qa_profile_id="profile-qa-1",
        snapshot={"mirrorPlaced": False},
        console_cursor=42,
        capability_registry_version="1.0.0",
    )
    path = tmp_path / "checkpoint.json"
    save_checkpoint(path, checkpoint)
    loaded = load_checkpoint(path)
    assert loaded == checkpoint
    assert loaded.scenario_step == "place-bookmark"
    assert loaded.console_cursor == 42


def test_checkpoint_rejects_empty_git_commit(tmp_path: Path) -> None:
    checkpoint = Checkpoint(
        scenario_step="step",
        git_commit="",
        active_scene="StudyRoom",
        qa_profile_id="p1",
        snapshot={},
        console_cursor=0,
        capability_registry_version="0",
    )
    path = tmp_path / "bad.json"
    try:
        save_checkpoint(path, checkpoint)
        raised = False
    except ValueError:
        raised = True
    assert raised

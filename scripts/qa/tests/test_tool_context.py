"""Run context fingerprint (design AC16)."""

from __future__ import annotations

from pathlib import Path

from scripts.qa.tool.context import compute_diff_hash, evaluate_reuse, read_git_worktree


def test_diff_hash_changes_when_related_uncommitted_file_changes() -> None:
    scoped = ["scripts/qa/tool/plan.py"]
    first = compute_diff_hash(
        base_revision="458d04e3",
        scoped_paths=scoped,
        file_contents={"scripts/qa/tool/plan.py": "version-a"},
        untracked_paths=[],
    )
    second = compute_diff_hash(
        base_revision="458d04e3",
        scoped_paths=scoped,
        file_contents={"scripts/qa/tool/plan.py": "version-b"},
        untracked_paths=[],
    )
    assert first != second
    assert len(first) == 64


def test_qa_run_artifacts_are_excluded_from_diff_hash() -> None:
    scoped = ["scripts/qa/tool/plan.py", "docs/qa/runs/20260915T000000Z-run-x/report.json"]
    without_artifact = compute_diff_hash(
        base_revision="458d04e3",
        scoped_paths=scoped,
        file_contents={"scripts/qa/tool/plan.py": "same"},
        untracked_paths=[],
    )
    with_artifact = compute_diff_hash(
        base_revision="458d04e3",
        scoped_paths=scoped,
        file_contents={
            "scripts/qa/tool/plan.py": "same",
            "docs/qa/runs/20260915T000000Z-run-x/report.json": "new-report",
        },
        untracked_paths=["docs/qa/runs/20260915T000000Z-run-x/report.json"],
    )
    assert without_artifact == with_artifact


def test_untracked_in_scope_changes_diff_hash() -> None:
    scoped = ["scripts/qa/tool/"]
    clean = compute_diff_hash(
        base_revision="458d04e3",
        scoped_paths=scoped,
        file_contents={},
        untracked_paths=[],
    )
    dirty = compute_diff_hash(
        base_revision="458d04e3",
        scoped_paths=scoped,
        file_contents={"scripts/qa/tool/new_helper.py": "print(1)"},
        untracked_paths=["scripts/qa/tool/new_helper.py"],
    )
    assert clean != dirty


def test_previous_pass_is_not_reused_when_diff_hash_changes() -> None:
    result = evaluate_reuse(
        previous={"diffHash": "aaa", "scenarioVerdict": "PASS"},
        current={"diffHash": "bbb"},
    )
    assert result["reusable"] is False
    assert "diff-hash-changed" in result["reasonCodes"]


def test_same_diff_hash_can_reuse_previous_pass() -> None:
    result = evaluate_reuse(
        previous={"diffHash": "aaa", "scenarioVerdict": "PASS"},
        current={"diffHash": "aaa"},
    )
    assert result["reusable"] is True
    assert result["reasonCodes"] == []


def test_read_git_worktree_hash_is_stable_for_same_scoped_file() -> None:
    scoped = ["scripts/qa/tool/context.py"]
    first = read_git_worktree(Path("."), scoped)
    second = read_git_worktree(Path("."), scoped)
    assert first["baseRevision"]
    assert first["diffHash"] == second["diffHash"]
    assert len(first["diffHash"]) == 64

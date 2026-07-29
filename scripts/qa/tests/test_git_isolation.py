"""Git isolation: owned-path rollback without reset --hard."""

from __future__ import annotations

import subprocess
from pathlib import Path
from typing import Callable, Sequence

import pytest

from autorun.git_isolation import (
    GitIsolationSession,
    UnownedDirtyChangesError,
)

GitRunner = Callable[[Sequence[str], Path], subprocess.CompletedProcess[str]]


def _run_git(cwd: Path, *args: str) -> str:
    completed = subprocess.run(
        ["git", *args],
        cwd=cwd,
        check=True,
        capture_output=True,
        text=True,
    )
    return completed.stdout.strip()


def _init_repo(tmp_path: Path) -> Path:
    repo = tmp_path / "repo"
    repo.mkdir()
    _run_git(repo, "init")
    _run_git(repo, "config", "user.email", "qa@example.com")
    _run_git(repo, "config", "user.name", "QA Autorun")
    (repo / "owned.txt").write_text("base\n", encoding="utf-8")
    (repo / "unrelated.txt").write_text("keep\n", encoding="utf-8")
    _run_git(repo, "add", "owned.txt", "unrelated.txt")
    _run_git(repo, "commit", "-m", "init")
    return repo


def test_refuse_unowned_dirty_changes(tmp_path: Path) -> None:
    repo = _init_repo(tmp_path)
    (repo / "unrelated.txt").write_text("dirty-unowned\n", encoding="utf-8")
    session = GitIsolationSession(repo_path=repo, owned_paths=["owned.txt"])
    with pytest.raises(UnownedDirtyChangesError):
        session.begin()


def test_begin_records_base_commit_and_owned_paths(tmp_path: Path) -> None:
    repo = _init_repo(tmp_path)
    head = _run_git(repo, "rev-parse", "HEAD")
    session = GitIsolationSession(repo_path=repo, owned_paths=["owned.txt"])
    base = session.begin()
    assert base == head
    assert session.base_commit == head
    assert session.owned_paths == ("owned.txt",)


def test_rollback_owned_paths_without_reset_hard(tmp_path: Path) -> None:
    repo = _init_repo(tmp_path)
    commands: list[tuple[str, ...]] = []

    def recording_runner(
        args: Sequence[str], cwd: Path
    ) -> subprocess.CompletedProcess[str]:
        commands.append(tuple(args))
        return subprocess.run(
            list(args),
            cwd=cwd,
            check=True,
            capture_output=True,
            text=True,
        )

    session = GitIsolationSession(
        repo_path=repo,
        owned_paths=["owned.txt", "owned_draft.py"],
        git_runner=recording_runner,
    )
    session.begin()
    (repo / "owned.txt").write_text("patched\n", encoding="utf-8")
    (repo / "owned_draft.py").write_text("new\n", encoding="utf-8")
    (repo / "unrelated.txt").write_text("keep\n", encoding="utf-8")

    session.rollback()

    assert (repo / "owned.txt").read_text(encoding="utf-8") == "base\n"
    assert not (repo / "owned_draft.py").exists()
    assert (repo / "unrelated.txt").read_text(encoding="utf-8") == "keep\n"
    flat = " ".join(" ".join(c) for c in commands)
    assert "reset --hard" not in flat
    assert "--hard" not in flat


def test_rollback_does_not_accept_foreign_worktree(tmp_path: Path) -> None:
    repo = _init_repo(tmp_path)
    foreign = tmp_path / "other-worktree"
    foreign.mkdir()
    session = GitIsolationSession(repo_path=repo, owned_paths=["owned.txt"])
    session.begin()
    with pytest.raises(ValueError):
        session.rollback(target_path=foreign)

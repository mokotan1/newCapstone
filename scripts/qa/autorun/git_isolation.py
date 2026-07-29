"""Git isolation for bounded autorun patches (design §8).

Records base commit + owned paths; restores only owned paths without
`git reset --hard`. Refuses unowned dirty worktrees. Never operates on a
foreign worktree path.
"""

from __future__ import annotations

import subprocess
from pathlib import Path
from typing import Callable, Sequence


class UnownedDirtyChangesError(RuntimeError):
    """Raised when the worktree has dirty paths outside the owned set."""


GitRunner = Callable[[Sequence[str], Path], subprocess.CompletedProcess[str]]


def _default_git_runner(
    args: Sequence[str], cwd: Path
) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        list(args),
        cwd=cwd,
        check=True,
        capture_output=True,
        text=True,
    )


class GitIsolationSession:
    """Scoped patch session bound to a single repository worktree."""

    def __init__(
        self,
        repo_path: Path | str,
        owned_paths: Sequence[str],
        git_runner: GitRunner | None = None,
    ) -> None:
        self._repo_path = Path(repo_path).resolve()
        if not owned_paths:
            raise ValueError("owned_paths must be non-empty")
        self._owned_paths = tuple(str(p).replace("\\", "/") for p in owned_paths)
        self._git_runner = git_runner or _default_git_runner
        self._base_commit: str | None = None

    @property
    def repo_path(self) -> Path:
        return self._repo_path

    @property
    def owned_paths(self) -> tuple[str, ...]:
        return self._owned_paths

    @property
    def base_commit(self) -> str | None:
        return self._base_commit

    def begin(self) -> str:
        self._assert_no_unowned_dirty()
        base = self._git(["git", "rev-parse", "HEAD"]).stdout.strip()
        if not base:
            raise RuntimeError("unable to resolve HEAD")
        self._base_commit = base
        return base

    def rollback(self, target_path: Path | str | None = None) -> None:
        if self._base_commit is None:
            raise RuntimeError("begin() must be called before rollback()")
        target = self._repo_path if target_path is None else Path(target_path).resolve()
        if target != self._repo_path:
            raise ValueError(
                f"refusing to touch unrelated worktree: {target} != {self._repo_path}"
            )

        # Restore only owned paths to the recorded base — never `git reset --hard`.
        for path in self._owned_paths:
            if self._is_tracked_at_base(path):
                self._git(["git", "checkout", self._base_commit, "--", path])
                continue
            candidate = self._repo_path / path
            if candidate.is_file():
                candidate.unlink()
            elif candidate.exists():
                raise RuntimeError(
                    f"refusing to recursively delete owned directory: {path}"
                )

    def _assert_no_unowned_dirty(self) -> None:
        owned = set(self._owned_paths)
        dirty = self._list_dirty_paths()
        unowned = sorted(path for path in dirty if path not in owned)
        if unowned:
            raise UnownedDirtyChangesError(
                f"unowned dirty paths block autorun: {unowned}"
            )

    def _list_dirty_paths(self) -> set[str]:
        # porcelain v1: XY PATH or XY ORIG -> PATH
        completed = self._git(["git", "status", "--porcelain"])
        paths: set[str] = set()
        for line in completed.stdout.splitlines():
            if not line.strip():
                continue
            entry = line[3:]
            if " -> " in entry:
                entry = entry.split(" -> ", 1)[1]
            paths.add(entry.replace("\\", "/"))
        return paths

    def _is_tracked_at_base(self, path: str) -> bool:
        if self._base_commit is None:
            return False
        result = subprocess.run(
            ["git", "cat-file", "-e", f"{self._base_commit}:{path}"],
            cwd=self._repo_path,
            capture_output=True,
            text=True,
        )
        return result.returncode == 0

    def _git(self, args: Sequence[str]) -> subprocess.CompletedProcess[str]:
        if len(args) >= 3 and args[0] == "git" and args[1] == "reset" and "--hard" in args:
            raise RuntimeError("git reset --hard is forbidden in autorun isolation")
        return self._git_runner(args, self._repo_path)

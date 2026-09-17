"""Run context fingerprint (design AC16, §4.3)."""

from __future__ import annotations

import hashlib
import subprocess
from collections.abc import Mapping, Sequence
from pathlib import Path
from typing import Any

from scripts.qa.tool.plan import canonical_json_hash

QA_EVIDENCE_PREFIXES: tuple[str, ...] = ("docs/qa/runs/",)


def _normalize(path: str) -> str:
    """Windows 경로 구분자를 / 로 통일한다."""
    return path.replace("\\", "/")


def _is_excluded(path: str) -> bool:
    """QA 런 산출물 경로는 diffHash에서 뺀다."""
    normalized = _normalize(path)
    return any(normalized.startswith(prefix) for prefix in QA_EVIDENCE_PREFIXES)


def _in_scope(path: str, scoped_paths: Sequence[str]) -> bool:
    """이 경로가 이번 실행의 scoped_paths 안에 있는지 본다."""
    normalized = _normalize(path)
    for scoped in scoped_paths:
        scoped_norm = _normalize(scoped)
        if normalized == scoped_norm or normalized.startswith(scoped_norm):
            return True
    return False


def compute_diff_hash(
    *,
    base_revision: str,
    scoped_paths: Sequence[str],
    file_contents: Mapping[str, str],
    untracked_paths: Sequence[str],
) -> str:
    """스코프 안 미커밋 파일 내용의 hash를 만든다. docs/qa/runs 는 제외한다."""
    paths = set(file_contents.keys()) | set(untracked_paths)
    entries: list[dict[str, str]] = []
    for path in sorted(paths, key=_normalize):
        if _is_excluded(path) or not _in_scope(path, scoped_paths):
            continue
        payload = file_contents.get(path, "")
        digest = hashlib.sha256(payload.encode("utf-8")).hexdigest()
        entries.append({"path": _normalize(path), "digest": digest})
    return canonical_json_hash(
        {
            "baseRevision": base_revision,
            "entries": entries,
        }
    )


def _git_text(repo: Path, args: Sequence[str]) -> str:
    """git 한 명령을 UTF-8로 읽어 반환한다. 실패하면 빈 문자열이다."""
    completed = subprocess.run(
        ["git", *args],
        cwd=str(repo),
        capture_output=True,
        check=False,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if completed.returncode != 0:
        return ""
    return completed.stdout


def read_git_worktree(repo: Path, scoped_paths: Sequence[str]) -> dict[str, Any]:
    """실제 git worktree의 스코프 안 미커밋 파일로 diffHash를 만든다."""
    root = repo.resolve()
    base_revision = _git_text(root, ["rev-parse", "HEAD"]).strip()
    porcelain = _git_text(root, ["status", "--porcelain", "-uall"])
    file_contents: dict[str, str] = {}
    untracked_paths: list[str] = []
    for line in porcelain.splitlines():
        if len(line) < 4:
            continue
        path = line[3:].strip().strip('"')
        if " -> " in path:
            path = path.split(" -> ", 1)[1]
        if line.startswith("??"):
            untracked_paths.append(path)
        candidate = root / path
        if candidate.is_file():
            file_contents[path] = candidate.read_text(encoding="utf-8", errors="replace")
        else:
            file_contents[path] = ""
    return {
        "baseRevision": base_revision,
        "diffHash": compute_diff_hash(
            base_revision=base_revision,
            scoped_paths=scoped_paths,
            file_contents=file_contents,
            untracked_paths=untracked_paths,
        ),
        "scopedPaths": [str(item) for item in scoped_paths],
    }


def evaluate_reuse(
    previous: Mapping[str, Any],
    current: Mapping[str, Any],
) -> dict[str, Any]:
    """이전 PASS는 현재 diffHash가 같을 때만 재사용한다."""
    previous_hash = str(previous.get("diffHash") or "")
    current_hash = str(current.get("diffHash") or "")
    if previous_hash != current_hash:
        return {
            "reusable": False,
            "reasonCodes": ["diff-hash-changed"],
        }
    return {"reusable": True, "reasonCodes": []}

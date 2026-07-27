"""Shared path confinement and hashing helpers for wiki_rag."""

from __future__ import annotations

import hashlib
from pathlib import Path


def sha256(path: Path) -> str:
    """Return the SHA-256 hex digest of a file."""

    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def resolve_inside(
    repo_root: Path,
    relative_path: str,
    *,
    path_label: str = "path",
) -> Path:
    """Resolve a repository-relative path and reject escapes outside repo_root."""

    if Path(relative_path).is_absolute():
        raise ValueError(f"{path_label} must be relative: {relative_path}")
    resolved = (repo_root / Path(relative_path)).resolve()
    try:
        resolved.relative_to(repo_root)
    except ValueError as error:
        raise ValueError(
            f"{path_label} escapes repository: {relative_path}"
        ) from error
    return resolved

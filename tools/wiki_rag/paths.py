"""Shared path confinement, hashing, and atomic I/O helpers for wiki_rag."""

from __future__ import annotations

import hashlib
import os
import tempfile
from pathlib import Path

# Text knowledge sources whose working-tree newlines may differ by platform.
# Binary formats (pdf/pptx/hwp) must keep raw-byte digests.
_TEXT_HASH_SUFFIXES = frozenset({".md", ".txt"})


def sha256(path: Path) -> str:
    """Return the SHA-256 hex digest of a file.

    Markdown and plain-text sources are hashed after normalizing newlines to
    LF (``\\r\\n`` / ``\\r`` → ``\\n``) so Windows ``core.autocrlf`` checkouts
    match Linux CI and Git blob bytes. Binary sources are hashed unchanged.
    """

    if path.suffix.casefold() in _TEXT_HASH_SUFFIXES:
        normalized = (
            path.read_bytes().replace(b"\r\n", b"\n").replace(b"\r", b"\n")
        )
        return hashlib.sha256(normalized).hexdigest()

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


def write_text_atomic(output_path: Path, content: str) -> None:
    """Write UTF-8 text atomically via a same-directory temporary file."""

    output_path.parent.mkdir(parents=True, exist_ok=True)
    temporary_path: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="w",
            encoding="utf-8",
            newline="\n",
            dir=output_path.parent,
            prefix=f".{output_path.name}.",
            suffix=".tmp",
            delete=False,
        ) as temporary:
            temporary.write(content)
            temporary.flush()
            os.fsync(temporary.fileno())
            temporary_path = Path(temporary.name)
        os.replace(temporary_path, output_path)
    finally:
        if temporary_path is not None and temporary_path.exists():
            temporary_path.unlink()

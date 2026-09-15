"""Offline package manifest: relative paths and SHA-256."""

from __future__ import annotations

import hashlib
from pathlib import Path
from typing import Any


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            chunk = handle.read(1024 * 1024)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest()


def build_package_manifest(
    root: Path,
    *,
    files: list[str],
    embedding_model: str,
    embedding_dimension: int,
    model_id: str,
    model_sha256: str,
    protocol_version: str = "1",
) -> dict[str, Any]:
    entries: list[dict[str, str]] = []
    for relative in files:
        path = root / relative
        if not path.is_file():
            raise FileNotFoundError(relative)
        entries.append({"path": relative.replace("\\", "/"), "sha256": sha256_file(path)})
    return {
        "protocol_version": protocol_version,
        "embedding": {"id": embedding_model, "dimension": embedding_dimension},
        "model": {"id": model_id, "sha256": model_sha256.lower()},
        "files": entries,
    }


def verify_package_manifest(root: Path, manifest: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    for entry in manifest.get("files") or []:
        if not isinstance(entry, dict):
            errors.append("invalid_file_entry")
            continue
        relative = str(entry.get("path") or "")
        expected = str(entry.get("sha256") or "").lower()
        path = root / relative
        if not path.is_file():
            errors.append(f"missing:{relative}")
            continue
        actual = sha256_file(path)
        if actual != expected:
            errors.append(f"checksum:{relative}")
    return errors

"""Artifact integrity checks (design AC10). Never rewrite source artifacts."""

from __future__ import annotations

import hashlib
from collections.abc import Mapping
from pathlib import Path
from typing import Any

_PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"


def _is_decodable_png(data: bytes) -> bool:
    """바이트가 PNG 시그니처와 IHDR/IEND를 가졌는지 본다. 내용은 수정하지 않는다."""
    return data.startswith(_PNG_SIGNATURE) and b"IHDR" in data and b"IEND" in data


def validate_artifact(run_root: Path, artifact: Mapping[str, Any]) -> dict[str, Any]:
    """스크린샷 한 개를 run root 기준으로 검증한다. 경로 탈출·손상 파일은 PASS가 아니다."""
    artifact_id = str(artifact.get("id") or "")
    relative = str(artifact.get("relativePath") or "")
    reason_codes: list[str] = []
    root = run_root.resolve()

    relative_path = Path(relative)
    if (
        not relative
        or relative_path.is_absolute()
        or ".." in relative_path.parts
    ):
        return {
            "artifactId": artifact_id,
            "verdict": "BLOCKED",
            "reasonCodes": ["path-escape"],
        }

    candidate = (root / relative_path).resolve()
    try:
        candidate.relative_to(root)
    except ValueError:
        return {
            "artifactId": artifact_id,
            "verdict": "BLOCKED",
            "reasonCodes": ["path-escape"],
        }

    if not candidate.is_file():
        return {
            "artifactId": artifact_id,
            "verdict": "BLOCKED",
            "reasonCodes": ["missing-file"],
        }

    data = candidate.read_bytes()
    if len(data) == 0:
        reason_codes.append("empty-file")
    expected_hash = artifact.get("sha256")
    if isinstance(expected_hash, str) and expected_hash:
        actual_hash = hashlib.sha256(data).hexdigest()
        if actual_hash != expected_hash:
            reason_codes.append("hash-mismatch")
    if artifact.get("kind") == "screenshot" and data and not _is_decodable_png(data):
        reason_codes.append("undecodable")

    return {
        "artifactId": artifact_id,
        "verdict": "PASS" if not reason_codes else "BLOCKED",
        "reasonCodes": reason_codes,
    }

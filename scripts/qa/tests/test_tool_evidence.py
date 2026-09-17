"""Independent evidence validator (design AC10)."""

from __future__ import annotations

import hashlib
from pathlib import Path

from scripts.qa.tool.evidence import validate_artifact

# 1x1 RGBA PNG
_MIN_PNG = (
    b"\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01"
    b"\x00\x00\x00\x01\x08\x06\x00\x00\x00\x1f\x15\xc4\x89"
    b"\x00\x00\x00\nIDATx\x9cc\x00\x01\x00\x00\x05\x00\x01"
    b"\r\n-\xb4\x00\x00\x00\x00IEND\xaeB`\x82"
)


def _artifact(relative_path: str, data: bytes, *, artifact_id: str = "shot-1") -> dict[str, str]:
    return {
        "id": artifact_id,
        "relativePath": relative_path,
        "kind": "screenshot",
        "sha256": hashlib.sha256(data).hexdigest(),
        "stepId": "capture",
    }


def test_valid_png_inside_run_root_passes(tmp_path: Path) -> None:
    shot = tmp_path / "attempts" / "a1" / "kitchen.png"
    shot.parent.mkdir(parents=True)
    shot.write_bytes(_MIN_PNG)
    result = validate_artifact(tmp_path, _artifact("attempts/a1/kitchen.png", _MIN_PNG))
    assert result["verdict"] == "PASS"
    assert result["artifactId"] == "shot-1"


def test_missing_file_rejects_pass(tmp_path: Path) -> None:
    result = validate_artifact(tmp_path, _artifact("attempts/a1/missing.png", _MIN_PNG))
    assert result["verdict"] != "PASS"
    assert result["artifactId"] == "shot-1"
    assert "missing-file" in result["reasonCodes"]


def test_zero_byte_file_rejects_pass(tmp_path: Path) -> None:
    path = tmp_path / "empty.png"
    path.write_bytes(b"")
    result = validate_artifact(tmp_path, _artifact("empty.png", b""))
    assert result["verdict"] != "PASS"
    assert "empty-file" in result["reasonCodes"]


def test_corrupt_image_rejects_pass(tmp_path: Path) -> None:
    data = b"not-a-png"
    path = tmp_path / "bad.png"
    path.write_bytes(data)
    result = validate_artifact(tmp_path, _artifact("bad.png", data))
    assert result["verdict"] != "PASS"
    assert "undecodable" in result["reasonCodes"]


def test_hash_mismatch_rejects_pass(tmp_path: Path) -> None:
    path = tmp_path / "kitchen.png"
    path.write_bytes(_MIN_PNG)
    artifact = _artifact("kitchen.png", _MIN_PNG)
    artifact["sha256"] = "0" * 64
    result = validate_artifact(tmp_path, artifact)
    assert result["verdict"] != "PASS"
    assert "hash-mismatch" in result["reasonCodes"]


def test_path_escape_rejects_pass(tmp_path: Path) -> None:
    outside = tmp_path.parent / "escaped.png"
    outside.write_bytes(_MIN_PNG)
    result = validate_artifact(tmp_path, _artifact("../escaped.png", _MIN_PNG))
    assert result["verdict"] != "PASS"
    assert "path-escape" in result["reasonCodes"]

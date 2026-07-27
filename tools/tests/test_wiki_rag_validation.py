from __future__ import annotations

import hashlib
from pathlib import Path

import pytest
import yaml

from wiki_rag.validate import validate_manifest, validate_transcript


def write_source(repo_root: Path, relative_path: str, content: bytes) -> None:
    source_path = repo_root / relative_path
    source_path.parent.mkdir(parents=True, exist_ok=True)
    source_path.write_bytes(content)


def write_manifest(
    repo_root: Path,
    sources: list[dict[str, object]],
) -> Path:
    manifest_path = repo_root / "docs/wiki/_meta/source-manifest.yaml"
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_data = {
        "schema_version": 1,
        "inputs": {
            "roots": ["시나리오"],
            "root_sources": [],
            "technical_sources": [],
            "included_extensions": ["md", "pdf", "hwp"],
            "exclusions": ["tools/**"],
        },
        "sources": sources,
    }
    manifest_path.write_text(
        yaml.safe_dump(manifest_data, allow_unicode=True, sort_keys=False),
        encoding="utf-8",
        newline="\n",
    )
    return manifest_path


def _md_source_record(
    repo_root: Path,
    relative_path: str,
    content: bytes,
    *,
    status: str = "extracted",
    write_transcript: bool = True,
) -> dict[str, object]:
    write_source(repo_root, relative_path, content)
    source_sha256 = hashlib.sha256(content).hexdigest()
    transcript_path = (
        f"docs/wiki/sources/scenario/sample--{source_sha256[:12]}.md"
    )
    record: dict[str, object] = {
        "source_id": f"scenario:{source_sha256[:12]}",
        "source_path": relative_path,
        "source_sha256": source_sha256,
        "source_type": "md",
        "category": "scenario",
        "title": "sample",
        "transcript_path": transcript_path,
        "status": status,
        "rag_eligible": True,
        "canonical_group": "scenario:sample",
    }
    if write_transcript:
        transcript = repo_root / transcript_path
        transcript.parent.mkdir(parents=True, exist_ok=True)
        transcript.write_text(
            "\n".join(
                (
                    "---",
                    f"source_id: {record['source_id']}",
                    f"source_path: {relative_path}",
                    f"source_sha256: {source_sha256}",
                    "source_type: md",
                    "category: scenario",
                    f"status: {status}",
                    "rag_eligible: true",
                    "---",
                    "",
                    content.decode("utf-8"),
                )
            ),
            encoding="utf-8",
            newline="\n",
        )
    return record


def manifest_with_missing_transcript(tmp_path: Path) -> Path:
    content = b"# Scenario body with enough non-whitespace characters."
    source = _md_source_record(
        tmp_path,
        "시나리오/a.md",
        content,
        write_transcript=False,
    )
    drifted_content = b"# Changed original content invalidates stored hash."
    write_source(tmp_path, "시나리오/a.md", drifted_content)
    return write_manifest(tmp_path, [source])


def test_validation_rejects_missing_transcript_and_hash_drift(
    tmp_path: Path,
) -> None:
    manifest_path = manifest_with_missing_transcript(tmp_path)

    report = validate_manifest(manifest_path, tmp_path)

    assert "missing_transcript" in report.error_codes
    assert "source_hash_changed" in report.error_codes
    assert not report.ok


def test_validation_rejects_replacement_character_in_rag_text(
    tmp_path: Path,
) -> None:
    transcript = tmp_path / "bad.md"
    transcript.write_text(
        "---\nrag_eligible: true\n---\n깨진 \ufffd 문자",
        encoding="utf-8",
    )

    report = validate_transcript(transcript)

    assert "unicode_replacement_character" in report.error_codes
    assert not report.ok


def test_hwp_pending_records_do_not_fail_gate_with_owner_skip(
    tmp_path: Path,
) -> None:
    hwp_content = b"HWP binary placeholder"
    write_source(tmp_path, "보고서/weekly.hwp", hwp_content)
    source_sha256 = hashlib.sha256(hwp_content).hexdigest()
    hwp_record = {
        "source_id": f"report:{source_sha256[:12]}",
        "source_path": "보고서/weekly.hwp",
        "source_sha256": source_sha256,
        "source_type": "hwp",
        "category": "report",
        "title": "weekly",
        "transcript_path": (
            f"docs/wiki/sources/report/weekly--{source_sha256[:12]}.md"
        ),
        "status": "pending",
        "rag_eligible": False,
        "canonical_group": "report:weekly",
    }
    md_content = b"# Valid markdown source with sufficient body text."
    md_record = _md_source_record(tmp_path, "docs/architecture.md", md_content)
    manifest_path = write_manifest(tmp_path, [hwp_record, md_record])

    report = validate_manifest(manifest_path, tmp_path)

    assert report.ok
    assert "unconverted_in_scope" not in report.error_codes
    assert "missing_transcript" not in report.error_codes
    hwp_rows = [
        row
        for row in report.rows
        if row.get("source_type") == "hwp"
    ]
    assert len(hwp_rows) == 1
    assert hwp_rows[0].get("skip_reason") == "skipped_by_owner"


def test_validation_rejects_unresolved_internal_link(tmp_path: Path) -> None:
    transcript = tmp_path / "linked.md"
    transcript.write_text(
        "\n".join(
            (
                "---",
                "source_id: technical:abc123",
                "source_path: docs/example.md",
                "source_sha256: abc",
                "source_type: md",
                "category: technical",
                "status: extracted",
                "rag_eligible: true",
                "---",
                "",
                "See [missing](docs/does-not-exist.md) for details.",
            )
        ),
        encoding="utf-8",
        newline="\n",
    )

    report = validate_transcript(transcript, repo_root=tmp_path)

    assert "unresolved_internal_link" in report.error_codes
    assert not report.ok


def test_validation_rejects_front_matter_source_id_drift(
    tmp_path: Path,
) -> None:
    content = b"# Scenario body with enough non-whitespace characters."
    source = _md_source_record(tmp_path, "시나리오/a.md", content)
    transcript_path = tmp_path / str(source["transcript_path"])
    transcript_text = transcript_path.read_text(encoding="utf-8")
    transcript_path.write_text(
        transcript_text.replace(
            f"source_id: {source['source_id']}",
            "source_id: scenario:deadbeef0000",
        ),
        encoding="utf-8",
        newline="\n",
    )
    manifest_path = write_manifest(tmp_path, [source])

    report = validate_manifest(manifest_path, tmp_path)

    assert "front_matter_drift" in report.error_codes
    assert not report.ok


def test_validation_rejects_extracted_status_with_insufficient_text(
    tmp_path: Path,
) -> None:
    transcript = tmp_path / "short.md"
    transcript.write_text(
        "\n".join(
            (
                "---",
                "source_id: technical:abc123",
                "source_path: docs/short.md",
                "source_sha256: abc",
                "source_type: md",
                "category: technical",
                "status: extracted",
                "rag_eligible: true",
                "---",
                "",
                "tiny",
            )
        ),
        encoding="utf-8",
        newline="\n",
    )

    report = validate_transcript(transcript)

    assert "insufficient_text" in report.error_codes
    assert not report.ok

from __future__ import annotations

import hashlib
from dataclasses import FrozenInstanceError
from pathlib import Path

import pytest

from wiki_rag.inventory import (
    InventoryError,
    discover_sources,
    validate_unique_records,
    write_manifest,
)


def write_source(repo_root: Path, relative_path: str, content: bytes) -> Path:
    source_path = repo_root / relative_path
    source_path.parent.mkdir(parents=True, exist_ok=True)
    source_path.write_bytes(content)
    return source_path


def test_inventory_includes_each_in_scope_original_once(tmp_path: Path) -> None:
    write_source(tmp_path, "시나리오/a.pdf", b"scenario")
    write_source(tmp_path, "기획서/b.pptx", b"planning")
    write_source(tmp_path, "보고서/c.hwp", b"report")
    write_source(tmp_path, "기획서/ignore.exe", b"not knowledge")

    records = discover_sources(
        tmp_path,
        roots=["시나리오", "기획서", "보고서"],
        root_sources=[],
        technical_sources=[],
    )

    assert {record.source_path for record in records} == {
        "시나리오/a.pdf",
        "기획서/b.pptx",
        "보고서/c.hwp",
    }
    assert all(record.source_sha256 for record in records)
    assert [record.source_path for record in records] == sorted(
        record.source_path for record in records
    )


def test_inventory_excludes_generated_and_tool_files(tmp_path: Path) -> None:
    write_source(tmp_path, "docs/architecture.md", b"architecture")
    write_source(tmp_path, "docs/wiki/generated.md", b"generated")
    write_source(tmp_path, "docs/scenario_extracts/old.txt", b"old extract")
    write_source(tmp_path, "docs/superpowers/plans/tooling.md", b"tool plan")
    write_source(tmp_path, "tools/requirements.txt", b"dependency")
    write_source(tmp_path, "tools/fixture.md", b"fixture")

    records = discover_sources(
        tmp_path,
        roots=["docs", "tools"],
        root_sources=[],
        technical_sources=["docs/architecture.md"],
    )

    assert [record.source_path for record in records] == [
        "docs/architecture.md"
    ]


def test_inventory_hashes_original_bytes_and_builds_stable_ids(
    tmp_path: Path,
) -> None:
    content = "원본 bytes\r\n".encode("utf-8")
    write_source(tmp_path, "시나리오/원본 문서.md", content)

    first = discover_sources(
        tmp_path,
        roots=["시나리오"],
        root_sources=[],
        technical_sources=[],
    )
    second = discover_sources(
        tmp_path,
        roots=["시나리오"],
        root_sources=[],
        technical_sources=[],
    )

    # Text sources hash LF-normalized bytes so Windows CRLF matches CI/LF.
    expected_hash = hashlib.sha256("원본 bytes\n".encode("utf-8")).hexdigest()
    assert first == second
    assert first[0].source_sha256 == expected_hash
    assert first[0].source_id == f"scenario:{expected_hash[:12]}"
    assert first[0].transcript_path == (
        f"docs/wiki/sources/scenario/원본-문서--{expected_hash[:12]}.md"
    )


def test_markdown_crlf_and_lf_share_the_same_source_hash(tmp_path: Path) -> None:
    from wiki_rag.paths import sha256 as path_sha256

    crlf_path = write_source(tmp_path, "시나리오/crlf.md", b"line-a\r\nline-b\r\n")
    lf_path = write_source(tmp_path, "시나리오/lf.md", b"line-a\nline-b\n")
    expected = hashlib.sha256(b"line-a\nline-b\n").hexdigest()

    assert path_sha256(crlf_path) == expected
    assert path_sha256(lf_path) == expected

    # Inventory uses the same helper; only one twin can exist (shared source_id).
    lf_path.unlink()
    records = discover_sources(
        tmp_path,
        roots=["시나리오"],
        root_sources=[],
        technical_sources=[],
    )
    assert len(records) == 1
    assert records[0].source_sha256 == expected


def test_binary_pdf_hash_keeps_raw_bytes_including_crlf(tmp_path: Path) -> None:
    content = b"%PDF-1.4\r\nbinary\r\n"
    write_source(tmp_path, "시나리오/raw.pdf", content)

    record = discover_sources(
        tmp_path,
        roots=["시나리오"],
        root_sources=[],
        technical_sources=[],
    )[0]

    assert record.source_sha256 == hashlib.sha256(content).hexdigest()


def test_source_records_are_frozen(tmp_path: Path) -> None:
    write_source(tmp_path, "시나리오/a.pdf", b"scenario")
    record = discover_sources(
        tmp_path,
        roots=["시나리오"],
        root_sources=[],
        technical_sources=[],
    )[0]

    with pytest.raises(FrozenInstanceError):
        record.title = "changed"  # type: ignore[misc]


def test_duplicate_source_ids_fail_the_scope_gate(tmp_path: Path) -> None:
    write_source(tmp_path, "기획서/a.pdf", b"same bytes")
    write_source(tmp_path, "기획서/b.pdf", b"same bytes")

    with pytest.raises(InventoryError, match="duplicate source_id"):
        discover_sources(
            tmp_path,
            roots=["기획서"],
            root_sources=[],
            technical_sources=[],
        )


def test_duplicate_transcript_paths_fail_the_scope_gate(tmp_path: Path) -> None:
    write_source(tmp_path, "시나리오/A B.pdf", b"first")
    write_source(tmp_path, "시나리오/A-B.pdf", b"second")
    records = discover_sources(
        tmp_path,
        roots=["시나리오"],
        root_sources=[],
        technical_sources=[],
    )
    duplicate = records[0].__class__(
        **{
            **records[1].to_dict(),
            "transcript_path": records[0].transcript_path,
        }
    )

    with pytest.raises(InventoryError, match="duplicate transcript_path"):
        validate_unique_records([records[0], duplicate])


def test_pdf_and_pptx_pair_share_canonical_group(tmp_path: Path) -> None:
    write_source(tmp_path, "기획서/Room Plan.pptx", b"slides")
    write_source(tmp_path, "기획서/Room Plan.pptx.pdf", b"export")

    records = discover_sources(
        tmp_path,
        roots=["기획서"],
        root_sources=[],
        technical_sources=[],
    )

    assert len(records) == 2
    assert records[0].canonical_group == records[1].canonical_group
    assert records[0].source_id != records[1].source_id


def test_root_project_reports_are_discovered_and_rag_ineligible(
    tmp_path: Path,
) -> None:
    write_source(tmp_path, "미니게임_구현_리포트.md", b"mini-game report")
    write_source(tmp_path, "report.pdf", b"capstone report")

    records = discover_sources(
        tmp_path,
        roots=[],
        root_sources=["미니게임_구현_리포트.md", "report.pdf"],
        technical_sources=[],
    )

    assert {record.category for record in records} == {"report"}
    assert all(record.rag_eligible is False for record in records)


def test_manifest_serialization_is_deterministic(tmp_path: Path) -> None:
    write_source(tmp_path, "시나리오/story.md", b"story")
    records = discover_sources(
        tmp_path,
        roots=["시나리오"],
        root_sources=[],
        technical_sources=[],
    )
    output_path = tmp_path / "docs/wiki/_meta/source-manifest.yaml"

    write_manifest(
        output_path,
        records,
        roots=["시나리오"],
        root_sources=[],
        technical_sources=[],
    )
    first = output_path.read_bytes()
    write_manifest(
        output_path,
        records,
        roots=["시나리오"],
        root_sources=[],
        technical_sources=[],
    )

    assert output_path.read_bytes() == first
    manifest_text = first.decode("utf-8")
    assert manifest_text.startswith("schema_version: 1\n")
    assert "roots:\n    - 시나리오\n" in manifest_text
    assert "exclusions:\n" in manifest_text
    assert f"source_id: {records[0].source_id}\n" in manifest_text
    assert f"source_sha256: {records[0].source_sha256}\n" in manifest_text

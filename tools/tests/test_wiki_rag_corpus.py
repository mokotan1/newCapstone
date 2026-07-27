from __future__ import annotations

import hashlib
from pathlib import Path

import pytest
import yaml
from wiki_rag.build_rag_corpus import (
    CorpusBuildError,
    build_rag_corpus,
    rag_output_filename,
)


def write_source(repo_root: Path, relative_path: str, content: bytes) -> None:
    source_path = repo_root / relative_path
    source_path.parent.mkdir(parents=True, exist_ok=True)
    source_path.write_bytes(content)


def write_transcript(
    repo_root: Path,
    *,
    record: dict[str, object],
    body: str,
) -> None:
    transcript_path = repo_root / str(record["transcript_path"])
    transcript_path.parent.mkdir(parents=True, exist_ok=True)
    transcript_path.write_text(
        "\n".join(
            (
                "---",
                f"source_id: {record['source_id']}",
                f"source_path: {record['source_path']}",
                f"source_sha256: {record['source_sha256']}",
                f"source_type: {record['source_type']}",
                f"category: {record['category']}",
                f"status: {record['status']}",
                f"rag_eligible: {'true' if record['rag_eligible'] else 'false'}",
                "---",
                "",
                body,
            )
        ),
        encoding="utf-8",
        newline="\n",
    )


def _source_record(
    *,
    source_id: str,
    source_path: str,
    category: str,
    title: str,
    status: str = "extracted",
    source_type: str = "md",
    rag_eligible: bool = True,
    canonical_group: str | None = None,
    body: str,
    repo_root: Path,
) -> dict[str, object]:
    content = body.encode("utf-8")
    write_source(repo_root, source_path, content)
    source_sha256 = hashlib.sha256(content).hexdigest()
    slug = title.lower().replace(" ", "-")
    record: dict[str, object] = {
        "source_id": source_id,
        "source_path": source_path,
        "source_sha256": source_sha256,
        "source_type": source_type,
        "category": category,
        "title": title,
        "transcript_path": (
            f"docs/wiki/sources/{category}/{slug}--{source_sha256[:12]}.md"
        ),
        "status": status,
        "rag_eligible": rag_eligible,
        "canonical_group": canonical_group or f"{category}:{slug}",
    }
    write_transcript(repo_root, record=record, body=body)
    return record


def write_manifest(repo_root: Path, sources: list[dict[str, object]]) -> Path:
    manifest_path = repo_root / "source-manifest.yaml"
    manifest_data = {
        "schema_version": 1,
        "inputs": {
            "roots": ["시나리오", "보고서"],
            "root_sources": [],
            "technical_sources": [],
            "included_extensions": ["md", "pdf", "pptx", "hwp"],
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


def sample_manifest_with_report(tmp_path: Path) -> Path:
    scenario_body = (
        "# Scenario body with enough non-whitespace characters for RAG inclusion."
    )
    report_body = (
        "# Weekly report body with enough non-whitespace characters for transcript."
    )
    scenario = _source_record(
        source_id="scenario:abc123",
        source_path="시나리오/story.md",
        category="scenario",
        title="story",
        body=scenario_body,
        repo_root=tmp_path,
    )
    report = _source_record(
        source_id="report:deadbeef0000",
        source_path="보고서/weekly.md",
        category="report",
        title="weekly",
        rag_eligible=False,
        body=report_body,
        repo_root=tmp_path,
    )
    return write_manifest(tmp_path, [scenario, report])


def manifest_with_empty_rag_source(tmp_path: Path) -> Path:
    empty = _source_record(
        source_id="scenario:empty001",
        source_path="시나리오/empty.md",
        category="scenario",
        title="empty",
        body="   ",
        repo_root=tmp_path,
    )
    return write_manifest(tmp_path, [empty])


def test_rag_corpus_excludes_reports_and_keeps_source_metadata(
    tmp_path: Path,
) -> None:
    written = build_rag_corpus(
        sample_manifest_with_report(tmp_path),
        output_dir=tmp_path / "rag",
        repo_root=tmp_path,
    )

    assert [path.name for path in written] == [rag_output_filename("scenario:abc123")]
    text = written[0].read_text(encoding="utf-8")
    assert "source_id: scenario:abc123" in text
    assert "source_path:" in text


def test_rag_corpus_rejects_unreviewed_or_empty_extract(tmp_path: Path) -> None:
    with pytest.raises(CorpusBuildError, match="meaningful text"):
        build_rag_corpus(
            manifest_with_empty_rag_source(tmp_path),
            output_dir=tmp_path / "rag",
            repo_root=tmp_path,
        )


def test_rag_corpus_excludes_pending_hwp(tmp_path: Path) -> None:
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
    scenario = _source_record(
        source_id="scenario:included01",
        source_path="시나리오/valid.md",
        category="scenario",
        title="valid",
        body="# Valid scenario body with enough non-whitespace characters.",
        repo_root=tmp_path,
    )
    manifest_path = write_manifest(tmp_path, [hwp_record, scenario])

    written = build_rag_corpus(
        manifest_path,
        output_dir=tmp_path / "rag",
        repo_root=tmp_path,
    )

    assert len(written) == 1
    assert written[0].name == rag_output_filename("scenario:included01")
    corpus_text = "\n".join(path.read_text(encoding="utf-8") for path in written)
    assert "weekly.hwp" not in corpus_text


def test_rag_corpus_deduplicates_high_similarity_pdf_pptx_pair(
    tmp_path: Path,
) -> None:
    shared_body = (
        "# Shared planning content with enough non-whitespace characters for RAG."
    )
    pptx_content = shared_body.encode("utf-8")
    pdf_content = shared_body.encode("utf-8")
    write_source(tmp_path, "기획서/Room Plan.pptx", pptx_content)
    write_source(tmp_path, "기획서/Room Plan.pptx.pdf", pdf_content)
    pptx_sha = hashlib.sha256(pptx_content).hexdigest()
    pdf_sha = hashlib.sha256(pdf_content).hexdigest()
    canonical_group = "planning:room-plan"
    pptx_record = {
        "source_id": f"planning:{pptx_sha[:12]}",
        "source_path": "기획서/Room Plan.pptx",
        "source_sha256": pptx_sha,
        "source_type": "pptx",
        "category": "planning",
        "title": "Room Plan",
        "transcript_path": f"docs/wiki/sources/planning/room-plan--{pptx_sha[:12]}.md",
        "status": "extracted",
        "rag_eligible": True,
        "canonical_group": canonical_group,
    }
    pdf_record = {
        "source_id": f"planning:{pdf_sha[:12]}",
        "source_path": "기획서/Room Plan.pptx.pdf",
        "source_sha256": pdf_sha,
        "source_type": "pdf",
        "category": "planning",
        "title": "Room Plan",
        "transcript_path": f"docs/wiki/sources/planning/room-plan--{pdf_sha[:12]}.md",
        "status": "extracted",
        "rag_eligible": True,
        "canonical_group": canonical_group,
    }
    write_transcript(tmp_path, record=pptx_record, body=shared_body)
    write_transcript(tmp_path, record=pdf_record, body=shared_body)
    manifest_path = write_manifest(tmp_path, [pptx_record, pdf_record])

    written = build_rag_corpus(
        manifest_path,
        output_dir=tmp_path / "rag",
        repo_root=tmp_path,
    )

    assert len(written) == 1
    text = written[0].read_text(encoding="utf-8")
    assert str(pptx_record["source_id"]) in text
    assert "related_source_ids:" in text
    assert str(pdf_record["source_id"]) in text

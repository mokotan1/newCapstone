from __future__ import annotations

import hashlib
from pathlib import Path
from types import SimpleNamespace

import pytest
import yaml

from wiki_rag.extract import convert_manifest, main


def write_source(repo_root: Path, relative_path: str, content: bytes) -> None:
    source_path = repo_root / relative_path
    source_path.parent.mkdir(parents=True, exist_ok=True)
    source_path.write_bytes(content)


def write_manifest(repo_root: Path, sources: list[dict[str, object]]) -> Path:
    manifest_path = repo_root / "docs/wiki/_meta/source-manifest.yaml"
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_data = {
        "schema_version": 1,
        "inputs": {
            "roots": ["시나리오"],
            "root_sources": [],
            "technical_sources": [],
            "included_extensions": ["pdf"],
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


def _pdf_source_record(
    repo_root: Path,
    relative_path: str,
    content: bytes,
) -> dict[str, object]:
    write_source(repo_root, relative_path, content)
    source_sha256 = hashlib.sha256(content).hexdigest()
    return {
        "source_id": f"scenario:{source_sha256[:12]}",
        "source_path": relative_path,
        "source_sha256": source_sha256,
        "source_type": "pdf",
        "category": "scenario",
        "title": "sample",
        "transcript_path": (
            f"docs/wiki/sources/scenario/sample--{source_sha256[:12]}.md"
        ),
        "status": "pending",
        "rag_eligible": True,
        "canonical_group": "scenario:sample",
    }


@pytest.fixture
def mock_pdf_reader(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(
        "wiki_rag.extractors.pdf.PdfReader",
        lambda _: SimpleNamespace(
            pages=[
                SimpleNamespace(extract_text=lambda: "본문 텍스트"),
            ]
        ),
    )


def test_convert_manifest_updates_status_warnings_and_transcript(
    tmp_path: Path,
    mock_pdf_reader: None,
) -> None:
    content = b"%PDF-1.4\n% integration fixture"
    source = _pdf_source_record(tmp_path, "시나리오/sample.pdf", content)
    manifest_path = write_manifest(tmp_path, [source])
    transcript_path = tmp_path / str(source["transcript_path"])

    counts = convert_manifest(manifest_path, tmp_path, frozenset({"pdf"}))

    assert counts == {"extracted": 1, "needs_review": 0}
    assert transcript_path.is_file()
    transcript = transcript_path.read_text(encoding="utf-8")
    assert "status: extracted\n" in transcript
    assert "본문 텍스트" in transcript

    updated = yaml.safe_load(manifest_path.read_text(encoding="utf-8"))
    record = updated["sources"][0]
    assert record["status"] == "extracted"
    assert record["warnings"] == []


def test_convert_manifest_marks_needs_review_when_warnings_present(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    content = b"%PDF-1.4\n% warning fixture"
    source = _pdf_source_record(tmp_path, "시나리오/warn.pdf", content)
    manifest_path = write_manifest(tmp_path, [source])
    transcript_path = tmp_path / str(source["transcript_path"])
    monkeypatch.setattr(
        "wiki_rag.extractors.pdf.PdfReader",
        lambda _: SimpleNamespace(
            pages=[
                SimpleNamespace(extract_text=lambda: ""),
            ]
        ),
    )

    counts = convert_manifest(manifest_path, tmp_path, frozenset({"pdf"}))

    assert counts == {"extracted": 0, "needs_review": 1}
    assert transcript_path.is_file()
    transcript = transcript_path.read_text(encoding="utf-8")
    assert "status: needs_review\n" in transcript

    updated = yaml.safe_load(manifest_path.read_text(encoding="utf-8"))
    record = updated["sources"][0]
    assert record["status"] == "needs_review"
    assert record["warnings"] == ["pdf_text_empty_page:1"]


def test_extract_cli_resolves_paths_from_repo_root(
    tmp_path: Path,
    mock_pdf_reader: None,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    content = b"%PDF-1.4\n% cli fixture"
    source = _pdf_source_record(tmp_path, "시나리오/cli.pdf", content)
    write_manifest(tmp_path, [source])
    transcript_path = tmp_path / str(source["transcript_path"])
    workdir = tmp_path / "tools"
    workdir.mkdir()
    monkeypatch.chdir(workdir)

    exit_code = main(
        [
            "--repo-root",
            str(tmp_path),
            "--manifest",
            "docs/wiki/_meta/source-manifest.yaml",
            "--types",
            "pdf",
        ]
    )

    assert exit_code == 0
    assert transcript_path.is_file()

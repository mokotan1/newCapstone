from __future__ import annotations

from pathlib import Path
from types import SimpleNamespace

import pytest
from wiki_rag.extractors.pdf import extract_pdf
from wiki_rag.models import SourceRecord
from wiki_rag.normalize import normalize_transcript


@pytest.fixture
def pdf_fixture(tmp_path: Path) -> Path:
    fixture = tmp_path / "한글 원본.pdf"
    fixture.write_bytes(b"%PDF-1.4\n% test fixture")
    return fixture


def test_pdf_markdown_has_page_markers_and_source_metadata(
    pdf_fixture: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    pages = [
        SimpleNamespace(extract_text=lambda: "첫 번째 본문\r\n둘째 줄"),
        SimpleNamespace(extract_text=lambda: ""),
    ]
    monkeypatch.setattr(
        "wiki_rag.extractors.pdf.PdfReader",
        lambda _: SimpleNamespace(pages=pages),
    )

    result = extract_pdf(pdf_fixture)

    assert "<!-- page: 1 -->" in result.markdown
    assert "<!-- page: 2 -->" in result.markdown
    assert "첫 번째 본문\n둘째 줄" in result.markdown
    assert result.page_or_slide_count == 2
    assert result.warnings == ["pdf_text_empty_page:2"]


def test_normalized_pdf_has_provenance_front_matter(
    pdf_fixture: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(
        "wiki_rag.extractors.pdf.PdfReader",
        lambda _: SimpleNamespace(
            pages=[
                SimpleNamespace(
                    extract_text=lambda: "본문 \t계속  \r\n둘째 줄\t"
                )
            ]
        ),
    )
    record = SourceRecord(
        source_id="scenario:abc123",
        source_path="시나리오/한글: 원본.pdf",
        source_sha256="abc123",
        source_type="pdf",
        category="scenario",
        title="한글 원본",
        transcript_path="docs/wiki/sources/scenario/한글-원본.md",
        status="pending",
        rag_eligible=True,
        canonical_group="scenario:한글-원본",
    )

    transcript = normalize_transcript(record, extract_pdf(pdf_fixture))

    assert transcript.startswith("---\n")
    assert 'source_path: "시나리오/한글: 원본.pdf"\n' in transcript
    assert "source_sha256: abc123\n" in transcript
    assert "status: extracted\n" in transcript
    assert "rag_eligible: true\n" in transcript
    assert "---\n\n<!-- page: 1 -->" in transcript
    assert "<!-- page: 1 -->" in transcript
    assert all(line == line.rstrip() for line in transcript.splitlines())
    assert "\t" not in transcript

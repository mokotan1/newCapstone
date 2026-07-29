"""Extract text from PDFs without pretending OCR occurred."""

from __future__ import annotations

from pathlib import Path

from pypdf import PdfReader

from . import ExtractionResult


def _normalize_line_endings(value: str) -> str:
    return value.replace("\r\n", "\n").replace("\r", "\n")


def extract_pdf(source_path: Path) -> ExtractionResult:
    """Extract each PDF page with stable provenance markers."""

    reader = PdfReader(source_path)
    sections: list[str] = []
    warnings: list[str] = []

    for page_number, page in enumerate(reader.pages, start=1):
        text = _normalize_line_endings(page.extract_text() or "").strip()
        sections.append(f"<!-- page: {page_number} -->")
        if text:
            sections.append(text)
        else:
            warnings.append(f"pdf_text_empty_page:{page_number}")
        sections.append("")

    return ExtractionResult(
        markdown="\n".join(sections).rstrip() + "\n",
        page_or_slide_count=len(reader.pages),
        warnings=warnings,
    )

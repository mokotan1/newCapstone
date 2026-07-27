"""Copy plain-text originals through the normalization pipeline."""

from __future__ import annotations

from pathlib import Path

from . import ExtractionResult


def _normalize_line_endings(value: str) -> str:
    return value.replace("\r\n", "\n").replace("\r", "\n")


def extract_txt(source_path: Path) -> ExtractionResult:
    """Read a text original and return normalized body content."""

    raw_bytes = source_path.read_bytes()
    try:
        content = raw_bytes.decode("utf-8-sig")
    except UnicodeDecodeError as error:
        raise ValueError(f"txt_invalid_utf8:{source_path}") from error

    body = _normalize_line_endings(content).strip()
    warnings: list[str] = []
    if not body:
        warnings.append("txt_text_empty")
    elif "\ufffd" in body:
        warnings.append("unicode_replacement_character")

    return ExtractionResult(
        markdown=body + ("\n" if body else ""),
        page_or_slide_count=0,
        warnings=warnings,
    )

"""Copy Markdown originals through the normalization pipeline."""

from __future__ import annotations

import re
from pathlib import Path

from . import ExtractionResult

_FRONT_MATTER_PATTERN = re.compile(
    r"\A---\r?\n.*?\r?\n---\r?\n",
    flags=re.DOTALL,
)


def _strip_existing_front_matter(content: str) -> str:
    return _FRONT_MATTER_PATTERN.sub("", content, count=1)


def _normalize_line_endings(value: str) -> str:
    return value.replace("\r\n", "\n").replace("\r", "\n")


def extract_md(source_path: Path) -> ExtractionResult:
    """Read a Markdown original and return its body for transcript normalization."""

    raw_bytes = source_path.read_bytes()
    try:
        content = raw_bytes.decode("utf-8-sig")
    except UnicodeDecodeError as error:
        raise ValueError(f"md_invalid_utf8:{source_path}") from error

    body = _strip_existing_front_matter(_normalize_line_endings(content)).strip()
    warnings: list[str] = []
    if not body:
        warnings.append("md_text_empty")
    elif "\ufffd" in body:
        warnings.append("unicode_replacement_character")

    return ExtractionResult(
        markdown=body + ("\n" if body else ""),
        page_or_slide_count=0,
        warnings=warnings,
    )

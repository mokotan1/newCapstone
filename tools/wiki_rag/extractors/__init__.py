"""Format-specific source extractors."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class ExtractionResult:
    """Markdown extraction plus quality signals for one source."""

    markdown: str
    page_or_slide_count: int
    warnings: list[str]

"""Normalize extracted Markdown and prepend provenance metadata."""

from __future__ import annotations

import json
import re
from typing import Any

from .extractors import ExtractionResult
from .models import SourceRecord

_HEADING_WITHOUT_SPACE = re.compile(r"^(#{1,6})([^\s#])", flags=re.MULTILINE)
_YAML_UNSAFE_PREFIXES = frozenset("-?:,[]{}#&*!|>'\"%@`")
_YAML_RESERVED = frozenset(
    {"null", "true", "false", "yes", "no", "on", "off", "~"}
)


def _yaml_scalar(value: Any) -> str:
    if isinstance(value, bool):
        return "true" if value else "false"
    if not isinstance(value, str):
        raise TypeError(f"unsupported transcript scalar: {type(value).__name__}")

    lowered = value.casefold()
    unsafe = (
        not value
        or value[0] in _YAML_UNSAFE_PREFIXES
        or lowered in _YAML_RESERVED
        or ": " in value
        or " #" in value
        or any(character in value for character in "\r\n\t")
    )
    return json.dumps(value, ensure_ascii=False) if unsafe else value


def _normalize_markdown(value: str) -> str:
    normalized = value.replace("\r\n", "\n").replace("\r", "\n")
    normalized = normalized.replace("\t", "    ")
    normalized = "\n".join(line.rstrip() for line in normalized.split("\n"))
    normalized = _HEADING_WITHOUT_SPACE.sub(r"\1 \2", normalized)
    return normalized.strip()


def normalize_transcript(
    record: SourceRecord,
    result: ExtractionResult,
) -> str:
    """Return deterministic Markdown with source provenance front matter."""

    status = "needs_review" if result.warnings else "extracted"
    metadata = (
        ("source_id", record.source_id),
        ("source_path", record.source_path),
        ("source_sha256", record.source_sha256),
        ("source_type", record.source_type),
        ("category", record.category),
        ("status", status),
        ("rag_eligible", record.rag_eligible),
    )
    front_matter = ["---"]
    front_matter.extend(
        f"{key}: {_yaml_scalar(value)}" for key, value in metadata
    )
    front_matter.extend(("---", ""))

    body = _normalize_markdown(result.markdown)
    return "\n".join(front_matter) + "\n" + body + ("\n" if body else "")

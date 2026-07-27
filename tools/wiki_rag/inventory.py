"""Discover project knowledge sources and write their authoritative manifest."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
import unicodedata
from collections.abc import Iterable, Sequence
from pathlib import Path
from typing import Any

if __package__ in {None, ""}:
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
    from wiki_rag.models import SourceRecord
else:
    from .models import SourceRecord

CATEGORY_BY_ROOT = {
    "시나리오": "scenario",
    "기획서": "planning",
    "보고서": "report",
    "docs": "technical",
}
DEFAULT_RAG_ELIGIBILITY = {
    "scenario": True,
    "planning": True,
    "report": False,
    "technical": True,
}

DEFAULT_SOURCE_ROOTS = tuple(CATEGORY_BY_ROOT)
ROOT_REPORT_SOURCES = (
    "미니게임_구현_리포트.md",
    "report.pdf",
)
TECHNICAL_SOURCE_ALLOWLIST = (
    "docs/architecture.md",
    "docs/fungus-migration-audit.md",
    "docs/fungus-room-migration-plan.md",
    "docs/glass-choice-menu-usage.md",
    "docs/play-log-analysis.md",
    "docs/play-log-pipeline.md",
    "docs/play-log-sheets-upload.md",
    "docs/qa/2026-07-14-regression-playtest.md",
    "docs/quest-tracker-manual-verification.md",
    "docs/security/llm-abuse-defense-plan.md",
    "docs/security/llm-defense-play-test-guide.md",
)
SUPPORTED_SOURCE_TYPES = frozenset({"hwp", "md", "pdf", "pptx", "txt"})
MANIFEST_EXCLUSIONS = (
    "docs/wiki/** (generated wiki, manifests, transcripts, and RAG output)",
    "docs/superpowers/** (agent plans and specifications)",
    "docs/scenario_extracts/** (previous generated extraction references)",
    "docs/generated/** (generated outputs)",
    "tools/** (dependencies, fixtures, tests, and tooling)",
    "disputatio/** (Unity project and vendor assets)",
    "unlisted docs/** (technical sources require explicit allowlisting)",
)

_NON_ALNUM_PATTERN = re.compile(r"[^\w]+", flags=re.UNICODE)
_KNOWN_SUFFIXES = frozenset(f".{item}" for item in SUPPORTED_SOURCE_TYPES)
_YAML_UNSAFE_PREFIXES = frozenset("-?:,[]{}#&*!|>'\"%@`")
_YAML_RESERVED = frozenset(
    {"null", "true", "false", "yes", "no", "on", "off", "~"}
)


class InventoryError(ValueError):
    """Raised when discovered records violate manifest invariants."""


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _strip_known_suffixes(filename: str) -> str:
    name = filename
    while Path(name).suffix.casefold() in _KNOWN_SUFFIXES:
        name = Path(name).stem
    return name


def _normalize_stem(filename: str) -> str:
    stem = unicodedata.normalize("NFKC", _strip_known_suffixes(filename))
    normalized = _NON_ALNUM_PATTERN.sub("-", stem.casefold()).strip("-_")
    return normalized or "source"


def _build_record(path: Path, repo_root: Path, category: str) -> SourceRecord:
    relative_path = path.relative_to(repo_root).as_posix()
    source_hash = _sha256(path)
    hash_prefix = source_hash[:12]
    normalized_stem = _normalize_stem(path.name)
    source_type = path.suffix.lstrip(".").casefold()
    return SourceRecord(
        source_id=f"{category}:{hash_prefix}",
        source_path=relative_path,
        source_sha256=source_hash,
        source_type=source_type,
        category=category,
        title=_strip_known_suffixes(path.name),
        transcript_path=(
            f"docs/wiki/sources/{category}/"
            f"{normalized_stem}--{hash_prefix}.md"
        ),
        status="pending",
        rag_eligible=DEFAULT_RAG_ELIGIBILITY[category],
        canonical_group=f"{category}:{normalized_stem}",
    )


def _iter_root_sources(
    repo_root: Path,
    root_name: str,
    technical_sources: frozenset[str],
) -> Iterable[tuple[Path, str]]:
    category = CATEGORY_BY_ROOT.get(root_name)
    root_path = repo_root / root_name
    if category is None or not root_path.is_dir():
        return

    for path in root_path.rglob("*"):
        if not path.is_file():
            continue
        source_type = path.suffix.lstrip(".").casefold()
        if source_type not in SUPPORTED_SOURCE_TYPES:
            continue
        relative_path = path.relative_to(repo_root).as_posix()
        if root_name == "docs" and relative_path not in technical_sources:
            continue
        yield path, category


def validate_unique_records(records: Sequence[SourceRecord]) -> None:
    """Reject ambiguous IDs or output paths before writing a manifest."""

    for field_name in ("source_id", "transcript_path"):
        seen: set[str] = set()
        duplicates: set[str] = set()
        for record in records:
            value = str(getattr(record, field_name))
            if value in seen:
                duplicates.add(value)
            seen.add(value)
        if duplicates:
            joined = ", ".join(sorted(duplicates))
            raise InventoryError(f"duplicate {field_name}: {joined}")


def discover_sources(
    repo_root: Path,
    roots: Sequence[str] | None = None,
    *,
    root_sources: Sequence[str] | None = None,
    technical_sources: Sequence[str] | None = None,
) -> list[SourceRecord]:
    """Discover allowed originals and return deterministic manifest records."""

    resolved_root = repo_root.resolve()
    selected_roots = tuple(roots) if roots is not None else DEFAULT_SOURCE_ROOTS
    selected_root_sources = (
        tuple(root_sources)
        if root_sources is not None
        else ROOT_REPORT_SOURCES
    )
    selected_technical_sources = frozenset(
        technical_sources
        if technical_sources is not None
        else TECHNICAL_SOURCE_ALLOWLIST
    )

    candidates: dict[str, tuple[Path, str]] = {}
    for root_name in selected_roots:
        for path, category in _iter_root_sources(
            resolved_root,
            root_name,
            selected_technical_sources,
        ):
            candidates[path.relative_to(resolved_root).as_posix()] = (
                path,
                category,
            )

    for relative_source in selected_root_sources:
        path = resolved_root / Path(relative_source)
        source_type = path.suffix.lstrip(".").casefold()
        if path.is_file() and source_type in SUPPORTED_SOURCE_TYPES:
            relative_path = path.relative_to(resolved_root).as_posix()
            candidates[relative_path] = (path, "report")

    records = [
        _build_record(path, resolved_root, category)
        for _, (path, category) in sorted(candidates.items())
    ]
    validate_unique_records(records)
    return records


def _yaml_scalar(value: Any) -> str:
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, int):
        return str(value)
    if not isinstance(value, str):
        raise TypeError(f"unsupported manifest scalar: {type(value).__name__}")
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


def _append_list(lines: list[str], key: str, values: Sequence[str]) -> None:
    lines.append(f"  {key}:")
    for value in values:
        lines.append(f"    - {_yaml_scalar(value)}")


def _render_manifest(
    records: Sequence[SourceRecord],
    *,
    roots: Sequence[str],
    root_sources: Sequence[str],
    technical_sources: Sequence[str],
) -> str:
    lines = ["schema_version: 1", "inputs:"]
    _append_list(lines, "roots", roots)
    _append_list(lines, "root_sources", root_sources)
    _append_list(lines, "technical_sources", technical_sources)
    _append_list(
        lines,
        "included_extensions",
        sorted(SUPPORTED_SOURCE_TYPES),
    )
    _append_list(lines, "exclusions", MANIFEST_EXCLUSIONS)
    lines.append("sources:")
    for record in records:
        record_items = tuple(record.to_dict().items())
        first_key, first_value = record_items[0]
        lines.append(f"  - {first_key}: {_yaml_scalar(first_value)}")
        for key, value in record_items[1:]:
            lines.append(f"    {key}: {_yaml_scalar(value)}")
    return "\n".join(lines) + "\n"


def write_manifest(
    output_path: Path,
    records: Sequence[SourceRecord],
    *,
    roots: Sequence[str] | None = None,
    root_sources: Sequence[str] | None = None,
    technical_sources: Sequence[str] | None = None,
) -> None:
    """Write a deterministic UTF-8 source manifest."""

    validate_unique_records(records)
    selected_roots = tuple(roots) if roots is not None else DEFAULT_SOURCE_ROOTS
    selected_root_sources = (
        tuple(root_sources)
        if root_sources is not None
        else ROOT_REPORT_SOURCES
    )
    selected_technical_sources = (
        tuple(technical_sources)
        if technical_sources is not None
        else TECHNICAL_SOURCE_ALLOWLIST
    )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(
        _render_manifest(
            sorted(records, key=lambda record: record.source_path),
            roots=selected_roots,
            root_sources=selected_root_sources,
            technical_sources=selected_technical_sources,
        ),
        encoding="utf-8",
        newline="\n",
    )


def _parse_args(arguments: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Build the project knowledge source manifest."
    )
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path("."),
        help="Repository root containing knowledge source folders.",
    )
    parser.add_argument(
        "--write-manifest",
        type=Path,
        required=True,
        help="Repository-relative or absolute manifest output path.",
    )
    return parser.parse_args(arguments)


def main(arguments: Sequence[str] | None = None) -> int:
    """Run source discovery and write the requested manifest."""

    args = _parse_args(arguments)
    repo_root = args.repo_root.resolve()
    output_path = args.write_manifest
    if not output_path.is_absolute():
        output_path = repo_root / output_path
    records = discover_sources(repo_root)
    write_manifest(output_path, records)
    print(f"Wrote {len(records)} sources to {output_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

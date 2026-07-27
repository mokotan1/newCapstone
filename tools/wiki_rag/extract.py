"""Convert manifest-selected sources into provenance-preserving Markdown."""

from __future__ import annotations

import argparse
import hashlib
import os
import sys
import tempfile
from collections.abc import Callable, Mapping, Sequence
from dataclasses import fields
from pathlib import Path
from typing import Any

import yaml

if __package__ in {None, ""}:
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
    from wiki_rag.extractors import ExtractionResult
    from wiki_rag.extractors.pdf import extract_pdf
    from wiki_rag.extractors.pptx import extract_pptx
    from wiki_rag.models import SourceRecord
    from wiki_rag.normalize import normalize_transcript
else:
    from .extractors import ExtractionResult
    from .extractors.pdf import extract_pdf
    from .extractors.pptx import extract_pptx
    from .models import SourceRecord
    from .normalize import normalize_transcript

Extractor = Callable[[Path], ExtractionResult]
EXTRACTORS: Mapping[str, Extractor] = {
    "pdf": extract_pdf,
    "pptx": extract_pptx,
}
_SOURCE_FIELDS = frozenset(field.name for field in fields(SourceRecord))


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _resolve_inside(repo_root: Path, relative_path: str) -> Path:
    if Path(relative_path).is_absolute():
        raise ValueError(f"manifest path must be relative: {relative_path}")
    resolved = (repo_root / Path(relative_path)).resolve()
    try:
        resolved.relative_to(repo_root)
    except ValueError as error:
        raise ValueError(
            f"manifest path escapes repository: {relative_path}"
        ) from error
    return resolved


def _resolve_transcript_path(
    repo_root: Path,
    record: SourceRecord,
) -> Path:
    transcript_path = _resolve_inside(repo_root, record.transcript_path)
    transcript_root = (repo_root / "docs/wiki/sources").resolve()
    try:
        transcript_path.relative_to(transcript_root)
    except ValueError as error:
        raise ValueError(
            "transcript_path must be under docs/wiki/sources: "
            f"{record.transcript_path}"
        ) from error
    return transcript_path


def _write_text_atomic(output_path: Path, content: str) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    temporary_path: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="w",
            encoding="utf-8",
            newline="\n",
            dir=output_path.parent,
            prefix=f".{output_path.name}.",
            suffix=".tmp",
            delete=False,
        ) as temporary:
            temporary.write(content)
            temporary.flush()
            os.fsync(temporary.fileno())
            temporary_path = Path(temporary.name)
        os.replace(temporary_path, output_path)
    finally:
        if temporary_path is not None and temporary_path.exists():
            temporary_path.unlink()


def write_transcript_atomic(
    repo_root: Path,
    record: SourceRecord,
    content: str,
) -> None:
    """Atomically write only to a record's assigned transcript path."""

    resolved_root = repo_root.resolve()
    output_path = _resolve_transcript_path(resolved_root, record)
    _write_text_atomic(output_path, content)


def _record_from_mapping(source: Mapping[str, Any]) -> SourceRecord:
    record_values = {key: source[key] for key in _SOURCE_FIELDS}
    return SourceRecord(**record_values)


def _failure_warning(error: Exception) -> str:
    message = " ".join(str(error).split())
    if len(message) > 160:
        message = message[:157] + "..."
    return (
        f"extraction_failed:{type(error).__name__}"
        + (f":{message}" if message else "")
    )


def _extract_record(
    repo_root: Path,
    record: SourceRecord,
    extractor: Extractor,
) -> ExtractionResult:
    source_path = _resolve_inside(repo_root, record.source_path)
    warnings: list[str] = []
    if _sha256(source_path) != record.source_sha256:
        warnings.append("source_sha256_mismatch")

    result = extractor(source_path)
    return ExtractionResult(
        markdown=result.markdown,
        page_or_slide_count=result.page_or_slide_count,
        warnings=[*warnings, *result.warnings],
    )


def convert_manifest(
    manifest_path: Path,
    repo_root: Path,
    source_types: frozenset[str],
) -> dict[str, int]:
    """Convert selected records and atomically update their manifest state."""

    resolved_root = repo_root.resolve()
    resolved_manifest = manifest_path.resolve()
    manifest_data = yaml.safe_load(resolved_manifest.read_text(encoding="utf-8"))
    if not isinstance(manifest_data, dict):
        raise TypeError("manifest root must be a mapping")
    sources = manifest_data.get("sources")
    if not isinstance(sources, list):
        raise TypeError("manifest sources must be a list")

    counts = {"extracted": 0, "needs_review": 0}
    for source in sources:
        if not isinstance(source, dict):
            raise TypeError("manifest source entries must be mappings")
        source_type = str(source.get("source_type", "")).casefold()
        if source_type not in source_types:
            continue

        record = _record_from_mapping(source)
        extractor = EXTRACTORS.get(source_type)
        if extractor is None:
            raise ValueError(f"unsupported extraction type: {source_type}")

        try:
            result = _extract_record(resolved_root, record, extractor)
        # Isolate corrupt documents so the batch records a reviewable result.
        except Exception as error:  # noqa: BLE001
            result = ExtractionResult(
                markdown="",
                page_or_slide_count=0,
                warnings=[_failure_warning(error)],
            )

        status = "needs_review" if result.warnings else "extracted"
        transcript = normalize_transcript(record, result)
        write_transcript_atomic(resolved_root, record, transcript)
        source["status"] = status
        source["warnings"] = result.warnings
        counts[status] += 1

    rendered_manifest = yaml.safe_dump(
        manifest_data,
        allow_unicode=True,
        sort_keys=False,
        width=1000,
    )
    _write_text_atomic(resolved_manifest, rendered_manifest)
    return counts


def _parse_args(arguments: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Extract PDF and PPTX manifest sources to Markdown."
    )
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path("."),
        help="Repository root containing knowledge source folders.",
    )
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument(
        "--types",
        required=True,
        help="Comma-separated source types (pdf,pptx).",
    )
    return parser.parse_args(arguments)


def main(arguments: Sequence[str] | None = None) -> int:
    """Run manifest-selected source extraction."""

    args = _parse_args(arguments)
    source_types = frozenset(
        item.strip().casefold() for item in args.types.split(",") if item.strip()
    )
    unsupported_types = source_types.difference(EXTRACTORS)
    if not source_types or unsupported_types:
        unsupported = ", ".join(sorted(unsupported_types)) or "(none selected)"
        raise ValueError(f"unsupported extraction types: {unsupported}")

    repo_root = args.repo_root.resolve()
    manifest_path = args.manifest
    if not manifest_path.is_absolute():
        manifest_path = repo_root / manifest_path
    counts = convert_manifest(manifest_path, repo_root, source_types)
    print(
        "Converted "
        f"{sum(counts.values())} sources: "
        f"{counts['extracted']} extracted, "
        f"{counts['needs_review']} needs_review"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

"""Validate manifest coverage, transcript quality, and encoding."""

from __future__ import annotations

import argparse
import re
import sys
from collections.abc import Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any
from urllib.parse import unquote, urlparse

import yaml

if __package__ in {None, ""}:
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
    from wiki_rag.build_rag_corpus import (
        CorpusBuildError,
        expected_rag_source_ids,
        rag_output_filename,
    )
    from wiki_rag.models import SourceRecord
    from wiki_rag.paths import resolve_inside as _resolve_inside
    from wiki_rag.paths import sha256 as _sha256
else:
    from .build_rag_corpus import (
        CorpusBuildError,
        expected_rag_source_ids,
        rag_output_filename,
    )
    from .models import SourceRecord
    from .paths import resolve_inside as _resolve_inside
    from .paths import sha256 as _sha256

_MIN_EXTRACTED_TEXT_CHARS = 40
_OWNER_SKIP_SOURCE_TYPES = frozenset({"hwp"})
_INTERNAL_LINK_PATTERN = re.compile(r"\[[^\]]+\]\(([^)]+)\)")
_REQUIRED_FRONT_MATTER_KEYS = frozenset(
    {
        "source_id",
        "source_path",
        "source_sha256",
        "source_type",
        "category",
        "status",
        "rag_eligible",
    }
)
_REQUIRED_RAG_FRONT_MATTER_KEYS = _REQUIRED_FRONT_MATTER_KEYS | frozenset({"title"})
_MANIFEST_CROSS_CHECK_KEYS = (
    "source_id",
    "source_path",
    "source_sha256",
    "status",
    "rag_eligible",
    "source_type",
    "category",
)


@dataclass(frozen=True)
class ValidationReport:
    """Aggregate validation outcome for one manifest or transcript."""

    error_codes: frozenset[str]
    rows: tuple[dict[str, Any], ...] = ()
    warnings: tuple[str, ...] = ()

    @property
    def ok(self) -> bool:
        return not self.error_codes


def _parse_front_matter(content: str) -> tuple[dict[str, Any], str]:
    if not content.startswith("---"):
        return {}, content
    lines = content.splitlines(keepends=True)
    if not lines or lines[0].strip() != "---":
        return {}, content
    end_index: int | None = None
    for index in range(1, len(lines)):
        if lines[index].strip() == "---":
            end_index = index
            break
    if end_index is None:
        return {}, content
    yaml_text = "".join(lines[1:end_index])
    body = "".join(lines[end_index + 1 :])
    parsed = yaml.safe_load(yaml_text)
    if not isinstance(parsed, dict):
        return {}, body
    return parsed, body


def _meaningful_text_length(body: str) -> int:
    return sum(1 for character in body if not character.isspace())


def _is_internal_link_target(target: str) -> bool:
    stripped = target.strip()
    if not stripped or stripped.startswith("#"):
        return False
    parsed = urlparse(stripped)
    return parsed.scheme in {"", "file"} and not stripped.startswith("//")


def _resolve_internal_link(
    target: str,
    *,
    repo_root: Path,
    source_path: Path | None = None,
    transcript_path: Path | None = None,
) -> Path | None:
    stripped = target.strip()
    parsed = urlparse(stripped)
    link_path = unquote(parsed.path)
    if parsed.scheme == "file":
        candidate = Path(link_path)
    elif link_path.startswith("/"):
        candidate = repo_root / link_path.lstrip("/")
    elif source_path is not None:
        candidate = (source_path.parent / link_path).resolve()
    elif transcript_path is not None:
        candidate = (transcript_path.parent / link_path).resolve()
    else:
        return None
    if candidate.is_file():
        return candidate
    return None


def _front_matter_matches_manifest(
    metadata: Mapping[str, Any],
    manifest_record: Mapping[str, Any],
) -> bool:
    for key in _MANIFEST_CROSS_CHECK_KEYS:
        if key not in metadata:
            continue
        front_value = metadata[key]
        manifest_value = manifest_record.get(key)
        if key == "rag_eligible":
            if bool(front_value) != bool(manifest_value):
                return False
        elif str(front_value) != str(manifest_value):
            return False
    return True


def _collect_transcript_errors(
    transcript_path: Path,
    *,
    repo_root: Path | None = None,
    manifest_record: Mapping[str, Any] | None = None,
) -> set[str]:
    errors: set[str] = set()

    if not transcript_path.is_file():
        errors.add("missing_transcript")
        return errors

    raw_bytes = transcript_path.read_bytes()
    try:
        content = raw_bytes.decode("utf-8")
    except UnicodeDecodeError:
        errors.add("invalid_utf8")
        return errors

    metadata, body = _parse_front_matter(content)
    missing_keys = _REQUIRED_FRONT_MATTER_KEYS.difference(metadata)
    if missing_keys:
        errors.add("invalid_front_matter")
    elif manifest_record is not None and not _front_matter_matches_manifest(
        metadata,
        manifest_record,
    ):
        errors.add("front_matter_drift")

    status = str(metadata.get("status", ""))
    rag_eligible = metadata.get("rag_eligible") is True
    content_kind = ""
    if manifest_record is not None:
        content_kind = str(manifest_record.get("content_kind", "")).casefold()

    if "\ufffd" in body and rag_eligible:
        errors.add("unicode_replacement_character")

    if status == "extracted" and content_kind != "visual_only":
        if _meaningful_text_length(body) < _MIN_EXTRACTED_TEXT_CHARS:
            errors.add("insufficient_text")

    if rag_eligible and status in {"extracted", "needs_review"}:
        if _meaningful_text_length(body) == 0:
            errors.add("missing_meaningful_text")

    resolved_repo_root = repo_root
    source_file: Path | None = None
    if manifest_record is not None:
        source_path_value = str(manifest_record.get("source_path", ""))
        if source_path_value and resolved_repo_root is not None:
            candidate = resolved_repo_root / Path(source_path_value)
            if candidate.is_file():
                source_file = candidate.resolve()

    if resolved_repo_root is not None:
        for match in _INTERNAL_LINK_PATTERN.finditer(body):
            target = match.group(1)
            if not _is_internal_link_target(target):
                continue
            if _resolve_internal_link(
                target,
                repo_root=resolved_repo_root,
                source_path=source_file,
                transcript_path=transcript_path,
            ) is None:
                errors.add("unresolved_internal_link")
                break

    return errors


def validate_transcript(
    transcript_path: Path,
    *,
    repo_root: Path | None = None,
    manifest_record: Mapping[str, Any] | None = None,
) -> ValidationReport:
    """Validate one transcript file."""

    errors = _collect_transcript_errors(
        transcript_path,
        repo_root=repo_root,
        manifest_record=manifest_record,
    )
    return ValidationReport(
        error_codes=frozenset(errors),
    )


def _record_from_mapping(source: Mapping[str, Any]) -> SourceRecord:
    return SourceRecord(
        source_id=str(source["source_id"]),
        source_path=str(source["source_path"]),
        source_sha256=str(source["source_sha256"]),
        source_type=str(source["source_type"]),
        category=str(source["category"]),
        title=str(source.get("title", "")),
        transcript_path=str(source["transcript_path"]),
        status=str(source["status"]),
        rag_eligible=bool(source["rag_eligible"]),
        canonical_group=str(source["canonical_group"]),
    )


def _collect_rag_document_errors(
    rag_path: Path,
    *,
    expected_source_ids: frozenset[str],
) -> set[str]:
    errors: set[str] = set()

    if not rag_path.is_file():
        errors.add("missing_rag_document")
        return errors

    try:
        content = rag_path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        errors.add("invalid_utf8")
        return errors

    metadata, body = _parse_front_matter(content)
    missing_keys = _REQUIRED_RAG_FRONT_MATTER_KEYS.difference(metadata)
    if missing_keys:
        errors.add("invalid_rag_front_matter")

    source_id = str(metadata.get("source_id", ""))
    if source_id not in expected_source_ids:
        errors.add("unexpected_rag_document")

    category = str(metadata.get("category", ""))
    source_type = str(metadata.get("source_type", "")).casefold()
    if category == "report":
        errors.add("rag_report_leak")
    if source_type == "hwp":
        errors.add("rag_hwp_leak")
    if metadata.get("rag_eligible") is not True:
        errors.add("rag_ineligible_leak")

    if _meaningful_text_length(body) < _MIN_EXTRACTED_TEXT_CHARS:
        errors.add("missing_meaningful_text")

    if "\ufffd" in body:
        errors.add("unicode_replacement_character")

    return errors


def validate_rag_corpus(
    manifest_path: Path,
    repo_root: Path,
    rag_dir: Path,
) -> ValidationReport:
    """Validate generated RAG corpus files against manifest eligibility."""

    resolved_root = repo_root.resolve()
    resolved_rag_dir = rag_dir.resolve()
    try:
        expected_source_ids = expected_rag_source_ids(
            manifest_path,
            repo_root=resolved_root,
        )
    except CorpusBuildError:
        return ValidationReport(error_codes=frozenset({"rag_corpus_build_failed"}))

    errors: set[str] = set()
    for source_id in expected_source_ids:
        rag_path = resolved_rag_dir / rag_output_filename(source_id)
        errors.update(
            _collect_rag_document_errors(
                rag_path,
                expected_source_ids=expected_source_ids,
            )
        )

    if resolved_rag_dir.is_dir():
        for rag_path in sorted(resolved_rag_dir.glob("*.md")):
            metadata, _ = _parse_front_matter(
                rag_path.read_text(encoding="utf-8")
            )
            source_id = str(metadata.get("source_id", ""))
            if source_id not in expected_source_ids:
                errors.add("unexpected_rag_document")

    return ValidationReport(error_codes=frozenset(errors))


def validate_manifest(
    manifest_path: Path,
    repo_root: Path,
    *,
    owner_skip_types: frozenset[str] = _OWNER_SKIP_SOURCE_TYPES,
) -> ValidationReport:
    """Validate every manifest record against its original and transcript."""

    resolved_root = repo_root.resolve()
    manifest_data = yaml.safe_load(manifest_path.read_text(encoding="utf-8"))
    if not isinstance(manifest_data, dict):
        raise TypeError("manifest root must be a mapping")
    sources = manifest_data.get("sources")
    if not isinstance(sources, list):
        raise TypeError("manifest sources must be a list")

    errors: set[str] = set()
    warnings: list[str] = []
    rows: list[dict[str, Any]] = []

    for source in sources:
        if not isinstance(source, dict):
            raise TypeError("manifest source entries must be mappings")

        record = _record_from_mapping(source)
        source_type = record.source_type.casefold()
        skip_reason: str | None = None
        row_warnings = [
            str(item) for item in source.get("warnings", []) if item is not None
        ]

        if source_type in owner_skip_types and record.status == "pending":
            skip_reason = "skipped_by_owner"
            rows.append(
                {
                    "source_path": record.source_path,
                    "source_type": record.source_type,
                    "transcript_path": record.transcript_path,
                    "status": record.status,
                    "warnings": row_warnings,
                    "rag_eligible": record.rag_eligible,
                    "canonical_group": record.canonical_group,
                    "skip_reason": skip_reason,
                }
            )
            continue

        source_path = _resolve_inside(resolved_root, record.source_path)
        transcript_path = _resolve_inside(resolved_root, record.transcript_path)

        if not source_path.is_file():
            errors.add("missing_source")
        elif _sha256(source_path) != record.source_sha256:
            errors.add("source_hash_changed")

        if record.status == "pending":
            errors.add("unconverted_in_scope")

        transcript_report = validate_transcript(
            transcript_path,
            repo_root=resolved_root,
            manifest_record=source,
        )
        errors.update(transcript_report.error_codes)
        warnings.extend(transcript_report.warnings)

        rows.append(
            {
                "source_path": record.source_path,
                "source_type": record.source_type,
                "transcript_path": record.transcript_path,
                "status": record.status,
                "warnings": row_warnings,
                "rag_eligible": record.rag_eligible,
                "canonical_group": record.canonical_group,
                "skip_reason": skip_reason,
            }
        )

    return ValidationReport(
        error_codes=frozenset(errors),
        rows=tuple(rows),
        warnings=tuple(warnings),
    )


def render_coverage_report(report: ValidationReport) -> str:
    """Render a human-readable coverage report."""

    lines = [
        "# Project Knowledge Coverage Report",
        "",
        f"- Validation status: {'PASS' if report.ok else 'FAIL'}",
        f"- Error codes: {', '.join(sorted(report.error_codes)) or '(none)'}",
        "",
        "| Source path | Type | Transcript | Status | Warnings | RAG | Canonical group | Notes |",
        "| --- | --- | --- | --- | --- | --- | --- | --- |",
    ]
    for row in report.rows:
        warning_text = "; ".join(row.get("warnings", [])) or "-"
        notes = row.get("skip_reason") or "-"
        lines.append(
            "| {source_path} | {source_type} | {transcript_path} | {status} | "
            "{warnings} | {rag_eligible} | {canonical_group} | {notes} |".format(
                source_path=row["source_path"],
                source_type=row["source_type"],
                transcript_path=row["transcript_path"],
                status=row["status"],
                warnings=warning_text.replace("|", "\\|"),
                rag_eligible="yes" if row.get("rag_eligible") else "no",
                canonical_group=row["canonical_group"],
                notes=notes,
            )
        )
    return "\n".join(lines) + "\n"


def write_coverage_report(report: ValidationReport, output_path: Path) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(
        render_coverage_report(report),
        encoding="utf-8",
        newline="\n",
    )


def _parse_args(arguments: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate project knowledge conversion coverage."
    )
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path("."),
        help="Repository root containing knowledge source folders.",
    )
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument(
        "--write-report",
        type=Path,
        help="Repository-relative or absolute coverage report output path.",
    )
    parser.add_argument(
        "--rag-dir",
        type=Path,
        help="Optional generated RAG corpus directory to validate.",
    )
    return parser.parse_args(arguments)


def main(arguments: Sequence[str] | None = None) -> int:
    """Run manifest validation and optionally write a coverage report."""

    args = _parse_args(arguments)
    repo_root = args.repo_root.resolve()
    manifest_path = args.manifest
    if not manifest_path.is_absolute():
        manifest_path = repo_root / manifest_path

    report = validate_manifest(manifest_path, repo_root)
    if args.rag_dir is not None:
        rag_dir = args.rag_dir
        if not rag_dir.is_absolute():
            rag_dir = repo_root / rag_dir
        rag_report = validate_rag_corpus(manifest_path, repo_root, rag_dir)
        report = ValidationReport(
            error_codes=report.error_codes | rag_report.error_codes,
            rows=report.rows,
            warnings=report.warnings,
        )
    if args.write_report is not None:
        output_path = args.write_report
        if not output_path.is_absolute():
            output_path = repo_root / output_path
        write_coverage_report(report, output_path)

    if report.ok:
        print(f"Validation passed for {len(report.rows)} manifest records.")
        return 0

    joined = ", ".join(sorted(report.error_codes))
    print(f"Validation failed: {joined}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())

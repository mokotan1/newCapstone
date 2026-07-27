"""Build citation-bearing RAG corpus documents from eligible transcripts."""

from __future__ import annotations

import argparse
import difflib
import re
import sys
from collections.abc import Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import yaml

if __package__ in {None, ""}:
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
    from wiki_rag.models import SourceRecord
    from wiki_rag.normalize import _normalize_markdown, _yaml_scalar
    from wiki_rag.paths import resolve_inside as _resolve_inside
else:
    from .models import SourceRecord
    from .normalize import _normalize_markdown, _yaml_scalar
    from .paths import resolve_inside as _resolve_inside

_RAG_ELIGIBLE_STATUSES = frozenset({"extracted", "needs_review"})
_OWNER_SKIP_SOURCE_TYPES = frozenset({"hwp"})
_MIN_MEANINGFUL_TEXT_CHARS = 40
_SIMILARITY_THRESHOLD = 0.90
_REQUIRED_RAG_FRONT_MATTER_KEYS = frozenset(
    {
        "source_id",
        "source_path",
        "source_sha256",
        "source_type",
        "category",
        "title",
        "status",
        "rag_eligible",
    }
)


class CorpusBuildError(Exception):
    """Raised when the RAG corpus cannot be built safely."""


@dataclass(frozen=True)
class RagDocumentPlan:
    """One RAG corpus document to emit for an eligible source."""

    record: SourceRecord
    body: str
    related_source_ids: tuple[str, ...] = ()


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


def _load_manifest_records(manifest: Path) -> list[SourceRecord]:
    manifest_data = yaml.safe_load(manifest.read_text(encoding="utf-8"))
    if not isinstance(manifest_data, dict):
        raise TypeError("manifest root must be a mapping")
    sources = manifest_data.get("sources")
    if not isinstance(sources, list):
        raise TypeError("manifest sources must be a list")
    return [
        _record_from_mapping(source)
        for source in sources
        if isinstance(source, dict)
    ]


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


def _collapse_for_similarity(body: str) -> str:
    collapsed = re.sub(r"\s+", " ", body.strip().casefold())
    return collapsed


def _body_similarity(left: str, right: str) -> float:
    left_norm = _collapse_for_similarity(left)
    right_norm = _collapse_for_similarity(right)
    if not left_norm and not right_norm:
        return 1.0
    if not left_norm or not right_norm:
        return 0.0
    return difflib.SequenceMatcher(None, left_norm, right_norm).ratio()


def _read_transcript_body(
    record: SourceRecord,
    *,
    repo_root: Path,
) -> str:
    transcript_path = _resolve_inside(repo_root, record.transcript_path)
    if not transcript_path.is_file():
        raise CorpusBuildError(
            f"missing transcript for RAG-eligible source {record.source_id}: "
            f"{record.transcript_path}"
        )
    content = transcript_path.read_text(encoding="utf-8")
    _, body = _parse_front_matter(content)
    return _normalize_markdown(body)


def _is_candidate_record(record: SourceRecord) -> bool:
    if not record.rag_eligible:
        return False
    if record.category == "report":
        return False
    if record.source_type.casefold() in _OWNER_SKIP_SOURCE_TYPES:
        return False
    if record.status == "blocked_hwp_com":
        return False
    return record.status in _RAG_ELIGIBLE_STATUSES


def rag_output_filename(source_id: str) -> str:
    """Return the stable RAG corpus filename for one manifest source_id."""

    category, suffix = source_id.split(":", 1)
    return f"{category}-{suffix}.md"


def plan_rag_documents(
    manifest: Path,
    *,
    repo_root: Path,
) -> list[RagDocumentPlan]:
    """Select eligible sources and resolve canonical PDF/PPTX duplicates."""

    resolved_root = repo_root.resolve()
    records = _load_manifest_records(manifest)
    candidates = [record for record in records if _is_candidate_record(record)]

    bodies: dict[str, str] = {}
    for record in candidates:
        body = _read_transcript_body(record, repo_root=resolved_root)
        if _meaningful_text_length(body) < _MIN_MEANINGFUL_TEXT_CHARS:
            raise CorpusBuildError(
                f"RAG-eligible source {record.source_id} lacks meaningful text"
            )
        bodies[record.source_id] = body

    grouped: dict[str, list[SourceRecord]] = {}
    for record in candidates:
        grouped.setdefault(record.canonical_group, []).append(record)

    selected: dict[str, RagDocumentPlan] = {}
    for group_records in grouped.values():
        pdfs = [
            record
            for record in group_records
            if record.source_type.casefold() == "pdf"
        ]
        pptxs = [
            record
            for record in group_records
            if record.source_type.casefold() == "pptx"
        ]

        suppressed_pdf_ids: set[str] = set()
        if pdfs and pptxs:
            for pdf_record in pdfs:
                for pptx_record in pptxs:
                    similarity = _body_similarity(
                        bodies[pdf_record.source_id],
                        bodies[pptx_record.source_id],
                    )
                    if similarity >= _SIMILARITY_THRESHOLD:
                        suppressed_pdf_ids.add(pdf_record.source_id)
                        existing = selected.get(pptx_record.source_id)
                        related = tuple(
                            sorted(
                                {
                                    *(existing.related_source_ids if existing else ()),
                                    pdf_record.source_id,
                                }
                            )
                        )
                        selected[pptx_record.source_id] = RagDocumentPlan(
                            record=pptx_record,
                            body=bodies[pptx_record.source_id],
                            related_source_ids=related,
                        )

        for record in group_records:
            if record.source_id in suppressed_pdf_ids:
                continue
            if record.source_id in selected:
                continue
            selected[record.source_id] = RagDocumentPlan(
                record=record,
                body=bodies[record.source_id],
            )

    return sorted(
        selected.values(),
        key=lambda plan: plan.record.source_id,
    )


def _render_rag_document(plan: RagDocumentPlan) -> str:
    record = plan.record
    metadata: list[tuple[str, Any]] = [
        ("source_id", record.source_id),
        ("source_path", record.source_path),
        ("source_sha256", record.source_sha256),
        ("source_type", record.source_type),
        ("category", record.category),
        ("title", record.title),
        ("status", record.status),
        ("rag_eligible", record.rag_eligible),
    ]
    front_matter = ["---"]
    front_matter.extend(
        f"{key}: {_yaml_scalar(value)}" for key, value in metadata
    )
    if plan.related_source_ids:
        front_matter.append("related_source_ids:")
        front_matter.extend(
            f"  - {_yaml_scalar(source_id)}"
            for source_id in plan.related_source_ids
        )
    front_matter.extend(("---", ""))
    body = plan.body
    return "\n".join(front_matter) + "\n" + body + ("\n" if body else "")


def build_rag_corpus(
    manifest: Path,
    *,
    output_dir: Path,
    repo_root: Path | None = None,
) -> list[Path]:
    """Write one RAG document per eligible source transcript."""

    manifest_path = manifest.resolve()
    resolved_root = (repo_root or manifest_path.parent).resolve()
    plans = plan_rag_documents(manifest_path, repo_root=resolved_root)

    resolved_output = output_dir.resolve()
    resolved_output.mkdir(parents=True, exist_ok=True)

    expected_names = {rag_output_filename(plan.record.source_id) for plan in plans}
    for existing in resolved_output.glob("*.md"):
        if existing.name not in expected_names:
            existing.unlink()

    written: list[Path] = []
    for plan in plans:
        output_path = resolved_output / rag_output_filename(plan.record.source_id)
        output_path.write_text(
            _render_rag_document(plan),
            encoding="utf-8",
            newline="\n",
        )
        written.append(output_path)
    return written


def expected_rag_source_ids(
    manifest: Path,
    *,
    repo_root: Path,
) -> frozenset[str]:
    """Return source_ids that should appear in the RAG corpus."""

    plans = plan_rag_documents(manifest.resolve(), repo_root=repo_root.resolve())
    return frozenset(plan.record.source_id for plan in plans)


def _parse_args(arguments: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Build citation-bearing RAG corpus files from eligible transcripts.",
    )
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path("."),
        help="Repository root for resolving manifest-relative paths.",
    )
    return parser.parse_args(arguments)


def main(arguments: Sequence[str] | None = None) -> int:
    """CLI entrypoint for RAG corpus generation."""

    args = _parse_args(arguments)
    repo_root = args.repo_root.resolve()
    manifest_path = args.manifest
    if not manifest_path.is_absolute():
        manifest_path = repo_root / manifest_path
    output_dir = args.output_dir
    if not output_dir.is_absolute():
        output_dir = repo_root / output_dir

    try:
        written = build_rag_corpus(
            manifest_path,
            output_dir=output_dir,
            repo_root=repo_root,
        )
    except CorpusBuildError as error:
        print(str(error), file=sys.stderr)
        return 1

    print(f"RAG corpus written: {len(written)} documents in {output_dir.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

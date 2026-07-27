"""Offline end-to-end integration for the project knowledge pipeline."""

from __future__ import annotations

from dataclasses import replace
from pathlib import Path

import yaml

from wiki_rag.build_rag_corpus import build_rag_corpus, expected_rag_source_ids
from wiki_rag.build_wiki import build_wiki
from wiki_rag.extract import EXTRACTORS, convert_manifest
from wiki_rag.inventory import discover_sources, write_manifest
from wiki_rag.validate import validate_manifest, validate_rag_corpus

_NON_HWP_EXTRACT_TYPES = frozenset(EXTRACTORS)

# Curated wiki pages cite these IDs; e2e aligns discovered records to them.
_CURATED_SOURCES: tuple[tuple[str, str, str, str], ...] = (
    ("scenario:93cff884e57e", "시나리오/world-lore.md", "scenario", "world-lore"),
    ("scenario:a73346ecb3d9", "시나리오/characters.md", "scenario", "characters"),
    ("planning:35ada8161577", "기획서/concept.md", "planning", "concept"),
    ("planning:e4e36660bb79", "기획서/opening.md", "planning", "opening"),
    ("planning:a54025e67028", "기획서/second-floor.md", "planning", "second-floor"),
    ("planning:47f3be566f34", "기획서/basement.md", "planning", "basement"),
    ("planning:b98bbfbdb019", "기획서/ai-dialogue.md", "planning", "ai-dialogue"),
    ("planning:9d4611de3ae3", "기획서/initial-plan.md", "planning", "initial-plan"),
    ("technical:85fdfa8e3425", "docs/fungus-room-migration-plan.md", "technical", "fungus-room"),
    ("technical:884df6c5b462", "docs/architecture.md", "technical", "architecture"),
    (
        "technical:03a736ea3ab1",
        "docs/security/llm-abuse-defense-plan.md",
        "technical",
        "llm-defense",
    ),
)

_CURATED_BY_PATH: dict[str, tuple[str, str, str]] = {
    source_path: (source_id, category, title)
    for source_id, source_path, category, title in _CURATED_SOURCES
}


def _write_source(repo_root: Path, relative_path: str, content: bytes) -> None:
    source_path = repo_root / relative_path
    source_path.parent.mkdir(parents=True, exist_ok=True)
    source_path.write_bytes(content)


def _fixture_body(source_id: str, title: str) -> bytes:
    text = (
        f"Fixture knowledge for {source_id} ({title}). "
        "This body is long enough for transcript and RAG validation gates."
    )
    return text.encode("utf-8")


def seed_small_knowledge_repo(repo_root: Path) -> None:
    """Create a minimal offline knowledge tree including curated citation IDs."""

    for source_id, source_path, _category, title in _CURATED_SOURCES:
        _write_source(repo_root, source_path, _fixture_body(source_id, title))

    _write_source(
        repo_root,
        "시나리오/extra-scenario.md",
        b"Extra scenario note used only by the offline end-to-end fixture.",
    )
    _write_source(repo_root, "보고서/skipped.hwp", b"owner-skipped hwp fixture")


def _align_curated_records(records: list) -> list:
    aligned = []
    for record in records:
        curated = _CURATED_BY_PATH.get(record.source_path)
        if curated is None:
            aligned.append(record)
            continue
        source_id, category, title = curated
        slug = title.lower().replace(" ", "-")
        aligned.append(
            replace(
                record,
                source_id=source_id,
                category=category,
                title=title,
                transcript_path=(
                    f"docs/wiki/sources/{category}/{slug}--{record.source_sha256[:12]}.md"
                ),
                canonical_group=f"{category}:{slug}",
            )
        )
    return aligned


def run_inventory(repo_root: Path) -> Path:
    """Discover sources and write the manifest under docs/wiki/_meta/."""

    technical_paths = tuple(path for _id, path, _cat, _title in _CURATED_SOURCES if path.startswith("docs/"))
    records = discover_sources(
        repo_root,
        roots=["시나리오", "기획서", "보고서", "docs"],
        root_sources=[],
        technical_sources=technical_paths,
    )
    manifest_path = repo_root / "docs/wiki/_meta/source-manifest.yaml"
    write_manifest(
        manifest_path,
        _align_curated_records(records),
        roots=["시나리오", "기획서", "보고서", "docs"],
        root_sources=[],
        technical_sources=technical_paths,
    )
    return manifest_path


def run_extraction(
    manifest: Path,
    *,
    repo_root: Path,
    allow_hwp: bool = False,
) -> None:
    """Extract transcripts for supported types; HWP stays owner-skipped when disabled."""

    _ = allow_hwp  # HWP is never in EXTRACTORS; pending rows remain owner-skipped.
    convert_manifest(manifest.resolve(), repo_root.resolve(), _NON_HWP_EXTRACT_TYPES)


def test_small_knowledge_set_runs_inventory_to_corpus_without_network(
    tmp_path: Path,
) -> None:
    seed_small_knowledge_repo(tmp_path)
    manifest = run_inventory(tmp_path)
    run_extraction(manifest, repo_root=tmp_path, allow_hwp=False)
    build_wiki(manifest=manifest, wiki_root=tmp_path / "docs/wiki")
    written = build_rag_corpus(
        manifest,
        output_dir=tmp_path / "docs/wiki/rag",
        repo_root=tmp_path,
    )
    assert written
    manifest_data = yaml.safe_load(manifest.read_text(encoding="utf-8"))
    hwp_source_ids = frozenset(
        str(source["source_id"])
        for source in manifest_data.get("sources") or []
        if isinstance(source, dict)
        and str(source.get("source_type", "")).casefold() == "hwp"
    )
    assert hwp_source_ids, "fixture should include HWP rows for exclusion coverage"
    corpus_source_ids = expected_rag_source_ids(manifest, repo_root=tmp_path)
    assert hwp_source_ids.isdisjoint(corpus_source_ids)
    manifest_report = validate_manifest(manifest, tmp_path)
    assert manifest_report.ok, sorted(manifest_report.error_codes)
    rag_report = validate_rag_corpus(
        manifest,
        tmp_path,
        tmp_path / "docs/wiki/rag",
    )
    assert rag_report.ok, sorted(rag_report.error_codes)

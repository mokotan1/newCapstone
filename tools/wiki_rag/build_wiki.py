"""Render curated wiki navigation pages from manifest records."""

from __future__ import annotations

import argparse
import sys
from collections.abc import Mapping, Sequence
from pathlib import Path
from typing import Any

import yaml

if __package__ in {None, ""}:
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
    from wiki_rag.models import SourceRecord
else:
    from .models import SourceRecord

_OWNER_SKIP_SOURCE_TYPES = frozenset({"hwp"})
_PUBLIC_NAV_STATUSES = frozenset({"extracted", "needs_review"})
_CATEGORY_LABELS: Mapping[str, str] = {
    "scenario": "Scenario",
    "planning": "Planning",
    "technical": "Technical",
    "report": "Report",
}
_BUILD_COMMAND = (
    "python tools/wiki_rag/build_wiki.py "
    "--manifest docs/wiki/_meta/source-manifest.yaml "
    "--wiki-root docs/wiki"
)
_CURATED_CITATION_IDS: frozenset[str] = frozenset(
    {
        "planning:35ada8161577",
        "scenario:a73346ecb3d9",
        "scenario:93cff884e57e",
        "planning:e4e36660bb79",
        "planning:a54025e67028",
        "planning:47f3be566f34",
        "technical:85fdfa8e3425",
        "planning:b98bbfbdb019",
        "technical:884df6c5b462",
        "technical:03a736ea3ab1",
        "planning:9d4611de3ae3",
    }
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


def _load_manifest_records(manifest: Path) -> tuple[dict[str, Any], list[SourceRecord]]:
    manifest_data = yaml.safe_load(manifest.read_text(encoding="utf-8"))
    if not isinstance(manifest_data, dict):
        raise TypeError("manifest root must be a mapping")
    sources = manifest_data.get("sources")
    if not isinstance(sources, list):
        raise TypeError("manifest sources must be a list")
    records = [
        _record_from_mapping(source)
        for source in sources
        if isinstance(source, dict)
    ]
    return manifest_data, records


def _transcript_href(transcript_path: str) -> str:
    normalized = transcript_path.replace("\\", "/")
    prefix = "docs/wiki/"
    if normalized.startswith(prefix):
        return normalized[len(prefix) :]
    return normalized


def _source_repo_href(source_path: str) -> str:
    normalized = source_path.replace("\\", "/")
    return f"../../{normalized}"


def _cite(record: SourceRecord) -> str:
    href = _transcript_href(record.transcript_path)
    return f"([source_id: {record.source_id}]({href}))"


def _validate_curated_citations(
    records_by_id: Mapping[str, SourceRecord],
) -> None:
    missing_ids = sorted(
        source_id
        for source_id in _CURATED_CITATION_IDS
        if source_id not in records_by_id
    )
    if missing_ids:
        joined = ", ".join(missing_ids)
        raise ValueError(
            "Curated wiki claims reference source_id values missing from the "
            f"manifest: {joined}"
        )


def _citation_from_id(
    records_by_id: Mapping[str, SourceRecord],
    source_id: str,
) -> str:
    record = records_by_id.get(source_id)
    if record is None:
        raise ValueError(
            f"Curated wiki claim references missing source_id: {source_id}"
        )
    return _cite(record)


def _is_public_nav_source(record: SourceRecord) -> bool:
    if record.category == "report":
        return False
    if record.source_type.casefold() in _OWNER_SKIP_SOURCE_TYPES:
        return False
    if record.status == "blocked_hwp_com":
        return False
    return record.status in _PUBLIC_NAV_STATUSES


def _render_source_index(
    *,
    category: str,
    label: str,
    records: Sequence[SourceRecord],
) -> str:
    lines = [
        f"# Source Index — {label}",
        "",
        "Back to [Home](Home.md).",
        "",
        "| Title | Type | Status | Canonical group | Transcript | Original |",
        "| --- | --- | --- | --- | --- | --- |",
    ]
    sorted_records = sorted(records, key=lambda item: item.title.casefold())
    for record in sorted_records:
        transcript_href = _transcript_href(record.transcript_path)
        original_href = _source_repo_href(record.source_path)
        lines.append(
            "| {title} | {source_type} | {status} | `{canonical_group}` | "
            "[transcript]({transcript_href}) | "
            "[original]({original_href}) |".format(
                title=record.title.replace("|", "\\|"),
                source_type=record.source_type,
                status=record.status,
                canonical_group=record.canonical_group,
                transcript_href=transcript_href,
                original_href=original_href,
            )
        )
    if not sorted_records:
        lines.append("| *(none)* | | | | | |")
    return "\n".join(lines) + "\n"


def _render_home(
    *,
    records: Sequence[SourceRecord],
    public_records: Sequence[SourceRecord],
) -> str:
    category_counts = {
        category: sum(1 for record in public_records if record.category == category)
        for category in ("scenario", "planning", "technical")
    }
    lines = [
        "# Project Knowledge Wiki",
        "",
        "## Source of truth",
        "",
        "Curated pages in `docs/wiki/` summarize reviewed project knowledge.",
        "Each nontrivial claim cites one or more `source_id` values that link to",
        "transcript pages under `docs/wiki/sources/`.",
        "The manifest at `docs/wiki/_meta/source-manifest.yaml` is the inventory",
        "of record for which originals were converted and their extraction status.",
        "",
        "## Scope",
        "",
        "- **Included:** scenario PDFs/Markdown, planning PDFs/PPTX, and allowlisted",
        "  technical docs with extracted or needs-review transcripts.",
        "- **Excluded from public navigation:** internal reports, pending or skipped",
        "  HWP originals, and blocked conversions.",
        "- **HWP:** owner-skipped; not converted in this pipeline.",
        "",
        (
            f"- **Manifest records:** {len(records)} total, "
            f"{len(public_records)} listed below."
        ),
        "",
        "## Update command",
        "",
        "After inventory, conversion, or validation changes, regenerate this wiki:",
        "",
        "```powershell",
        f"{_BUILD_COMMAND}",
        "```",
        "",
        "## Curated pages",
        "",
        "- [Game Overview](Game-Overview.md)",
        "- [Story and World](Story-and-World.md)",
        "- [Rooms and Progression](Rooms-and-Progression.md)",
        "- [AI and Dialogue](AI-and-Dialogue.md)",
        "- [Architecture](Architecture.md)",
        "- [Development History](Development-History.md)",
        "- [Operations](OPERATIONS.md)",
        "",
        "## Source indexes",
        "",
    ]
    for category, label in _CATEGORY_LABELS.items():
        if category == "report":
            continue
        count = category_counts.get(category, 0)
        index_name = f"Source-Index-{label}.md"
        lines.append(f"- [{label} ({count})]({index_name})")
    lines.extend(
        [
            "",
            "## Public source listing",
            "",
            "| Category | Title | Type | Status | Transcript |",
            "| --- | --- | --- | --- | --- |",
        ]
    )
    for record in sorted(public_records, key=lambda item: (item.category, item.title)):
        label = _CATEGORY_LABELS.get(record.category, record.category.title())
        transcript_href = _transcript_href(record.transcript_path)
        lines.append(
            "| {category} | {title} | {source_type} | {status} | "
            "[transcript]({transcript_href}) |".format(
                category=label,
                title=record.title.replace("|", "\\|"),
                source_type=record.source_type,
                status=record.status,
                transcript_href=transcript_href,
            )
        )
    if not public_records:
        lines.append("| *(none)* | | | | |")
    return "\n".join(lines) + "\n"


def _render_game_overview(records_by_id: Mapping[str, SourceRecord]) -> str:
    claims: list[str] = []
    concept = (
        "The game is a 2025 Korea-set mystery thriller in a cult leader's mansion "
        "where the player gathers information, solves puzzles, and escapes."
    )
    claims.append(
        f"- {concept} {_citation_from_id(records_by_id, 'planning:35ada8161577')}"
    )

    goal = (
        "The player goal is to explore the mansion, solve puzzles, uncover the "
        "mystery, and find an exit."
    )
    claims.append(
        f"- {goal} {_citation_from_id(records_by_id, 'planning:35ada8161577')}"
    )

    loop = (
        "Core loop: room exploration, evidence and puzzle combination, and "
        "linear story progression guided by scenario beats."
    )
    claims.append(
        f"- {loop} {_citation_from_id(records_by_id, 'planning:35ada8161577')}"
    )

    genre = (
        "Genre positioning: mystery thriller, occult horror, and exploration "
        "adventure."
    )
    claims.append(
        f"- {genre} {_citation_from_id(records_by_id, 'scenario:a73346ecb3d9')}"
    )

    lines = [
        "# Game Overview",
        "",
        "Back to [Home](Home.md).",
        "",
        "## Product premise",
        "",
    ]
    lines.extend(claims)
    lines.extend(
        [
            "",
            "## Related sources",
            "",
            "- [Story and World](Story-and-World.md)",
            "- [Rooms and Progression](Rooms-and-Progression.md)",
            "- [Source Index — Scenario](Source-Index-Scenario.md)",
            "- [Source Index — Planning](Source-Index-Planning.md)",
            "",
        ]
    )
    return "\n".join(lines)


def _render_story_and_world(records_by_id: Mapping[str, SourceRecord]) -> str:
    claims: list[str] = []
    setting = (
        "Setting: 2025 Republic of Korea, inside the hidden mansion of a cult "
        "leader; outwardly charitable but concealing occult experiments."
    )
    claims.append(
        f"- {setting} {_citation_from_id(records_by_id, 'scenario:93cff884e57e')}"
    )

    protagonist = (
        "Protagonist: a former police detective driven by guilt over a lost "
        "partner; dry and cynical tone with investigative judgment under pressure."
    )
    claims.append(
        f"- {protagonist} {_citation_from_id(records_by_id, 'scenario:a73346ecb3d9')}"
    )

    cheshire = (
        "Cheshire: a mansion parrot who delivers riddles and testimony; not a "
        "simple animal but a witness to the mansion's truth."
    )
    claims.append(
        f"- {cheshire} {_citation_from_id(records_by_id, 'scenario:a73346ecb3d9')}"
    )

    antagonist = (
        "Antagonist Alfred: mansion owner and leader of the cult 'Those Who "
        "Advocate'; twisted faith after his wife's death leads to human sacrifice "
        "experiments."
    )
    claims.append(
        f"- {antagonist} {_citation_from_id(records_by_id, 'scenario:a73346ecb3d9')}"
    )

    cult = (
        "The cult poses as a university service club helping vulnerable people "
        "while recruiting victims for rituals and body-regeneration research."
    )
    claims.append(
        f"- {cult} {_citation_from_id(records_by_id, 'scenario:93cff884e57e')}"
    )

    lines = [
        "# Story and World",
        "",
        "Back to [Home](Home.md).",
        "",
        "## Setting and characters",
        "",
    ]
    lines.extend(claims)
    lines.extend(
        [
            "",
            "## Related sources",
            "",
            "- [Game Overview](Game-Overview.md)",
            "- [Rooms and Progression](Rooms-and-Progression.md)",
            "- [Source Index — Scenario](Source-Index-Scenario.md)",
            "",
        ]
    )
    return "\n".join(lines)


def _render_rooms_and_progression(
    records_by_id: Mapping[str, SourceRecord],
) -> str:
    claims: list[str] = []
    opening = (
        "Opening flow: civil-office phone call about a foul smell, arrival at the "
        "mansion, doorbell with no answer, then entering through an unlocked gate."
    )
    claims.append(
        f"- {opening} {_citation_from_id(records_by_id, 'planning:e4e36660bb79')}"
    )

    second_floor = (
        "Second-floor corridor connects study, child's room, wife's room with "
        "closet, and master bedroom around a central hall."
    )
    claims.append(
        f"- {second_floor} {_citation_from_id(records_by_id, 'planning:a54025e67028')}"
    )

    basement = (
        "Basement lab progression requires collecting sacred vessels from the "
        "second floor and feeding them into an extractor to craft a radiant cross "
        "item."
    )
    claims.append(
        f"- {basement} {_citation_from_id(records_by_id, 'planning:47f3be566f34')}"
    )

    migration = (
        "Room interaction is implemented through Unity scenes coordinated with "
        "Fungus flowcharts and the Godlotto interaction layer."
    )
    claims.append(
        f"- {migration} {_citation_from_id(records_by_id, 'technical:85fdfa8e3425')}"
    )

    lines = [
        "# Rooms and Progression",
        "",
        "Back to [Home](Home.md).",
        "",
        "## Scene flow and dependencies",
        "",
    ]
    lines.extend(claims)
    lines.extend(
        [
            "",
            "## Related sources",
            "",
            "- [Game Overview](Game-Overview.md)",
            "- [Architecture](Architecture.md)",
            "- [Source Index — Planning](Source-Index-Planning.md)",
            "",
        ]
    )
    return "\n".join(lines)


def _render_ai_and_dialogue(records_by_id: Mapping[str, SourceRecord]) -> str:
    claims: list[str] = []
    dynamic = (
        "Main AI system: dynamic NPC dialogue generated at runtime instead of "
        "only pre-written lines, with scene-specific context flags shaping prompts."
    )
    claims.append(
        f"- {dynamic} {_citation_from_id(records_by_id, 'planning:b98bbfbdb019')}"
    )

    prompts = (
        "Persona and instructions live in external prompt files so behavior can "
        "change without code edits; Fungus bool variables inject situational orders."
    )
    claims.append(
        f"- {prompts} {_citation_from_id(records_by_id, 'planning:b98bbfbdb019')}"
    )

    backend = (
        "Production stack routes Unity chat UI through FastAPI `/chat` endpoints "
        "with Groq primary and Gemini fallback providers."
    )
    claims.append(
        f"- {backend} {_citation_from_id(records_by_id, 'technical:884df6c5b462')}"
    )

    defense = (
        "LLM abuse defenses and play-test guidance are documented for Cheshire "
        "prompt hardening and rate limiting."
    )
    claims.append(
        f"- {defense} {_citation_from_id(records_by_id, 'technical:03a736ea3ab1')}"
    )

    lines = [
        "# AI and Dialogue",
        "",
        "Back to [Home](Home.md).",
        "",
        "## Cheshire, prompts, and tutor behavior",
        "",
    ]
    lines.extend(claims)
    lines.extend(
        [
            "",
            "## Related sources",
            "",
            "- [Architecture](Architecture.md)",
            "- [Source Index — Planning](Source-Index-Planning.md)",
            "- [Source Index — Technical](Source-Index-Technical.md)",
            "",
        ]
    )
    return "\n".join(lines)


def _render_architecture(records_by_id: Mapping[str, SourceRecord]) -> str:
    claims: list[str] = []
    overview = (
        "Monorepo layout: Unity client in `disputatio/`, FastAPI AI backend in "
        "`backend_ai/`, CI scripts, and deploy compose under `deploy/`."
    )
    claims.append(
        f"- {overview} {_citation_from_id(records_by_id, 'technical:884df6c5b462')}"
    )

    unity = (
        "Unity 6 (6000.0.36f1) with URP, Fungus dialogue, Input System, and team "
        "gameplay code under `Assets/godlotto/Script/`."
    )
    claims.append(
        f"- {unity} {_citation_from_id(records_by_id, 'technical:884df6c5b462')}"
    )

    persistence = (
        "Client persistence uses PlayerPrefs checkpoints and Fungus variables; "
        "server-side data includes CSV quiz banks and optional Redis rate limits."
    )
    claims.append(
        f"- {persistence} {_citation_from_id(records_by_id, 'technical:884df6c5b462')}"
    )

    deploy = (
        "Deployment path: GHCR images to EC2 with Docker Compose and Caddy per "
        "`deploy/docker-compose.prod.yml`."
    )
    claims.append(
        f"- {deploy} {_citation_from_id(records_by_id, 'technical:884df6c5b462')}"
    )

    lines = [
        "# Architecture",
        "",
        "Back to [Home](Home.md).",
        "",
        "## Unity, backend, deployment, and tools",
        "",
    ]
    lines.extend(claims)
    lines.extend(
        [
            "",
            "## Related sources",
            "",
            "- [AI and Dialogue](AI-and-Dialogue.md)",
            "- [Operations](OPERATIONS.md)",
            "- [Source Index — Technical](Source-Index-Technical.md)",
            "",
        ]
    )
    return "\n".join(lines)


def _render_development_history(
    records: Sequence[SourceRecord],
    records_by_id: Mapping[str, SourceRecord],
) -> str:
    claims: list[str] = []
    initial = (
        "Early planning positioned the project as a mansion mystery with puzzle "
        "exploration and evidence-driven story beats."
    )
    claims.append(
        f"- {initial} {_citation_from_id(records_by_id, 'planning:9d4611de3ae3')}"
    )

    report_records = sorted(
        (
            record
            for record in records
            if record.category == "report"
            and record.status in _PUBLIC_NAV_STATUSES
        ),
        key=lambda item: item.title.casefold(),
    )

    lines = [
        "# Development History",
        "",
        "Back to [Home](Home.md).",
        "",
        "## Design evolution",
        "",
    ]
    lines.extend(claims)
    lines.extend(
        [
            "",
            "## Internal materials",
            "",
            "Reports are indexed here for navigation only; they are excluded from",
            "public Home listings and from the RAG corpus (`rag_eligible: false`).",
            "",
            "| Title | Type | Status | Transcript | Original |",
            "| --- | --- | --- | --- | --- |",
        ]
    )
    for record in report_records:
        transcript_href = _transcript_href(record.transcript_path)
        original_href = _source_repo_href(record.source_path)
        lines.append(
            "| {title} | {source_type} | {status} | "
            "[transcript]({transcript_href}) | "
            "[original]({original_href}) |".format(
                title=record.title.replace("|", "\\|"),
                source_type=record.source_type,
                status=record.status,
                transcript_href=transcript_href,
                original_href=original_href,
            )
        )
    if not report_records:
        lines.append("| *(none)* | | | | |")
    lines.append("")
    return "\n".join(lines)


def _render_operations() -> str:
    return "\n".join(
        [
            "# Operations",
            "",
            "Back to [Home](Home.md).",
            "",
            "This page documents the local project-knowledge pipeline order.",
            "",
            "## Pipeline order",
            "",
            "1. **Inventory** — discover originals and write the manifest:",
            "   `python tools/wiki_rag/inventory.py --repo-root .`",
            "2. **Conversion** — extract transcripts for in-scope sources:",
            "   `python tools/wiki_rag/extract.py --manifest docs/wiki/_meta/source-manifest.yaml`",
            "3. **Validation** — gate coverage, encoding, and link integrity:",
            "   `python tools/wiki_rag/validate.py --manifest docs/wiki/_meta/source-manifest.yaml`",
            "4. **Curation review** — edit curated pages and verify each claim cites a",
            "   manifest `source_id`.",
            "5. **Wiki build** — regenerate navigation pages:",
            f"   `{_BUILD_COMMAND}`",
            "6. **RAG corpus build** — emit citation-bearing documents for eligible sources:",
            "   `python tools/wiki_rag/build_rag_corpus.py --repo-root . "
            "--manifest docs/wiki/_meta/source-manifest.yaml --output-dir docs/wiki/rag`",
            "   Then validate corpus coverage:",
            "   `python tools/wiki_rag/validate.py --repo-root . "
            "--manifest docs/wiki/_meta/source-manifest.yaml --rag-dir docs/wiki/rag`",
            "7. **Embedding index build** — (Task 7+) index the RAG corpus for backend",
            "   retrieval.",
            "8. **Local backend test** — run FastAPI tests and a smoke chat against the",
            "   refreshed index.",
            "9. **Deployment** — publish backend images and compose stack after local",
            "   validation passes.",
            "",
            "## Source edit policy",
            "",
            "Any change to an original document requires:",
            "",
            "- manifest refresh (inventory and/or conversion),",
            "- transcript regeneration when bytes change,",
            "- validation,",
            "- wiki rebuild, and",
            "- RAG corpus rebuild plus embedding re-index before relying on retrieval.",
            "",
            "## HWP scope",
            "",
            "HWP originals are **owner-skipped** and outside conversion scope.",
            "Pending HWP manifest rows remain for inventory traceability but do not",
            "appear on Home or in the RAG corpus until explicitly converted through",
            "a supported path.",
            "",
        ]
    )


def build_wiki(
    *,
    manifest: Path,
    wiki_root: Path,
) -> None:
    """Render Home, source indexes, curated pages, and operations docs."""

    manifest_path = manifest.resolve()
    resolved_wiki_root = wiki_root.resolve()

    _, records = _load_manifest_records(manifest_path)
    records_by_id = {record.source_id: record for record in records}
    _validate_curated_citations(records_by_id)
    public_records = [record for record in records if _is_public_nav_source(record)]

    resolved_wiki_root.mkdir(parents=True, exist_ok=True)

    pages: dict[str, str] = {
        "Home.md": _render_home(records=records, public_records=public_records),
        "Game-Overview.md": _render_game_overview(records_by_id),
        "Story-and-World.md": _render_story_and_world(records_by_id),
        "Rooms-and-Progression.md": _render_rooms_and_progression(records_by_id),
        "AI-and-Dialogue.md": _render_ai_and_dialogue(records_by_id),
        "Architecture.md": _render_architecture(records_by_id),
        "Development-History.md": _render_development_history(
            records,
            records_by_id,
        ),
        "OPERATIONS.md": _render_operations(),
    }

    for category, label in _CATEGORY_LABELS.items():
        if category == "report":
            continue
        category_records = [
            record for record in public_records if record.category == category
        ]
        pages[f"Source-Index-{label}.md"] = _render_source_index(
            category=category,
            label=label,
            records=category_records,
        )

    for filename, content in pages.items():
        output_path = resolved_wiki_root / filename
        output_path.write_text(content, encoding="utf-8", newline="\n")


def _parse_args(arguments: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Build curated wiki navigation pages from the source manifest.",
    )
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--wiki-root", type=Path, required=True)
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path("."),
        help="Repository root for resolving manifest-relative paths.",
    )
    return parser.parse_args(arguments)


def main(arguments: Sequence[str] | None = None) -> int:
    """CLI entrypoint for wiki generation."""

    args = _parse_args(arguments)
    repo_root = args.repo_root.resolve()
    manifest_path = args.manifest
    if not manifest_path.is_absolute():
        manifest_path = repo_root / manifest_path
    wiki_root = args.wiki_root
    if not wiki_root.is_absolute():
        wiki_root = repo_root / wiki_root

    build_wiki(
        manifest=manifest_path,
        wiki_root=wiki_root,
    )
    print(f"Wiki pages written to {wiki_root.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

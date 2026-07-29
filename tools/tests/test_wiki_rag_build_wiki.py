from __future__ import annotations

import hashlib
import re
from pathlib import Path

import pytest
import yaml
from wiki_rag.build_wiki import build_wiki

_CITATION_PATTERN = re.compile(
    r"\(\[source_id: ([^\]]+)\]\((sources/[^)]+)\)\)"
)

_CURATED_CITATION_FIXTURES: tuple[tuple[str, str, str, str], ...] = (
    ("scenario:93cff884e57e", "시나리오/world.pdf", "scenario", "world-lore"),
    ("scenario:31ea9031cf8f", "시나리오/characters.pdf", "scenario", "characters"),
    ("planning:35ada8161577", "기획서/concept.pdf", "planning", "concept"),
    ("planning:e4e36660bb79", "기획서/opening.pdf", "planning", "opening"),
    ("planning:a54025e67028", "기획서/second-floor.pdf", "planning", "second-floor"),
    ("planning:47f3be566f34", "기획서/basement.pdf", "planning", "basement"),
    ("planning:b98bbfbdb019", "기획서/ai-dialogue.pdf", "planning", "ai-dialogue"),
    ("planning:9d4611de3ae3", "기획서/initial-plan.pdf", "planning", "initial-plan"),
    ("technical:e52de73281b4", "docs/fungus-room-migration-plan.md", "technical", "fungus-room"),
    ("technical:505bbb50868b", "docs/architecture.md", "technical", "architecture"),
    ("technical:ca17d157de10", "docs/security/llm-abuse-defense-plan.md", "technical", "llm-defense"),
)


def _source_record(
    *,
    source_id: str,
    source_path: str,
    category: str,
    title: str,
    status: str,
    source_type: str = "pdf",
    rag_eligible: bool = True,
) -> dict[str, object]:
    content = f"{source_id}:{title}".encode()
    source_sha256 = hashlib.sha256(content).hexdigest()
    slug = title.lower().replace(" ", "-")
    return {
        "source_id": source_id,
        "source_path": source_path,
        "source_sha256": source_sha256,
        "source_type": source_type,
        "category": category,
        "title": title,
        "transcript_path": (
            f"docs/wiki/sources/{category}/{slug}--{source_sha256[:12]}.md"
        ),
        "status": status,
        "rag_eligible": rag_eligible,
        "canonical_group": f"{category}:{slug}",
    }


def _curated_citation_records() -> list[dict[str, object]]:
    return [
        _source_record(
            source_id=source_id,
            source_path=source_path,
            category=category,
            title=title,
            status="extracted",
            source_type="md" if source_path.endswith(".md") else "pdf",
        )
        for source_id, source_path, category, title in _CURATED_CITATION_FIXTURES
    ]


def sample_manifest(tmp_path: Path) -> Path:
    manifest_path = tmp_path / "source-manifest.yaml"
    manifest_data = {
        "schema_version": 1,
        "inputs": {
            "roots": ["시나리오", "기획서", "보고서"],
            "root_sources": [],
            "technical_sources": [],
            "included_extensions": ["pdf", "hwp"],
            "exclusions": ["tools/**"],
        },
        "sources": [
            *_curated_citation_records(),
            _source_record(
                source_id="planning:blocked001",
                source_path="기획서/blocked.hwp",
                category="planning",
                title="blocked_hwp_com",
                status="blocked_hwp_com",
                source_type="hwp",
                rag_eligible=False,
            ),
            _source_record(
                source_id="report:pending001",
                source_path="보고서/weekly.hwp",
                category="report",
                title="weekly-report",
                status="pending",
                source_type="hwp",
                rag_eligible=False,
            ),
            _source_record(
                source_id="report:72ed55d40f51",
                source_path="미니게임_구현_리포트.md",
                category="report",
                title="minigame-report",
                status="extracted",
                source_type="md",
                rag_eligible=False,
            ),
        ],
    }
    manifest_path.write_text(
        yaml.safe_dump(manifest_data, allow_unicode=True, sort_keys=False),
        encoding="utf-8",
        newline="\n",
    )
    return manifest_path


def _expected_transcript_href(transcript_path: str) -> str:
    normalized = transcript_path.replace("\\", "/")
    prefix = "docs/wiki/"
    if normalized.startswith(prefix):
        return normalized[len(prefix) :]
    return normalized


def _manifest_hrefs_by_source_id(manifest_path: Path) -> dict[str, str]:
    manifest_data = yaml.safe_load(manifest_path.read_text(encoding="utf-8"))
    sources = manifest_data["sources"]
    return {
        str(source["source_id"]): _expected_transcript_href(str(source["transcript_path"]))
        for source in sources
        if isinstance(source, dict)
    }


def test_home_lists_only_extracted_or_reviewable_sources(
    tmp_path: Path,
) -> None:
    build_wiki(manifest=sample_manifest(tmp_path), wiki_root=tmp_path)
    home = (tmp_path / "Home.md").read_text(encoding="utf-8")

    assert "Scenario" in home
    assert "blocked_hwp_com" not in home
    assert "weekly-report" not in home
    assert "weekly.hwp" not in home
    assert "minigame-report" not in home
    assert "report:72ed55d40f51" not in home


def test_home_excludes_pending_hwp_by_source_id(tmp_path: Path) -> None:
    build_wiki(manifest=sample_manifest(tmp_path), wiki_root=tmp_path)
    home = (tmp_path / "Home.md").read_text(encoding="utf-8")

    assert "report:pending001" not in home
    assert "planning:blocked001" not in home


def test_curated_page_citations_match_manifest_hrefs(tmp_path: Path) -> None:
    manifest_path = sample_manifest(tmp_path)
    build_wiki(manifest=manifest_path, wiki_root=tmp_path)
    page = (tmp_path / "Story-and-World.md").read_text(encoding="utf-8")
    hrefs_by_id = _manifest_hrefs_by_source_id(manifest_path)

    citations = _CITATION_PATTERN.findall(page)
    assert citations, "expected at least one grounded citation on Story-and-World"

    for source_id, href in citations:
        assert source_id in hrefs_by_id
        assert href == hrefs_by_id[source_id]
        assert href.startswith("sources/")


def test_curated_pages_use_expected_citation_format(tmp_path: Path) -> None:
    build_wiki(manifest=sample_manifest(tmp_path), wiki_root=tmp_path)

    curated_pages = (
        "Game-Overview.md",
        "Story-and-World.md",
        "Rooms-and-Progression.md",
        "AI-and-Dialogue.md",
        "Architecture.md",
        "Development-History.md",
    )
    for filename in curated_pages:
        page = (tmp_path / filename).read_text(encoding="utf-8")
        citations = _CITATION_PATTERN.findall(page)
        assert citations, f"{filename} should contain grounded citations"
        for source_id, href in citations:
            assert source_id
            assert href.startswith("sources/")


def test_build_wiki_fails_on_missing_curated_citation_ids(tmp_path: Path) -> None:
    manifest_path = sample_manifest(tmp_path)
    manifest_data = yaml.safe_load(manifest_path.read_text(encoding="utf-8"))
    manifest_data["sources"] = [
        source
        for source in manifest_data["sources"]
        if source["source_id"] != "scenario:31ea9031cf8f"
    ]
    manifest_path.write_text(
        yaml.safe_dump(manifest_data, allow_unicode=True, sort_keys=False),
        encoding="utf-8",
        newline="\n",
    )

    with pytest.raises(ValueError, match="scenario:31ea9031cf8f"):
        build_wiki(manifest=manifest_path, wiki_root=tmp_path)


def test_reports_listed_only_in_development_history(tmp_path: Path) -> None:
    build_wiki(manifest=sample_manifest(tmp_path), wiki_root=tmp_path)
    dev_history = (tmp_path / "Development-History.md").read_text(
        encoding="utf-8",
    )
    home = (tmp_path / "Home.md").read_text(encoding="utf-8")

    assert "## Internal materials" in dev_history
    assert "minigame-report" in dev_history
    assert "Source Index — Report" not in home

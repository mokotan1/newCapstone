from __future__ import annotations

import hashlib
from pathlib import Path

import yaml

from wiki_rag.build_wiki import build_wiki


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
    content = f"{source_id}:{title}".encode("utf-8")
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
            _source_record(
                source_id="scenario:93cff884e57e",
                source_path="시나리오/world.pdf",
                category="scenario",
                title="world-lore",
                status="extracted",
            ),
            _source_record(
                source_id="planning:35ada8161577",
                source_path="기획서/concept.pdf",
                category="planning",
                title="concept",
                status="needs_review",
            ),
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


def test_curated_page_claim_has_source_link(tmp_path: Path) -> None:
    build_wiki(manifest=sample_manifest(tmp_path), wiki_root=tmp_path)
    page = (tmp_path / "Story-and-World.md").read_text(encoding="utf-8")

    assert "source_id:" in page


def test_reports_listed_only_in_development_history(tmp_path: Path) -> None:
    build_wiki(manifest=sample_manifest(tmp_path), wiki_root=tmp_path)
    dev_history = (tmp_path / "Development-History.md").read_text(
        encoding="utf-8",
    )
    home = (tmp_path / "Home.md").read_text(encoding="utf-8")

    assert "## Internal materials" in dev_history
    assert "minigame-report" in dev_history
    assert "Source Index — Report" not in home

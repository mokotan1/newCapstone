"""Tutor RAG locale filtering with Korean fallback and empty-index safety."""

from __future__ import annotations

import json
from pathlib import Path

from services.tutor_rag_service import TutorRAGService


def _write_index(path: Path, chunks: list[dict]) -> None:
    path.write_text(
        json.dumps({"embedding_model": "test", "chunks": chunks}, ensure_ascii=False),
        encoding="utf-8",
    )


def _sample_chunk(**overrides: object) -> dict:
    base = {
        "id": "scenario:abc123:world-lore:0",
        "text": "world lore body",
        "locale": "ko",
        "embedding": [1.0, 0.0],
        "source_id": "scenario:abc123",
        "source_path": "시나리오/world.pdf",
        "category": "scenario",
        "title": "World Setting",
    }
    base.update(overrides)
    return base


def test_empty_index_returns_empty_block(tmp_path: Path) -> None:
    idx = tmp_path / "empty.json"
    _write_index(idx, [])
    svc = TutorRAGService(idx, api_key="", embedding_model="m")
    assert svc.build_context_block("q", top_k=3, max_context_chars=1000, locale="en") == ""


def test_chunks_without_locale_metadata_unchanged(tmp_path: Path) -> None:
    idx = tmp_path / "untagged.json"
    _write_index(
        idx,
        [
            {
                "id": "a",
                "text": "untagged body",
                "embedding": [1.0, 0.0],
                "source_id": "technical:doc1",
                "source_path": "docs/architecture.md",
            },
        ],
    )
    svc = TutorRAGService(idx, api_key="dummy", embedding_model="m", min_similarity=0.0)

    def fake_embed(text: str) -> list[float]:
        return [1.0, 0.0]

    svc._embed_query = fake_embed  # type: ignore[method-assign]
    block = svc.build_context_block("q", top_k=3, max_context_chars=2000, locale="en")
    assert "untagged body" in block
    assert "source_id=technical:doc1" in block
    assert "source_path=docs/architecture.md" in block


def test_build_context_block_en_header_has_no_hangul(tmp_path: Path) -> None:
    idx = tmp_path / "untagged.json"
    _write_index(
        idx,
        [
            _sample_chunk(text="untagged body", locale="ko"),
        ],
    )
    svc = TutorRAGService(idx, api_key="dummy", embedding_model="m", min_similarity=0.0)
    svc._embed_query = lambda text: [1.0, 0.0]  # type: ignore[method-assign]
    block = svc.build_context_block("q", top_k=3, max_context_chars=2000, locale="en")
    assert "untagged body" in block
    assert "참고 자료" not in block
    assert "퀴즈 출제" not in block
    lower = block.lower()
    assert "reference" in lower or "material" in lower
    assert "source_id=scenario:abc123" in block


def test_locale_filter_prefers_en_then_falls_back_to_ko(tmp_path: Path) -> None:
    idx = tmp_path / "loc.json"
    _write_index(
        idx,
        [
            _sample_chunk(
                id="ko1",
                locale="ko",
                text="korean only chunk",
                embedding=[1.0, 0.0],
            ),
            _sample_chunk(
                id="en1",
                locale="en",
                text="english only chunk",
                embedding=[1.0, 0.0],
            ),
        ],
    )
    svc = TutorRAGService(idx, api_key="dummy", embedding_model="m", min_similarity=0.0)
    svc._embed_query = lambda text: [1.0, 0.0]  # type: ignore[method-assign]

    en_block = svc.build_context_block("q", top_k=5, max_context_chars=4000, locale="en")
    assert "english only chunk" in en_block
    assert "korean only chunk" not in en_block

    ja_block = svc.build_context_block("q", top_k=5, max_context_chars=4000, locale="ja")
    assert "korean only chunk" in ja_block
    assert "english only chunk" not in ja_block


def test_min_similarity_threshold_returns_empty_block(tmp_path: Path) -> None:
    idx = tmp_path / "threshold.json"
    _write_index(
        idx,
        [
            _sample_chunk(embedding=[1.0, 0.0]),
        ],
    )
    svc = TutorRAGService(idx, api_key="dummy", embedding_model="m", min_similarity=0.99)
    svc._embed_query = lambda text: [0.0, 1.0]  # type: ignore[method-assign]
    block = svc.build_context_block("q", top_k=3, max_context_chars=2000, locale="ko")
    assert block == ""


def test_project_profile_uses_project_headers_not_quiz(tmp_path: Path) -> None:
    idx = tmp_path / "project.json"
    _write_index(
        idx,
        [
            _sample_chunk(text="world lore body"),
        ],
    )
    svc = TutorRAGService(idx, api_key="dummy", embedding_model="m", min_similarity=0.0)
    svc._embed_query = lambda text: [1.0, 0.0]  # type: ignore[method-assign]
    block = svc.build_context_block(
        "q",
        top_k=3,
        max_context_chars=2000,
        locale="ko",
        rag_profile="project",
    )
    assert "프로젝트 참고 자료" in block
    assert "퀴즈 출제" not in block
    assert "source_id=scenario:abc123" in block


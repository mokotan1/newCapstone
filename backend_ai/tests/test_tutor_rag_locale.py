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
            {"id": "a", "text": "untagged body", "embedding": [1.0, 0.0]},
        ],
    )
    svc = TutorRAGService(idx, api_key="dummy", embedding_model="m")

    def fake_embed(text: str) -> list[float]:
        return [1.0, 0.0]

    svc._embed_query = fake_embed  # type: ignore[method-assign]
    block = svc.build_context_block("q", top_k=3, max_context_chars=2000, locale="en")
    assert "untagged body" in block


def test_build_context_block_en_header_has_no_hangul(tmp_path: Path) -> None:
    idx = tmp_path / "untagged.json"
    _write_index(
        idx,
        [
            {"id": "a", "text": "untagged body", "embedding": [1.0, 0.0]},
        ],
    )
    svc = TutorRAGService(idx, api_key="dummy", embedding_model="m")
    svc._embed_query = lambda text: [1.0, 0.0]  # type: ignore[method-assign]
    block = svc.build_context_block("q", top_k=3, max_context_chars=2000, locale="en")
    assert "untagged body" in block
    assert "참고 자료" not in block
    assert "퀴즈 출제" not in block
    lower = block.lower()
    assert "reference" in lower or "material" in lower


def test_locale_filter_prefers_en_then_falls_back_to_ko(tmp_path: Path) -> None:
    idx = tmp_path / "loc.json"
    _write_index(
        idx,
        [
            {
                "id": "ko1",
                "locale": "ko",
                "text": "korean only chunk",
                "embedding": [1.0, 0.0],
            },
            {
                "id": "en1",
                "locale": "en",
                "text": "english only chunk",
                "embedding": [1.0, 0.0],
            },
        ],
    )
    svc = TutorRAGService(idx, api_key="dummy", embedding_model="m")
    svc._embed_query = lambda text: [1.0, 0.0]  # type: ignore[method-assign]

    en_block = svc.build_context_block("q", top_k=5, max_context_chars=4000, locale="en")
    assert "english only chunk" in en_block
    assert "korean only chunk" not in en_block

    ja_block = svc.build_context_block("q", top_k=5, max_context_chars=4000, locale="ja")
    assert "korean only chunk" in ja_block
    assert "english only chunk" not in ja_block

from __future__ import annotations

from pathlib import Path

from config import get_settings
from services.tutor_rag_service import TutorRAGService

_KNOWN_QUERY = "십자가를 창문의 크기와 맞출 것"


def _production_service() -> TutorRAGService:
    settings = get_settings()
    index = Path(__file__).resolve().parents[1] / settings.tutor_rag_index_path
    service = TutorRAGService(index, embedding_model=settings.tutor_embedding_model)
    assert service.enabled
    assert service.prepare_error is None
    return service


def test_known_korean_query_returns_source_id() -> None:
    block = _production_service().build_context_block(
        _KNOWN_QUERY,
        top_k=3,
        max_context_chars=4000,
        locale="ko",
    )
    assert "source_id=" in block
    assert "planning:" in block or "scenario:" in block or "technical:" in block


def test_known_query_ja_locale_keeps_source_id() -> None:
    block = _production_service().build_context_block(
        _KNOWN_QUERY,
        top_k=3,
        max_context_chars=4000,
        locale="ja",
    )
    assert "source_id=" in block


def test_known_query_en_locale_keeps_source_id() -> None:
    block = _production_service().build_context_block(
        _KNOWN_QUERY,
        top_k=3,
        max_context_chars=4000,
        locale="en",
    )
    assert "source_id=" in block

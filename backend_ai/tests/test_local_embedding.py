from __future__ import annotations

import json
from pathlib import Path

from services.local_embedding import DIMENSION, MODEL_ID, embed_text, embed_texts
from services.tutor_rag_service import TutorRAGService


def test_embed_text_is_deterministic_and_fixed_dim() -> None:
    first = embed_text("체셔 앵무")
    second = embed_text("체셔 앵무")
    assert first == second
    assert len(first) == DIMENSION
    assert any(value != 0.0 for value in first)


def test_embed_texts_matches_single() -> None:
    batch = embed_texts(["hello", "체셔"])
    assert batch[0] == embed_text("hello")
    assert batch[1] == embed_text("체셔")


def test_tutor_rag_retrieves_without_google_key(tmp_path: Path) -> None:
    query = "성가의 출처"
    chunk_text = "성가의 출처는 저택 서재의 악보이다."
    payload = {
        "embedding_model": MODEL_ID,
        "chunks": [
            {
                "id": "sample:0",
                "text": chunk_text,
                "locale": "ko",
                "embedding": embed_text(chunk_text),
                "source_id": "sample_corpus",
                "source_path": "data/tutor_rag/sample_corpus.md",
                "category": "tutor",
                "title": "Sample",
            }
        ],
    }
    index_path = tmp_path / "index.json"
    index_path.write_text(json.dumps(payload), encoding="utf-8")
    service = TutorRAGService(
        index_path,
        embedding_model=MODEL_ID,
        min_similarity=0.0,
    )
    block = service.build_context_block(query, top_k=1, max_context_chars=2000, locale="ko")
    assert "source_id=sample_corpus" in block
    assert "성가의 출처" in block


def test_tutor_rag_rejects_cloud_embedding_index(tmp_path: Path) -> None:
    payload = {
        "embedding_model": "models/text-embedding-004",
        "chunks": [
            {
                "id": "a",
                "text": "body",
                "locale": "ko",
                "embedding": [1.0, 0.0],
                "source_id": "cloud",
                "source_path": "x.md",
                "category": "tutor",
                "title": "x",
            }
        ],
    }
    index_path = tmp_path / "cloud.json"
    index_path.write_text(json.dumps(payload), encoding="utf-8")
    service = TutorRAGService(index_path, embedding_model=MODEL_ID)
    assert service.enabled is False
    assert service.prepare_error == "unsupported_embedding_model"


def test_tutor_rag_missing_index_is_prepare_error(tmp_path: Path) -> None:
    service = TutorRAGService(tmp_path / "missing.json", embedding_model=MODEL_ID)
    assert service.enabled is False
    assert service.prepare_error == "index_missing"
    assert service.build_context_block("q", top_k=1, max_context_chars=100, locale="ko") == ""


def test_tutor_rag_corrupt_index_is_prepare_error(tmp_path: Path) -> None:
    path = tmp_path / "corrupt.json"
    path.write_text("{not-json", encoding="utf-8")
    service = TutorRAGService(path, embedding_model=MODEL_ID)
    assert service.enabled is False
    assert service.prepare_error == "corrupt_index"


def test_tutor_rag_dimension_mismatch_is_prepare_error(tmp_path: Path) -> None:
    payload = {
        "embedding_model": MODEL_ID,
        "chunks": [
            {
                "id": "a",
                "text": "body",
                "locale": "ko",
                "embedding": [1.0, 0.0],
                "source_id": "short",
                "source_path": "x.md",
            }
        ],
    }
    path = tmp_path / "dim.json"
    path.write_text(json.dumps(payload), encoding="utf-8")
    service = TutorRAGService(path, embedding_model=MODEL_ID)
    assert service.enabled is False
    assert service.prepare_error == "embedding_dimension_mismatch"

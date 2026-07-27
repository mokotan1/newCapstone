from __future__ import annotations

import json
import logging
import math
from pathlib import Path
from typing import Any

from services.locale_support import normalize_locale

logger = logging.getLogger(__name__)

_TUTOR_CONTEXT_BLOCK_HEADERS: dict[str, str] = {
    "ko": (
        "[참고 자료 — 아래 인용문만 퀴즈 출제·해설·근거로 사용하세요. "
        "없는 내용은 상식으로 보충하지 마세요.]"
    ),
    "ja": (
        "[参考資料 — 以下の引用のみをクイズ出題・解説・根拠に使ってください。"
        "ない内容は常識で補わないでください。]"
    ),
    "en": (
        "[Reference material — Use only the quotes below for quiz questions, "
        "explanations, and evidence. Do not fill gaps with general knowledge.]"
    ),
}

_PROJECT_CONTEXT_BLOCK_HEADERS: dict[str, str] = {
    "ko": (
        "[프로젝트 참고 자료 — 아래 인용문만 세계관·기획·구현 사실의 근거로 사용하세요. "
        "없는 내용은 상식으로 보충하지 마세요.]"
    ),
    "ja": (
        "[プロジェクト参考資料 — 以下の引用のみを世界観・企画・実装の根拠に使ってください。"
        "ない内容は常識で補わないでください。]"
    ),
    "en": (
        "[Project reference — Use only the quotes below as evidence for "
        "world-building, design, and implementation facts. "
        "Do not fill gaps with general knowledge.]"
    ),
}


def _cosine_similarity(a: list[float], b: list[float]) -> float:
    if len(a) != len(b) or not a:
        return 0.0
    dot = sum(x * y for x, y in zip(a, b, strict=True))
    na = math.sqrt(sum(x * x for x in a))
    nb = math.sqrt(sum(y * y for y in b))
    if na == 0 or nb == 0:
        return 0.0
    return dot / (na * nb)


def _truncate_block(text: str, max_chars: int) -> str:
    if len(text) <= max_chars:
        return text
    return text[: max_chars - 3] + "..."


def _chunk_locale(ch: dict[str, Any]) -> str | None:
    raw = ch.get("locale")
    if not isinstance(raw, str) or not raw.strip():
        return None
    return normalize_locale(raw)


class TutorRAGService:
    """Loads precomputed embeddings and retrieves context for tutor queries."""

    def __init__(
        self,
        index_path: Path,
        *,
        api_key: str,
        embedding_model: str,
        min_similarity: float = 0.0,
    ) -> None:
        self._index_path = index_path
        self._api_key = api_key
        self._embedding_model = embedding_model
        self._min_similarity = min_similarity
        self._chunks: list[dict[str, Any]] = []
        self._load_index()

    def _load_index(self) -> None:
        if not self._index_path.is_file():
            logger.warning("Tutor RAG index missing: %s — retrieval disabled", self._index_path)
            self._chunks = []
            return
        try:
            data = json.loads(self._index_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as e:
            logger.error("Failed to load tutor RAG index: %s", e)
            self._chunks = []
            return
        self._chunks = data.get("chunks") or []
        logger.info("Loaded tutor RAG index: %d chunks from %s", len(self._chunks), self._index_path)

    @property
    def enabled(self) -> bool:
        return bool(self._chunks)

    def _embed_query(self, text: str) -> list[float] | None:
        if not self._api_key:
            logger.warning("GOOGLE_API_KEY empty — cannot embed tutor RAG query")
            return None
        try:
            import google.generativeai as genai

            genai.configure(api_key=self._api_key)
            res = genai.embed_content(
                model=self._embedding_model,
                content=text,
                task_type="retrieval_query",
            )
            emb = res.get("embedding")
            if isinstance(emb, list):
                return emb
        except Exception as e:
            logger.error("Gemini embed_query failed: %s", e)
        return None

    def _chunks_for_locale(self, locale: str) -> list[dict[str, Any]]:
        """Filter by chunk ``locale`` metadata when present; else use all chunks.

        When tagged chunks exist but none match ``locale``, fall back to ``ko``.
        Empty index remains empty (does not raise).
        """
        if not self._chunks:
            return []

        tagged = [(ch, _chunk_locale(ch)) for ch in self._chunks]
        any_tagged = any(loc is not None for _, loc in tagged)
        if not any_tagged:
            return list(self._chunks)

        want = normalize_locale(locale)
        matched = [ch for ch, loc in tagged if loc == want]
        if matched:
            return matched
        if want != "ko":
            ko_matched = [ch for ch, loc in tagged if loc == "ko"]
            if ko_matched:
                return ko_matched
        return []

    @staticmethod
    def _format_citation_line(
        rank: int,
        *,
        source_id: str,
        source_path: str,
        score: float,
    ) -> str:
        return (
            f"--- [{rank}] source_id={source_id}, "
            f"source_path={source_path}, score={score:.3f}"
        )

    def build_context_block(
        self,
        query_text: str,
        *,
        top_k: int,
        max_context_chars: int,
        locale: str = "ko",
        min_similarity: float | None = None,
        rag_profile: str = "tutor",
    ) -> str:
        pool = self._chunks_for_locale(locale)
        if not pool:
            return ""

        query_vec = self._embed_query(query_text)
        if query_vec is None:
            return ""

        threshold = self._min_similarity if min_similarity is None else min_similarity

        scored: list[tuple[float, dict[str, Any]]] = []
        for ch in pool:
            emb = ch.get("embedding")
            if not isinstance(emb, list):
                continue
            sim = _cosine_similarity(query_vec, emb)
            if sim >= threshold:
                scored.append((sim, ch))

        if not scored:
            return ""

        scored.sort(key=lambda x: x[0], reverse=True)
        picked = scored[:top_k]

        loc = normalize_locale(locale)
        if rag_profile == "project":
            header = _PROJECT_CONTEXT_BLOCK_HEADERS[loc]
        else:
            header = _TUTOR_CONTEXT_BLOCK_HEADERS[loc]
        lines: list[str] = [header]
        total = len(header)
        for rank, (sim, ch) in enumerate(picked, start=1):
            body = (ch.get("text") or "").strip()
            if not body:
                continue
            source_id = str(ch.get("source_id") or ch.get("id") or f"chunk_{rank}")
            source_path = str(ch.get("source_path") or "unknown")
            citation = self._format_citation_line(
                rank,
                source_id=source_id,
                source_path=source_path,
                score=sim,
            )
            piece = f"{citation}\n{body}"
            if total + len(piece) + 2 > max_context_chars:
                remain = max_context_chars - total - len(citation) - 20
                if remain > 80:
                    piece = f"{citation}\n{_truncate_block(body, remain)}"
                else:
                    break
            lines.append(piece)
            total += len(piece) + 2

        if len(lines) == 1:
            return ""
        return "\n\n".join(lines)

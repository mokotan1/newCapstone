#!/usr/bin/env python3
"""Build tutor_rag_index.json from docs/wiki/rag Markdown corpus with source metadata."""

from __future__ import annotations

import argparse
import json
import re
import sys
import time
from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import yaml

_BACKEND_DIR = Path(__file__).resolve().parent.parent
if str(_BACKEND_DIR) not in sys.path:
    sys.path.insert(0, str(_BACKEND_DIR))

from config import get_settings

ALLOWED_FRONT_MATTER_KEYS: frozenset[str] = frozenset(
    {
        "source_id",
        "source_path",
        "source_sha256",
        "source_type",
        "category",
        "title",
        "status",
        "rag_eligible",
        "related_source_ids",
    }
)

_HEADING_RE = re.compile(r"^(#{1,2})\s+(.+)$", re.MULTILINE)
_DEFAULT_MAX_CHUNK_CHARS = 900
_DEFAULT_LOCALE = "ko"


@dataclass(frozen=True)
class RagChunk:
    id: str
    text: str
    locale: str
    source_id: str
    source_path: str
    category: str
    title: str
    embedding: list[float] | None = None


def _section_slug(heading: str) -> str:
    text = heading.strip().lower()
    text = re.sub(r"[^\w\s-]", "", text, flags=re.UNICODE)
    text = re.sub(r"[\s_]+", "-", text).strip("-")
    return text or "section"


def _split_front_matter(raw: str) -> tuple[dict[str, Any], str]:
    if not raw.startswith("---"):
        raise ValueError("RAG corpus file missing YAML front matter")
    parts = raw.split("---", 2)
    if len(parts) < 3:
        raise ValueError("Malformed YAML front matter")
    meta = yaml.safe_load(parts[1])
    if not isinstance(meta, dict):
        raise ValueError("Front matter must be a YAML mapping")
    body = parts[2].lstrip("\n")
    return meta, body


def _validate_front_matter(meta: dict[str, Any], path: Path) -> dict[str, str]:
    unknown = set(meta) - ALLOWED_FRONT_MATTER_KEYS
    if unknown:
        joined = ", ".join(sorted(unknown))
        raise ValueError(f"{path}: unknown front matter fields: {joined}")

    required = ("source_id", "source_path", "category", "title")
    missing = [key for key in required if not str(meta.get(key) or "").strip()]
    if missing:
        joined = ", ".join(missing)
        raise ValueError(f"{path}: missing required front matter: {joined}")

    return {
        "source_id": str(meta["source_id"]).strip(),
        "source_path": str(meta["source_path"]).strip(),
        "category": str(meta["category"]).strip(),
        "title": str(meta["title"]).strip(),
    }


def _pack_paragraphs(paragraphs: list[str], max_chars: int) -> list[str]:
    chunks: list[str] = []
    buf = ""
    for paragraph in paragraphs:
        paragraph = paragraph.strip()
        if not paragraph:
            continue
        if len(buf) + len(paragraph) + 2 <= max_chars:
            buf = f"{buf}\n\n{paragraph}" if buf else paragraph
            continue
        if buf:
            chunks.append(buf)
        if len(paragraph) <= max_chars:
            buf = paragraph
        else:
            for i in range(0, len(paragraph), max_chars):
                chunks.append(paragraph[i : i + max_chars])
            buf = ""
    if buf:
        chunks.append(buf)
    return chunks


def _split_by_headings(body: str) -> list[tuple[str, str]]:
    sections: list[tuple[str, str]] = []
    matches = list(_HEADING_RE.finditer(body))
    if not matches:
        stripped = body.strip()
        if stripped:
            sections.append(("intro", stripped))
        return sections

    prefix = body[: matches[0].start()].strip()
    if prefix:
        sections.append(("intro", prefix))

    for index, match in enumerate(matches):
        heading = match.group(2).strip()
        start = match.end()
        end = matches[index + 1].start() if index + 1 < len(matches) else len(body)
        content = body[start:end].strip()
        if content:
            sections.append((heading, content))
    return sections


def _chunk_section(
    *,
    source_id: str,
    section_slug: str,
    heading: str,
    body: str,
    metadata: dict[str, str],
    max_chars: int,
    locale: str,
) -> list[RagChunk]:
    prefix = f"## {heading}\n\n" if heading != "intro" else ""
    paragraphs = [p for p in re.split(r"\n\s*\n", body) if p.strip()]
    packed = _pack_paragraphs(paragraphs, max(max_chars - len(prefix), 200))
    out: list[RagChunk] = []
    for ordinal, piece in enumerate(packed):
        text = f"{prefix}{piece}" if prefix else piece
        chunk_id = f"{source_id}:{section_slug}:{ordinal}"
        out.append(
            RagChunk(
                id=chunk_id,
                text=text,
                locale=locale,
                source_id=metadata["source_id"],
                source_path=metadata["source_path"],
                category=metadata["category"],
                title=metadata["title"],
            )
        )
    return out


def load_corpus_chunks(
    corpus_dir: Path,
    *,
    max_chars: int = _DEFAULT_MAX_CHUNK_CHARS,
    locale: str = _DEFAULT_LOCALE,
) -> list[RagChunk]:
    if not corpus_dir.is_dir():
        return []

    chunks: list[RagChunk] = []
    for path in sorted(corpus_dir.glob("*.md")):
        raw = path.read_text(encoding="utf-8")
        meta, body = _split_front_matter(raw)
        metadata = _validate_front_matter(meta, path)
        for heading, section_body in _split_by_headings(body):
            slug = _section_slug(heading)
            chunks.extend(
                _chunk_section(
                    source_id=metadata["source_id"],
                    section_slug=slug,
                    heading=heading,
                    body=section_body,
                    metadata=metadata,
                    max_chars=max_chars,
                    locale=locale,
                )
            )
    return chunks


def _chunk_to_index_dict(chunk: RagChunk, embedding: list[float]) -> dict[str, Any]:
    return {
        "id": chunk.id,
        "text": chunk.text,
        "locale": chunk.locale,
        "embedding": embedding,
        "source_id": chunk.source_id,
        "source_path": chunk.source_path,
        "category": chunk.category,
        "title": chunk.title,
    }


def _validate_index_payload(payload: dict[str, Any], expected_vectors: int) -> None:
    chunks = payload.get("chunks")
    if not isinstance(chunks, list):
        raise ValueError("Index payload missing chunks list")
    if len(chunks) != expected_vectors:
        raise ValueError(
            f"Index vector count mismatch: expected {expected_vectors}, got {len(chunks)}"
        )
    required = {"id", "text", "locale", "embedding", "source_id", "source_path", "category", "title"}
    for index, chunk in enumerate(chunks):
        if not isinstance(chunk, dict):
            raise ValueError(f"Chunk {index} is not an object")
        missing = required - set(chunk)
        if missing:
            joined = ", ".join(sorted(missing))
            raise ValueError(f"Chunk {index} missing fields: {joined}")
        embedding = chunk.get("embedding")
        if not isinstance(embedding, list) or not embedding:
            raise ValueError(f"Chunk {index} has invalid embedding")


def write_index_atomically(
    out_path: Path,
    chunks: list[RagChunk],
    *,
    embedding_model: str,
    embed_fn: Callable[[list[str]], list[list[float]]],
) -> None:
    if not chunks:
        raise ValueError("Refusing to write an empty index")

    texts = [chunk.text for chunk in chunks]
    vectors = embed_fn(texts)
    if len(vectors) != len(chunks):
        raise RuntimeError(
            f"Embedding count mismatch: expected {len(chunks)}, got {len(vectors)}"
        )

    payload = {
        "embedding_model": embedding_model,
        "chunks": [
            _chunk_to_index_dict(chunk, vector)
            for chunk, vector in zip(chunks, vectors, strict=True)
        ],
    }
    _validate_index_payload(payload, expected_vectors=len(chunks))

    tmp_path = out_path.with_suffix(out_path.suffix + ".tmp")
    tmp_path.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")
    tmp_path.replace(out_path)


def _default_embed_batch(
    genai: object,
    model: str,
    texts: list[str],
    *,
    batch_size: int = 8,
    sleep_s: float = 0.2,
) -> list[list[float]]:
    embeddings: list[list[float]] = []
    for i in range(0, len(texts), batch_size):
        batch = texts[i : i + batch_size]
        for text in batch:
            res = genai.embed_content(
                model=model,
                content=text,
                task_type="retrieval_document",
            )
            emb = res.get("embedding")
            if not isinstance(emb, list):
                raise RuntimeError(f"Bad embedding response for chunk starting: {text[:40]!r}")
            embeddings.append(emb)
            time.sleep(sleep_s)
    return embeddings


def main() -> int:
    parser = argparse.ArgumentParser(description="Build project wiki RAG embedding index")
    parser.add_argument("--max-chunk-chars", type=int, default=_DEFAULT_MAX_CHUNK_CHARS)
    parser.add_argument("--corpus-dir", type=Path, default=None)
    parser.add_argument("--output-path", type=Path, default=None)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    settings = get_settings()
    corpus_dir = (
        args.corpus_dir
        if args.corpus_dir is not None
        else (_BACKEND_DIR / settings.tutor_rag_corpus_dir).resolve()
    )
    out_path = (
        args.output_path
        if args.output_path is not None
        else (_BACKEND_DIR / settings.tutor_rag_index_path).resolve()
    )

    chunks = load_corpus_chunks(corpus_dir, max_chars=args.max_chunk_chars)
    if not chunks:
        print(f"No chunks to index under {corpus_dir}", file=sys.stderr)
        return 1

    source_ids = sorted({chunk.source_id for chunk in chunks})
    print(f"Corpus: {corpus_dir}")
    print(f"Chunks: {len(chunks)}")
    print(f"Source IDs ({len(source_ids)}):")
    for source_id in source_ids:
        print(f"  - {source_id}")

    if args.dry_run:
        return 0

    if not settings.google_api_key:
        print("GOOGLE_API_KEY is required in backend_ai/.env", file=sys.stderr)
        return 1

    import google.generativeai as genai

    genai.configure(api_key=settings.google_api_key)
    model = settings.tutor_embedding_model

    def embed_fn(texts: list[str]) -> list[list[float]]:
        return _default_embed_batch(genai, model, texts)

    print(f"Embedding {len(chunks)} chunks with {model} ...")
    write_index_atomically(
        out_path,
        chunks,
        embedding_model=model,
        embed_fn=embed_fn,
    )
    print(f"Wrote {out_path} ({len(chunks)} vectors)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

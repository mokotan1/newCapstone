#!/usr/bin/env python3
"""Build tutor_rag_index.json from data/tutor_rag/* and data/tutor_quiz/quiz_bank.csv."""

from __future__ import annotations

import argparse
import csv
import json
import sys
import time
from pathlib import Path

_BACKEND_DIR = Path(__file__).resolve().parent.parent
if str(_BACKEND_DIR) not in sys.path:
    sys.path.insert(0, str(_BACKEND_DIR))

from config import get_settings  # noqa: E402


def _chunk_text(text: str, max_chars: int) -> list[str]:
    text = text.strip()
    if not text:
        return []
    parts = text.split("\n\n")
    chunks: list[str] = []
    buf = ""
    for p in parts:
        p = p.strip()
        if not p:
            continue
        if len(buf) + len(p) + 2 <= max_chars:
            buf = f"{buf}\n\n{p}" if buf else p
        else:
            if buf:
                chunks.append(buf)
            if len(p) <= max_chars:
                buf = p
            else:
                for i in range(0, len(p), max_chars):
                    chunks.append(p[i : i + max_chars])
                buf = ""
    if buf:
        chunks.append(buf)
    return chunks


def _load_corpus_chunks(corpus_dir: Path, max_chars: int) -> list[tuple[str, str]]:
    out: list[tuple[str, str]] = []
    if not corpus_dir.is_dir():
        return out
    for path in sorted(corpus_dir.rglob("*")):
        if path.suffix.lower() not in (".md", ".txt"):
            continue
        if path.name == "README.md":
            continue
        raw = path.read_text(encoding="utf-8")
        rel = path.relative_to(corpus_dir).as_posix()
        for i, ch in enumerate(_chunk_text(raw, max_chars)):
            cid = f"doc:{rel}:{i}"
            out.append((cid, ch))
    return out


def _load_quiz_chunks(csv_path: Path) -> list[tuple[str, str, str]]:
    """Return (chunk_id, text, locale) triples for each non-empty locale column set."""
    out: list[tuple[str, str, str]] = []
    if not csv_path.is_file():
        return out
    with csv_path.open(encoding="utf-8-sig", newline="") as f:
        reader = csv.DictReader(f)
        for r in reader:
            qid = (r.get("question_id") or "").strip()
            if not qid:
                continue
            qko = (r.get("question_ko") or "").strip()
            ref_ko = (
                r.get("reference_snippet_ko") or r.get("reference_snippet") or ""
            ).strip()
            locales: list[tuple[str, str, str]] = [
                ("ko", qko, ref_ko),
                ("ja", (r.get("question_ja") or "").strip(), (r.get("reference_snippet_ja") or "").strip()),
                ("en", (r.get("question_en") or "").strip(), (r.get("reference_snippet_en") or "").strip()),
            ]
            for loc, qtext, ref in locales:
                body = f"{qtext}\n{ref}".strip() if qtext or ref else ""
                if not body and loc != "ko":
                    continue
                if not body:
                    continue
                out.append((f"quiz:{qid}:{loc}", body, loc))
    return out


def _embed_batch(
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
        for t in batch:
            res = genai.embed_content(
                model=model,
                content=t,
                task_type="retrieval_document",
            )
            emb = res.get("embedding")
            if not isinstance(emb, list):
                raise RuntimeError(f"Bad embedding response for chunk starting: {t[:40]!r}")
            embeddings.append(emb)
            time.sleep(sleep_s)
    return embeddings


def main() -> int:
    parser = argparse.ArgumentParser(description="Build tutor RAG embedding index")
    parser.add_argument("--max-chunk-chars", type=int, default=900)
    args = parser.parse_args()

    settings = get_settings()
    if not settings.google_api_key:
        print("GOOGLE_API_KEY is required in backend_ai/.env", file=sys.stderr)
        return 1

    corpus_dir = (_BACKEND_DIR / settings.tutor_rag_corpus_dir).resolve()
    quiz_csv = (_BACKEND_DIR / settings.tutor_quiz_csv_path).resolve()
    out_path = (_BACKEND_DIR / settings.tutor_rag_index_path).resolve()

    corpus_pairs = [(cid, text, "ko") for cid, text in _load_corpus_chunks(corpus_dir, args.max_chunk_chars)]
    pairs = corpus_pairs + _load_quiz_chunks(quiz_csv)
    if not pairs:
        print("No chunks to index (empty corpus and quiz bank).", file=sys.stderr)
        return 1

    import google.generativeai as genai

    genai.configure(api_key=settings.google_api_key)
    model = settings.tutor_embedding_model
    ids = [p[0] for p in pairs]
    texts = [p[1] for p in pairs]
    locales = [p[2] for p in pairs]

    print(f"Embedding {len(texts)} chunks with {model} ...")
    vectors = _embed_batch(genai, model, texts)

    payload = {
        "embedding_model": model,
        "chunks": [
            {"id": i, "text": t, "locale": loc, "embedding": e}
            for i, t, loc, e in zip(ids, texts, locales, vectors, strict=True)
        ],
    }
    out_path.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")
    print(f"Wrote {out_path} ({len(vectors)} vectors)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

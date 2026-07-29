"""Project wiki RAG index builder: metadata, chunking, and atomic writes."""

from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest

_SCRIPTS_DIR = Path(__file__).resolve().parent.parent / "scripts"
if str(_SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPTS_DIR))

from build_tutor_rag_index import (
    load_corpus_chunks,
    write_index_atomically,
)


def _write_sample_corpus(corpus_dir: Path) -> None:
    corpus_dir.mkdir(parents=True, exist_ok=True)
    (corpus_dir / "sample.md").write_text(
        """---
source_id: scenario:abc123
source_path: 시나리오/world.pdf
source_sha256: abc123deadbeef
source_type: pdf
category: scenario
title: World Setting
status: extracted
rag_eligible: true
---

## World Lore

First paragraph about the mansion.

Second paragraph with more detail about the world.

## Characters

Alice enters the story here.
""",
        encoding="utf-8",
    )


def test_builder_preserves_wiki_source_metadata(tmp_path: Path) -> None:
    _write_sample_corpus(tmp_path)
    chunks = load_corpus_chunks(tmp_path)
    assert chunks
    assert chunks[0].source_id == "scenario:abc123"
    assert chunks[0].source_path == "시나리오/world.pdf"
    assert chunks[0].category == "scenario"
    assert chunks[0].title == "World Setting"
    assert chunks[0].id.startswith("scenario:abc123:")


def test_builder_rejects_unknown_front_matter_fields(tmp_path: Path) -> None:
    _write_sample_corpus(tmp_path)
    bad = tmp_path / "bad.md"
    bad.write_text(
        """---
source_id: scenario:bad001
source_path: 시나리오/bad.pdf
source_sha256: deadbeef
source_type: pdf
category: scenario
title: Bad Doc
status: extracted
rag_eligible: true
injected_field: untrusted
---

Body text.
""",
        encoding="utf-8",
    )
    with pytest.raises(ValueError, match="unknown front matter"):
        load_corpus_chunks(tmp_path)


def test_builder_heading_aware_chunk_ids_are_stable(tmp_path: Path) -> None:
    _write_sample_corpus(tmp_path)
    chunks = load_corpus_chunks(tmp_path, max_chars=900)
    ids = [chunk.id for chunk in chunks]
    assert "scenario:abc123:world-lore:0" in ids
    assert "scenario:abc123:characters:0" in ids
    assert all(chunk.text for chunk in chunks)


def test_builder_does_not_replace_existing_index_when_embedding_fails(tmp_path: Path) -> None:
    _write_sample_corpus(tmp_path)
    chunks = load_corpus_chunks(tmp_path)
    old = tmp_path / "index.json"
    old.write_text('{"chunks":["old"]}', encoding="utf-8")

    def embed_fn(texts: list[str]) -> list[list[float]]:
        raise RuntimeError("embedding failed")

    with pytest.raises(RuntimeError, match="embedding failed"):
        write_index_atomically(
            old,
            chunks,
            embedding_model="test-model",
            embed_fn=embed_fn,
        )
    assert old.read_text(encoding="utf-8") == '{"chunks":["old"]}'


def test_write_index_atomically_writes_metadata_rich_chunks(tmp_path: Path) -> None:
    _write_sample_corpus(tmp_path)
    chunks = load_corpus_chunks(tmp_path)
    out = tmp_path / "index.json"

    def embed_fn(texts: list[str]) -> list[list[float]]:
        return [[float(i), 1.0] for i, _ in enumerate(texts)]

    write_index_atomically(
        out,
        chunks,
        embedding_model="test-model",
        embed_fn=embed_fn,
    )
    payload = json.loads(out.read_text(encoding="utf-8"))
    assert payload["embedding_model"] == "test-model"
    saved = payload["chunks"]
    assert saved
    first = saved[0]
    assert first["source_id"] == "scenario:abc123"
    assert first["source_path"] == "시나리오/world.pdf"
    assert first["category"] == "scenario"
    assert first["title"] == "World Setting"
    assert isinstance(first["embedding"], list)

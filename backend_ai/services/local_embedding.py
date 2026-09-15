"""Deterministic local embeddings shared by index build and query time."""

from __future__ import annotations

import hashlib
import math

MODEL_ID = "local-hash-v1"
DIMENSION = 256
_NGRAM = 3


def embed_text(text: str) -> list[float]:
    vector = [0.0] * DIMENSION
    padded = f"  {text.casefold()}  "
    if len(padded) < _NGRAM:
        return vector
    for index in range(len(padded) - _NGRAM + 1):
        gram = padded[index : index + _NGRAM]
        digest = hashlib.blake2b(gram.encode("utf-8"), digest_size=8).digest()
        bucket = int.from_bytes(digest[:4], "little") % DIMENSION
        sign = 1.0 if digest[4] % 2 == 0 else -1.0
        vector[bucket] += sign
    norm = math.sqrt(sum(value * value for value in vector))
    if norm == 0.0:
        return vector
    return [value / norm for value in vector]


def embed_texts(texts: list[str]) -> list[list[float]]:
    return [embed_text(text) for text in texts]

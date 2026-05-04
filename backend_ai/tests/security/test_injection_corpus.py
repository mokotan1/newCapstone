from __future__ import annotations

from pathlib import Path

import yaml
import pytest

from llm_defense.input_sanitize import sanitize_llm_text

_CORPUS = Path(__file__).parent / "injection_corpus.yaml"


def _load_items() -> list[dict]:
    data = yaml.safe_load(_CORPUS.read_text(encoding="utf-8"))
    assert isinstance(data, list)
    return data


@pytest.mark.parametrize("item", _load_items(), ids=lambda x: x.get("id", "?"))
def test_corpus_payloads_sanitize_cleanly(item: dict) -> None:
    if "payload" in item:
        raw = str(item["payload"])
    else:
        tpl = str(item.get("payload-template", ""))
        size = int(item.get("filler_size", 0))
        raw = tpl.replace("{{FILLER}}", "x" * size)
    out = sanitize_llm_text(raw, max_chars=10_000)
    assert "\u200b" not in out
    assert "\u202e" not in out and "\u202c" not in out

from __future__ import annotations

from llm_defense.input_sanitize import sanitize_llm_text


def test_removes_zero_width() -> None:
    raw = "안녕\u200b\u200c키"
    out = sanitize_llm_text(raw, max_chars=100)
    assert "\u200b" not in out
    assert "안녕" in out
    assert "키" in out


def test_removes_bidi_markers() -> None:
    raw = "\u202e" "evil\u202c"
    out = sanitize_llm_text(raw, max_chars=100)
    assert "\u202e" not in out


def test_truncates_to_max_chars() -> None:
    long = "a" * 50
    out = sanitize_llm_text(long, max_chars=10)
    assert len(out) == 10
    assert out.endswith("…")

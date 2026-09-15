"""Sanitize Cheshire dialogue so players never see empty, JSON, or overlong lines."""

from __future__ import annotations

import re

from services.locale_support import normalize_locale

_MAX_SENTENCES = 2
# ChesterVoiceCommon counts sentences by period only. `!`/`?` are mannerisms
# (`깍!`, `켁!`) and must not trip the length guard.
_PERIOD = re.compile(r"[.。]+")
_TOOL_MARKERS = ("give_hint", "emote", "update_quiz")

_DIALOGUE_FALLBACK: dict[str, str] = {
    "ko": "지금은 짧게만 말할게. 다시 물어봐 줘.",
    "ja": "今は短くだけ話すよ。もう一度聞いてね。",
    "en": "I'll keep it short. Ask me again.",
}


def dialogue_fallback_line(locale: str = "ko") -> str:
    """Game-authored line used when the model output is not a valid short reply."""
    return _DIALOGUE_FALLBACK[normalize_locale(locale)]


def sanitize_dialogue_reply(text: str, locale: str = "ko") -> str:
    """Keep a 1–2 period-sentence player line; clip extras; fallback for empty/JSON."""
    stripped = (text or "").strip()
    if not stripped:
        return dialogue_fallback_line(locale)
    if _looks_like_json_or_tool(stripped):
        return dialogue_fallback_line(locale)
    if _sentence_count(stripped) > _MAX_SENTENCES:
        clipped = _first_n_period_sentences(stripped, _MAX_SENTENCES)
        return clipped if clipped else dialogue_fallback_line(locale)
    return stripped


def looks_like_json_or_tool(text: str) -> bool:
    """True when the line looks like a tool call or JSON blob, not a player line."""
    return _looks_like_json_or_tool((text or "").strip())


def dialogue_sentence_count(text: str) -> int:
    """Count sentences using the same splitter as the dialogue guard."""
    return _sentence_count((text or "").strip())


def _looks_like_json_or_tool(text: str) -> bool:
    if text.startswith(("{", "[")):
        return True
    lowered = text.lower()
    if "```json" in lowered:
        return True
    return any(marker in lowered for marker in _TOOL_MARKERS) and (
        "{" in text or "(" in text
    )


def _sentence_count(text: str) -> int:
    if not text:
        return 0
    n = len(_PERIOD.findall(text))
    return n if n > 0 else 1


def _first_n_period_sentences(text: str, n: int) -> str:
    for count, match in enumerate(_PERIOD.finditer(text), start=1):
        if count >= n:
            return text[: match.end()].strip()
    return text.strip()

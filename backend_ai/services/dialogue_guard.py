"""Sanitize Cheshire dialogue so players never see empty, JSON, or overlong lines."""

from __future__ import annotations

import re

from services.locale_support import normalize_locale

_MAX_SENTENCES = 2
_SENTENCE_SPLIT = re.compile(r"[.!?。！？]+")
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
    """Return ``text`` if it is a 1–2 sentence player line; otherwise a locale fallback."""
    stripped = (text or "").strip()
    if not stripped:
        return dialogue_fallback_line(locale)
    if _looks_like_json_or_tool(stripped):
        return dialogue_fallback_line(locale)
    if _sentence_count(stripped) > _MAX_SENTENCES:
        return dialogue_fallback_line(locale)
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
    parts = [part.strip() for part in _SENTENCE_SPLIT.split(text) if part.strip()]
    return len(parts)

"""Canonical locale helpers mirroring Unity ``CheshireLocaleResolver``."""

from __future__ import annotations

from typing import Any

_DEFAULT_LOCALE = "ko"

_PLAYER_MSG_ALL_ENGINES_FAILED: dict[str, str] = {
    "ko": "모든 AI 엔진 실패",
    "ja": "すべてのAIエンジンが失敗しました",
    "en": "All AI engines failed",
}

_PLAYER_MSG_API_KEY_REQUIRED: dict[str, str] = {
    "ko": "API 키 설정 필요",
    "ja": "APIキーの設定が必要です",
    "en": "API key configuration required",
}

_PLAYER_MSG_RATE_LIMIT: dict[str, str] = {
    "ko": "AI 사용 한도에 도달했습니다. 잠시 후 다시 시도해 주세요.",
    "ja": "AIの利用上限に達しました。しばらくしてからもう一度お試しください。",
    "en": "AI usage limit reached. Please try again later.",
}

_RESPONSE_LANGUAGE_INSTRUCTION: dict[str, str] = {
    "ko": "Respond to the player only in Korean.",
    "ja": "Respond to the player only in Japanese.",
    "en": "Respond to the player only in English.",
}


def normalize_locale(raw: Any) -> str:
    """Map aliases / BCP-47 tags to ``ko`` | ``ja`` | ``en``; default ``ko``.

    Mirrors ``CheshireLocaleResolver.NormalizeLocale`` (strip region, lower-case,
    ko/kr/korean, ja/jp/japanese, en/english; unknown → ko).
    """
    if raw is None:
        return _DEFAULT_LOCALE
    if not isinstance(raw, str):
        return _DEFAULT_LOCALE

    s = raw.strip()
    if not s:
        return _DEFAULT_LOCALE

    for sep in ("-", "_"):
        idx = s.find(sep)
        if idx > 0:
            s = s[:idx]
            break

    s = s.strip().lower()

    if s in ("ko", "kr", "korean"):
        return "ko"
    if s in ("ja", "jp", "japanese"):
        return "ja"
    if s in ("en", "english"):
        return "en"
    return _DEFAULT_LOCALE


def response_language_instruction(locale: str) -> str:
    """Trusted system-channel line: model must reply in the player locale."""
    key = normalize_locale(locale)
    return _RESPONSE_LANGUAGE_INSTRUCTION[key]


def api_key_required_message(locale: str = _DEFAULT_LOCALE) -> str:
    """Player-facing HTTP detail when no AI provider API keys are configured."""
    key = normalize_locale(locale)
    return _PLAYER_MSG_API_KEY_REQUIRED[key]


def all_engines_failed_message(locale: str = _DEFAULT_LOCALE) -> str:
    """Player-facing HTTP detail when every AI provider failed."""
    key = normalize_locale(locale)
    return _PLAYER_MSG_ALL_ENGINES_FAILED[key]


def user_visible_ai_error(
    last_error: BaseException | None,
    locale: str = _DEFAULT_LOCALE,
) -> str:
    """Map provider exceptions to a short player-facing string for ``locale``."""
    key = normalize_locale(locale)
    if last_error is None:
        return _PLAYER_MSG_ALL_ENGINES_FAILED[key]
    raw = str(last_error)
    lower = raw.lower()
    if "429" in raw or "rate_limit" in lower or "too many requests" in lower:
        return _PLAYER_MSG_RATE_LIMIT[key]
    return _PLAYER_MSG_ALL_ENGINES_FAILED[key]

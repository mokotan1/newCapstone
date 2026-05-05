from __future__ import annotations

import re
import unicodedata

# Zero-width and format characters often used to hide payloads.
_ZERO_WIDTH_AND_FORMAT = frozenset(
    {
        "\u200b",  # ZWSP
        "\u200c",  # ZWNJ
        "\u200d",  # ZWJ
        "\u2060",  # WORD JOINER
        "\ufeff",  # BOM
        "\u00ad",  # SOFT HYPHEN
    }
)

# Bidi / direction overrides (common injection trick).
_BIDI_CONTROLS = frozenset(
    {
        "\u202a",
        "\u202b",
        "\u202c",
        "\u202d",
        "\u202e",
        "\u2066",
        "\u2067",
        "\u2068",
        "\u2069",
    }
)

# Matches other Cc except tab/newline; we preserve \n \t for natural text.
_CC_STRIP_REGEX = re.compile(r"[\x00-\x08\x0b\x0c\x0e-\x1f\x7f]")


def sanitize_llm_text(text: str, *, max_chars: int) -> str:
    """
    Normalize untrusted LLM-bound text on the server.
    Does not decode base64 or HTML — per defense plan (hidden payload patterns).
    """
    if max_chars <= 0:
        return ""
    s = text if isinstance(text, str) else ""
    s = unicodedata.normalize("NFC", s)
    out_chars: list[str] = []
    for ch in s:
        cat = unicodedata.category(ch)
        if ch in _ZERO_WIDTH_AND_FORMAT:
            continue
        if ch in _BIDI_CONTROLS:
            continue
        if cat == "Cf":  # other format chars
            continue
        out_chars.append(ch)
    folded = "".join(out_chars)
    folded = _CC_STRIP_REGEX.sub("", folded)
    if len(folded) > max_chars:
        return folded[: max_chars - 1] + "…"
    return folded

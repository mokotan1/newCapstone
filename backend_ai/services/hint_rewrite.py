from __future__ import annotations

import json

from models.requests import HintRewritePayload

HINT_REWRITE_SERVER_INSTRUCTION = """
[Cheshire hint rewrite policy]
You rewrite only the provided base hint in Cheshire's voice.
Do not solve the puzzle yourself.
Do not invent new places, items, rewards, passwords, or facts.
Preserve the base_hint meaning.
Never reveal forbidden terms.
Return a short player-facing line, not analysis.
""".strip()


def build_hint_rewrite_external_document(payload: HintRewritePayload) -> str:
    data = payload.model_dump(exclude_none=True)
    return json.dumps(data, ensure_ascii=False, sort_keys=True)


def _fallback(payload: HintRewritePayload) -> str:
    if payload.fallback_line and payload.fallback_line.strip():
        return payload.fallback_line.strip()
    return payload.base_hint.strip()


def apply_hint_rewrite_fallback(line: str, payload: HintRewritePayload) -> str:
    text = (line or "").strip()
    if not text:
        return _fallback(payload)

    for forbidden in payload.forbidden_terms:
        term = (forbidden or "").strip()
        if term and term in text:
            return _fallback(payload)

    for required in payload.required_terms:
        term = (required or "").strip()
        if term and term not in text:
            return _fallback(payload)

    if len(text) > 300:
        return _fallback(payload)

    return text

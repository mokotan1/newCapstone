from __future__ import annotations

from models.requests import HintRewritePayload
from services.hint_rewrite import (
    HINT_REWRITE_SERVER_INSTRUCTION,
    apply_hint_rewrite_fallback,
    build_hint_rewrite_external_document,
)


def _payload() -> HintRewritePayload:
    return HintRewritePayload(
        hint_id="opaque_bottle_sink_use",
        item_id="opaque_bottle",
        hint_target="kitchen_sink",
        hint_level="direct",
        base_hint="이 병은 주방 싱크대에서 사용할 수 있다.",
        required_terms=["병", "싱크대"],
        forbidden_terms=["열쇠"],
        fallback_line="그 병은 주방 싱크대에서 물을 채워볼 수 있다.",
    )


def test_instruction_requires_rewrite_only() -> None:
    assert "rewrite only" in HINT_REWRITE_SERVER_INSTRUCTION
    assert "Do not solve" in HINT_REWRITE_SERVER_INSTRUCTION


def test_external_document_contains_payload_but_not_policy() -> None:
    doc = build_hint_rewrite_external_document(_payload())

    assert "opaque_bottle_sink_use" in doc
    assert "이 병은 주방 싱크대에서 사용할 수 있다." in doc
    assert "trusted" not in doc.lower()


def test_valid_line_is_preserved() -> None:
    line = "병은 목마르다. 싱크대가 기억한다."

    assert apply_hint_rewrite_fallback(line, _payload()) == line


def test_forbidden_term_uses_fallback() -> None:
    line = "싱크대에서 열쇠를 꺼내."

    assert apply_hint_rewrite_fallback(line, _payload()) == "그 병은 주방 싱크대에서 물을 채워볼 수 있다."


def test_missing_required_term_uses_fallback() -> None:
    line = "물이 기억한다."

    assert apply_hint_rewrite_fallback(line, _payload()) == "그 병은 주방 싱크대에서 물을 채워볼 수 있다."

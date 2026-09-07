from __future__ import annotations

import pytest

from models.requests import ChatRequest


def test_prompt_only() -> None:
    r = ChatRequest(prompt="hello", system="s")
    assert r.prompt == "hello"


def test_message_only_fills_prompt() -> None:
    r = ChatRequest(message="from message", system="s")
    assert r.prompt == "from message"


def test_user_id_optional_and_ignored_in_core_model() -> None:
    r = ChatRequest(prompt="a", system="s", user_id="u-1")
    assert r.user_id == "u-1"
    assert r.prompt == "a"


def test_extra_fields_ignored() -> None:
    r = ChatRequest.model_validate(
        {"prompt": "x", "system": "s", "some_legacy": 1},
    )
    assert r.prompt == "x"


def test_empty_prompt_and_message_raises() -> None:
    with pytest.raises(Exception):
        ChatRequest.model_validate({"system": "s", "prompt": "", "message": ""})


def test_cheshire_dialogue_is_dialogue_only() -> None:
    request = ChatRequest(prompt="안녕", system="체셔", use_tools=True)
    assert request.is_dialogue_only is True


def test_tutor_profile_is_not_dialogue_only() -> None:
    request = ChatRequest(prompt="골리앗", rag_profile="tutor", use_tools=True)
    assert request.is_dialogue_only is False


def test_character_facts_and_dialogue_context_are_optional() -> None:
    request = ChatRequest(
        prompt="힌트 줄래?",
        character_facts="서재 열쇠는 이미 사용됨",
        dialogue_context="플레이어가 같은 질문을 세 번 반복함",
    )
    assert request.character_facts == "서재 열쇠는 이미 사용됨"
    assert request.dialogue_context == "플레이어가 같은 질문을 세 번 반복함"


def test_hint_rewrite_payload_is_accepted() -> None:
    r = ChatRequest.model_validate(
        {
            "prompt": "이 병 어디다 써?",
            "system": "client scene",
            "hint_rewrite": {
                "hint_id": "opaque_bottle_sink_use",
                "item_id": "opaque_bottle",
                "hint_target": "kitchen_sink",
                "hint_level": "direct",
                "base_hint": "이 병은 주방 싱크대에서 사용할 수 있다.",
                "required_terms": ["병", "싱크대"],
                "forbidden_terms": ["열쇠"],
                "fallback_line": "그 병은 주방 싱크대에서 물을 채워볼 수 있다.",
            },
        }
    )

    assert r.hint_rewrite is not None
    assert r.hint_rewrite.hint_id == "opaque_bottle_sink_use"
    assert r.hint_rewrite.required_terms == ["병", "싱크대"]
    assert r.hint_rewrite.forbidden_terms == ["열쇠"]


def test_hint_rewrite_rejects_empty_base_hint() -> None:
    with pytest.raises(ValueError):
        ChatRequest.model_validate(
            {
                "prompt": "힌트",
                "hint_rewrite": {
                    "hint_id": "h",
                    "item_id": "i",
                    "hint_target": "t",
                    "hint_level": "direct",
                    "base_hint": "",
                },
            }
        )


def test_chat_request_accepts_locale_en() -> None:
    req = ChatRequest(prompt="hi", locale="en-US")
    assert req.locale == "en"


def test_chat_request_omitted_locale_defaults_ko() -> None:
    req = ChatRequest(prompt="hi")
    assert req.locale == "ko"


@pytest.mark.parametrize(
    "raw",
    ["JA", "JP", "ja-JP", "Japanese", "japanese"],
)
def test_chat_request_japanese_aliases_normalize_to_ja(raw: str) -> None:
    req = ChatRequest(prompt="hi", locale=raw)
    assert req.locale == "ja"


def test_chat_request_rejects_unknown_rag_profile() -> None:
    with pytest.raises(ValueError, match="rag_profile"):
        ChatRequest(prompt="hi", rag_profile="anything")


def test_chat_request_accepts_project_rag_profile() -> None:
    req = ChatRequest(prompt="hi", rag_profile="project")
    assert req.rag_profile == "project"

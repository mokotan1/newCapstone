from __future__ import annotations

from services.dialogue_guard import (
    dialogue_fallback_line,
    sanitize_dialogue_reply,
)


def test_empty_reply_uses_locale_fallback() -> None:
    assert sanitize_dialogue_reply("", locale="ko") == dialogue_fallback_line("ko")
    assert sanitize_dialogue_reply("   ", locale="en") == dialogue_fallback_line("en")


def test_json_or_tool_like_reply_uses_fallback() -> None:
    json_blob = '{"name": "give_hint", "arguments": {"target_object": "bed"}}'
    assert sanitize_dialogue_reply(json_blob, locale="ko") == dialogue_fallback_line("ko")
    assert sanitize_dialogue_reply("[emote](happy)", locale="ja") == dialogue_fallback_line("ja")


def test_more_than_two_period_sentences_keeps_first_two() -> None:
    overlong = "첫 문장이다. 둘째 문장이다. 셋째 문장이다."
    assert sanitize_dialogue_reply(overlong, locale="ko") == "첫 문장이다. 둘째 문장이다."


def test_catchphrase_after_two_periods_is_not_replaced() -> None:
    line = "여긴 침실이야. 침대 아래를 봐. 깍!"
    assert sanitize_dialogue_reply(line, locale="ko") == line


def test_exclamation_opener_does_not_count_as_a_period_sentence() -> None:
    line = "켁! 문을 두드려. 깍!"
    assert sanitize_dialogue_reply(line, locale="ko") == line


def test_one_or_two_sentences_pass_through() -> None:
    one = "켁."
    two = "나는 체셔 앵무야. 저택을 안내하지."
    assert sanitize_dialogue_reply(one, locale="ko") == one
    assert sanitize_dialogue_reply(two, locale="ko") == two

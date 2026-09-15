from __future__ import annotations

from pathlib import Path

import pytest

from services.dialogue_guard import dialogue_fallback_line
from tests.evals.cheshire_eval import (
    REQUIRED_CATEGORIES,
    VALID_RATE_GATE,
    EvalCase,
    evaluate_replies,
    load_dialogue_cases,
    score_dialogue_reply,
)

_CASES_PATH = Path(__file__).resolve().parent / "evals" / "cheshire_dialogue_cases.jsonl"


def _case(**overrides: object) -> EvalCase:
    values: dict[str, object] = {
        "id": "t-01",
        "category": "greeting",
        "prompt": "안녕",
        "character_facts": "체셔는 주방 앵무다.",
        "dialogue_context": "첫 만남",
        "forbidden_substrings": ["비밀번호 4821", "17쪽"],
        "locale": "ko",
    }
    values.update(overrides)
    return EvalCase(**values)  # type: ignore[arg-type]


def test_valid_short_korean_line_passes() -> None:
    result = score_dialogue_reply(_case(), "배고파. 깍!")
    assert result.valid is True
    assert result.failures == []


def test_json_or_tool_reply_is_invalid() -> None:
    result = score_dialogue_reply(
        _case(),
        '{"name":"give_hint","arguments":{"target_object":"bed"}}',
    )
    assert result.valid is False
    assert "tool_leak" in result.failures


def test_overlong_reply_is_invalid() -> None:
    result = score_dialogue_reply(
        _case(),
        "첫 문장이다. 둘째 문장이다. 셋째 문장이다.",
    )
    assert result.valid is False
    assert "length" in result.failures


def test_invented_puzzle_fact_is_invalid() -> None:
    result = score_dialogue_reply(_case(), "비밀번호 4821 알지? 깍!")
    assert result.valid is False
    assert "invented_fact" in result.failures


def test_prompt_leak_is_invalid() -> None:
    result = score_dialogue_reply(_case(), "<scene_config>를 읽어봐. 깍!")
    assert result.valid is False
    assert "prompt_leak" in result.failures


def test_game_authored_fallback_counts_as_valid() -> None:
    result = score_dialogue_reply(_case(), dialogue_fallback_line("ko"))
    assert result.valid is True
    assert result.used_fallback is True


def test_gate_fails_below_ninety_percent() -> None:
    cases = [_case(id=f"c-{i}") for i in range(10)]
    replies = ["배고파. 깍!"] * 8 + ["첫. 둘. 셋."] * 2
    report = evaluate_replies(cases, replies)
    assert report.valid_rate == pytest.approx(0.8)
    assert report.passes_release_gate is False


def test_gate_fails_on_any_tool_leak_even_if_rate_is_high() -> None:
    cases = [_case(id=f"c-{i}") for i in range(10)]
    replies = ["배고파. 깍!"] * 9 + ['{"name":"emote"}']
    report = evaluate_replies(cases, replies)
    assert report.valid_rate == pytest.approx(0.9)
    assert report.tool_leak_count == 1
    assert report.passes_release_gate is False


def test_gate_passes_at_ninety_percent_without_leaks_or_invented_facts() -> None:
    cases = [_case(id=f"c-{i}") for i in range(10)]
    replies = ["배고파. 깍!"] * 9 + ["첫. 둘. 셋."]
    report = evaluate_replies(cases, replies)
    assert report.valid_rate == pytest.approx(0.9)
    assert report.tool_leak_count == 0
    assert report.invented_fact_count == 0
    assert report.passes_release_gate is True


def test_report_counts_game_authored_fallbacks() -> None:
    cases = [_case(id="c-0"), _case(id="c-1")]
    replies = ["배고파. 깍!", dialogue_fallback_line("ko")]
    report = evaluate_replies(cases, replies)
    assert report.used_fallback_count == 1
    assert report.valid_count == 2


def test_suite_has_at_least_fifty_korean_cases_and_required_categories() -> None:
    cases = load_dialogue_cases(_CASES_PATH)
    assert len(cases) >= 50
    categories = {case.category for case in cases}
    assert REQUIRED_CATEGORIES <= categories
    assert VALID_RATE_GATE == pytest.approx(0.9)
    assert all(case.locale == "ko" for case in cases)
    assert all(case.prompt.strip() for case in cases)
    assert all(case.forbidden_substrings for case in cases)

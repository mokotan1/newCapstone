from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from services.dialogue_guard import (
    dialogue_fallback_line,
    dialogue_sentence_count,
    looks_like_json_or_tool,
)

REQUIRED_CATEGORIES = frozenset(
    {
        "greeting",
        "hint",
        "wrong_answer",
        "repeated",
        "emotion",
        "lore_boundary",
    }
)
VALID_RATE_GATE = 0.9
_HANGUL = re.compile(r"[가-힣]")
_PROMPT_LEAK_MARKERS = (
    "<scene_config>",
    "<external_document>",
    "<user_input>",
    "[시스템",
    "character_facts",
    "dialogue_context",
)


@dataclass(frozen=True)
class EvalCase:
    id: str
    category: str
    prompt: str
    character_facts: str
    dialogue_context: str
    forbidden_substrings: tuple[str, ...]
    locale: str = "ko"
    system: str = ""
    previous_reply: str = ""


@dataclass(frozen=True)
class ReplyScore:
    valid: bool
    failures: list[str]
    used_fallback: bool


@dataclass(frozen=True)
class EvalReport:
    case_count: int
    valid_count: int
    valid_rate: float
    tool_leak_count: int
    invented_fact_count: int
    prompt_leak_count: int
    used_fallback_count: int
    passes_release_gate: bool
    scores: tuple[ReplyScore, ...]


def score_dialogue_reply(case: EvalCase, reply: str) -> ReplyScore:
    """Score a player-visible Cheshire line against format and lore constraints."""
    locale = case.locale
    stripped = (reply or "").strip()
    if stripped == dialogue_fallback_line(locale):
        return ReplyScore(valid=True, failures=[], used_fallback=True)

    failures: list[str] = []
    if not stripped:
        failures.append("empty")
    if looks_like_json_or_tool(stripped):
        failures.append("tool_leak")
    if dialogue_sentence_count(stripped) > 2:
        failures.append("length")
    lowered = stripped.lower()
    if any(marker.lower() in lowered for marker in _PROMPT_LEAK_MARKERS):
        failures.append("prompt_leak")
    if any(token and token in stripped for token in case.forbidden_substrings):
        failures.append("invented_fact")
    if case.previous_reply and stripped == case.previous_reply.strip():
        failures.append("repetition")
    if locale == "ko" and stripped and _HANGUL.search(stripped) is None:
        failures.append("korean")
    return ReplyScore(valid=not failures, failures=failures, used_fallback=False)


def evaluate_replies(cases: list[EvalCase], replies: list[str]) -> EvalReport:
    if len(cases) != len(replies):
        raise ValueError("cases and replies must be the same length")
    scores = tuple(
        score_dialogue_reply(case, reply) for case, reply in zip(cases, replies)
    )
    valid_count = sum(1 for score in scores if score.valid)
    case_count = len(cases)
    valid_rate = valid_count / case_count if case_count else 0.0
    tool_leak_count = sum(1 for score in scores if "tool_leak" in score.failures)
    invented_fact_count = sum(
        1 for score in scores if "invented_fact" in score.failures
    )
    prompt_leak_count = sum(1 for score in scores if "prompt_leak" in score.failures)
    used_fallback_count = sum(1 for score in scores if score.used_fallback)
    passes = (
        valid_rate >= VALID_RATE_GATE
        and tool_leak_count == 0
        and invented_fact_count == 0
        and prompt_leak_count == 0
    )
    return EvalReport(
        case_count=case_count,
        valid_count=valid_count,
        valid_rate=valid_rate,
        tool_leak_count=tool_leak_count,
        invented_fact_count=invented_fact_count,
        prompt_leak_count=prompt_leak_count,
        used_fallback_count=used_fallback_count,
        passes_release_gate=passes,
        scores=scores,
    )


def load_dialogue_cases(path: Path) -> list[EvalCase]:
    cases: list[EvalCase] = []
    raw = path.read_text(encoding="utf-8")
    for line_no, line in enumerate(raw.splitlines(), start=1):
        if not line.strip():
            continue
        payload: dict[str, Any] = json.loads(line)
        category = str(payload["category"])
        if category not in REQUIRED_CATEGORIES:
            raise ValueError(f"{path}:{line_no} unknown category {category!r}")
        forbidden = payload.get("forbidden_substrings") or []
        if not isinstance(forbidden, list) or not forbidden:
            raise ValueError(f"{path}:{line_no} forbidden_substrings must be non-empty")
        cases.append(
            EvalCase(
                id=str(payload["id"]),
                category=category,
                prompt=str(payload["prompt"]),
                character_facts=str(payload.get("character_facts") or ""),
                dialogue_context=str(payload.get("dialogue_context") or ""),
                forbidden_substrings=tuple(str(item) for item in forbidden),
                locale=str(payload.get("locale") or "ko"),
                system=str(payload.get("system") or ""),
                previous_reply=str(payload.get("previous_reply") or ""),
            )
        )
    return cases

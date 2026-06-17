from __future__ import annotations

import json
from pathlib import Path

import pytest

from analyze_play_logs import load_play_logs, build_session_summary
from player_context_summary import (
    HINT_POLICY_DIRECT,
    HINT_POLICY_LIGHT,
    HINT_POLICY_NORMAL,
    PLAYER_CONTEXT_FIELDS,
    build_player_context_summary,
    format_prompt_snippet,
    lookup_session_context,
    recommend_hint_policy,
    write_player_context_summary,
)

FIXTURES_DIR = Path(__file__).resolve().parent.parent / "fixtures" / "play_logs"
OUTPUT_DIR = FIXTURES_DIR.parent / "_tmp_player_context"


@pytest.mark.parametrize(
    ("stuck_score", "expected"),
    [
        (0, HINT_POLICY_NORMAL),
        (39, HINT_POLICY_NORMAL),
        (40, HINT_POLICY_LIGHT),
        (69, HINT_POLICY_LIGHT),
        (70, HINT_POLICY_DIRECT),
        (100, HINT_POLICY_DIRECT),
    ],
)
def test_recommend_hint_policy_thresholds(stuck_score: int, expected: str) -> None:
    assert recommend_hint_policy(float(stuck_score)) == expected


def test_build_player_context_summary_excludes_chat_text() -> None:
    events = load_play_logs(FIXTURES_DIR)
    session_summary = build_session_summary(events)
    payload = build_player_context_summary(events, session_summary)

    assert payload["version"] == 1
    assert len(payload["sessions"]) == 3

    serialized = json.dumps(payload)
    assert "user_message" not in serialized
    assert "bot_response" not in serialized

    for entry in payload["sessions"]:
        assert set(entry.keys()) == set(PLAYER_CONTEXT_FIELDS)
        assert isinstance(entry["stuck_score"], int)


def test_build_player_context_summary_uses_latest_scene_puzzle() -> None:
    events = load_play_logs(FIXTURES_DIR)
    session_summary = build_session_summary(events)
    payload = build_player_context_summary(events, session_summary)

    sess_a = lookup_session_context(payload, "sess-a")
    assert sess_a is not None
    assert sess_a["current_scene"] == "Kitchen"
    assert sess_a["current_puzzle"] == "Kitchen"
    assert sess_a["recommended_hint_policy"] == HINT_POLICY_LIGHT


def test_lookup_session_context_and_prompt_snippet() -> None:
    events = load_play_logs(FIXTURES_DIR)
    session_summary = build_session_summary(events)
    payload = build_player_context_summary(events, session_summary)

    sess_b = lookup_session_context(payload, "sess-b")
    assert sess_b is not None
    snippet = format_prompt_snippet(sess_b)
    assert "hint_policy=" in snippet
    assert "user_message" not in snippet


def test_write_player_context_summary_creates_json_file() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    events = load_play_logs(FIXTURES_DIR)
    session_summary = build_session_summary(events)

    output_path = write_player_context_summary(events, session_summary, OUTPUT_DIR)
    assert output_path.name == "player_context_summary.json"
    assert output_path.is_file()

    payload = json.loads(output_path.read_text(encoding="utf-8"))
    assert "sessions" in payload

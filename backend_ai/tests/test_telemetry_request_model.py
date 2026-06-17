from __future__ import annotations

import pytest
from pydantic import ValidationError

from config import get_settings
from models.requests import TelemetryEvent, TelemetryIngestRequest


def _valid_event_payload() -> dict:
    return {
        "session_id": "sess-1",
        "anonymous_player_id": "anon-1",
        "scene_name": "BedRoom",
        "puzzle_id": "BedRoom",
        "event_time": "2026-06-16T07:00:00.0000000+00:00",
        "event_type": "cheshire_user_message",
        "user_message": "where is the key?",
        "bot_response": "",
        "hint_level": "subtle",
        "progress_state": "room=GlobalChatbot;quest=q1;step=2",
        "time_since_scene_start": 12.5,
        "attempt_count": 3,
        "wrong_action_count": 1,
        "repeated_question_count": 0,
        "solved": False,
    }


def test_valid_event_parses() -> None:
    event = TelemetryEvent.model_validate(_valid_event_payload())
    assert event.session_id == "sess-1"
    assert event.attempt_count == 3
    assert event.solved is False


def test_session_id_required() -> None:
    payload = _valid_event_payload()
    del payload["session_id"]
    with pytest.raises(ValidationError):
        TelemetryEvent.model_validate(payload)


def test_event_type_required_non_empty() -> None:
    payload = _valid_event_payload()
    payload["event_type"] = ""
    with pytest.raises(ValidationError):
        TelemetryEvent.model_validate(payload)


def test_optional_text_defaults_to_empty() -> None:
    event = TelemetryEvent.model_validate(
        {"session_id": "s", "event_type": "scene_enter"},
    )
    assert event.user_message == ""
    assert event.bot_response == ""
    assert event.attempt_count == 0
    assert event.time_since_scene_start == 0.0


def test_user_message_length_bound() -> None:
    payload = _valid_event_payload()
    payload["user_message"] = "x" * 5000
    with pytest.raises(ValidationError):
        TelemetryEvent.model_validate(payload)


def test_negative_counts_rejected() -> None:
    payload = _valid_event_payload()
    payload["attempt_count"] = -1
    with pytest.raises(ValidationError):
        TelemetryEvent.model_validate(payload)


def test_extra_fields_ignored() -> None:
    payload = _valid_event_payload()
    payload["unexpected_future_column"] = "ignored"
    event = TelemetryEvent.model_validate(payload)
    assert event.session_id == "sess-1"


def test_batch_requires_at_least_one_event() -> None:
    with pytest.raises(ValidationError):
        TelemetryIngestRequest.model_validate({"events": []})


def test_batch_rejects_over_max() -> None:
    max_batch = get_settings().telemetry_max_batch
    payload = {"events": [_valid_event_payload() for _ in range(max_batch + 1)]}
    with pytest.raises(ValidationError):
        TelemetryIngestRequest.model_validate(payload)


def test_batch_accepts_within_max() -> None:
    request = TelemetryIngestRequest.model_validate(
        {"events": [_valid_event_payload(), _valid_event_payload()]},
    )
    assert len(request.events) == 2

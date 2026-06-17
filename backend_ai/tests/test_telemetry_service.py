from __future__ import annotations

import csv
from pathlib import Path

from models.requests import TelemetryEvent
from services.telemetry_service import TELEMETRY_COLUMNS, TelemetryService


def _event(**overrides) -> TelemetryEvent:
    base = {
        "session_id": "sess-1",
        "anonymous_player_id": "anon-1",
        "scene_name": "BedRoom",
        "puzzle_id": "BedRoom",
        "event_time": "2026-06-16T07:00:00+00:00",
        "event_type": "cheshire_user_message",
        "user_message": "hello",
        "bot_response": "",
        "hint_level": "subtle",
        "progress_state": "room=Global",
        "time_since_scene_start": 1.0,
        "attempt_count": 1,
        "wrong_action_count": 0,
        "repeated_question_count": 0,
        "solved": False,
    }
    base.update(overrides)
    return TelemetryEvent.model_validate(base)


def _read_rows(path: Path) -> list[list[str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as fh:
        return list(csv.reader(fh))


def test_append_creates_file_with_header(tmp_path: Path) -> None:
    service = TelemetryService(log_dir=tmp_path / "logs")
    accepted = service.append_events([_event()])

    assert accepted == 1
    rows = _read_rows(service.csv_path)
    assert rows[0] == list(TELEMETRY_COLUMNS)
    assert len(rows) == 2  # header + 1 data row


def test_header_written_only_once_across_appends(tmp_path: Path) -> None:
    service = TelemetryService(log_dir=tmp_path / "logs")
    service.append_events([_event()])
    service.append_events([_event(session_id="sess-2"), _event(session_id="sess-3")])

    rows = _read_rows(service.csv_path)
    header_count = sum(1 for row in rows if row and row[0] == TELEMETRY_COLUMNS[0])
    assert header_count == 1
    assert len(rows) == 1 + 3  # one header + three data rows


def test_data_row_values_in_column_order(tmp_path: Path) -> None:
    service = TelemetryService(log_dir=tmp_path / "logs")
    service.append_events([_event(user_message="hi", attempt_count=4, solved=True)])

    rows = _read_rows(service.csv_path)
    record = dict(zip(rows[0], rows[1]))
    assert record["session_id"] == "sess-1"
    assert record["user_message"] == "hi"
    assert record["attempt_count"] == "4"
    assert record["solved"] == "true"


def test_rfc4180_escaping_roundtrips_special_chars(tmp_path: Path) -> None:
    service = TelemetryService(log_dir=tmp_path / "logs")
    nasty = 'comma, "quote" and\nnewline'
    service.append_events([_event(user_message=nasty)])

    rows = _read_rows(service.csv_path)
    record = dict(zip(rows[0], rows[1]))
    assert record["user_message"] == nasty


def test_formula_injection_is_neutralized(tmp_path: Path) -> None:
    service = TelemetryService(log_dir=tmp_path / "logs")
    service.append_events([_event(user_message="=SUM(A1:A9)")])

    rows = _read_rows(service.csv_path)
    record = dict(zip(rows[0], rows[1]))
    assert record["user_message"].startswith("'")
    assert "=SUM(A1:A9)" in record["user_message"]


def test_append_returns_count_for_batch(tmp_path: Path) -> None:
    service = TelemetryService(log_dir=tmp_path / "logs")
    accepted = service.append_events([_event(), _event(session_id="s2")])
    assert accepted == 2


def test_csv_path_uses_configured_filename(tmp_path: Path) -> None:
    service = TelemetryService(log_dir=tmp_path / "logs", csv_filename="custom.csv")
    service.append_events([_event()])
    assert service.csv_path.name == "custom.csv"
    assert service.csv_path.exists()

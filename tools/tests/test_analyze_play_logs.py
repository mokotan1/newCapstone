from __future__ import annotations

from pathlib import Path

import pandas as pd
import pytest

from analyze_play_logs import (
    REQUIRED_COLUMNS,
    analyze_play_logs,
    build_puzzle_difficulty,
    build_session_summary,
    clamp100,
    compute_stuck_score,
    load_play_logs,
    write_outputs,
)

FIXTURES_DIR = Path(__file__).resolve().parent.parent / "fixtures" / "play_logs"


def test_load_play_logs_reads_fixture_directory() -> None:
    df = load_play_logs(FIXTURES_DIR)
    assert not df.empty
    assert set(REQUIRED_COLUMNS).issubset(df.columns)
    assert df["session_id"].nunique() == 3


def test_load_play_logs_missing_columns_raises_clear_error() -> None:
    bad_dir = FIXTURES_DIR.parent / "_tmp_bad_input"
    bad_dir.mkdir(parents=True, exist_ok=True)
    bad_csv = bad_dir / "bad.csv"
    bad_csv.write_text("session_id,scene_name\na,Kitchen\n", encoding="utf-8")

    try:
        with pytest.raises(ValueError, match="Missing required columns"):
            load_play_logs(bad_csv)
    finally:
        bad_csv.unlink(missing_ok=True)


def test_build_session_summary_aggregates_per_session_puzzle() -> None:
    events = load_play_logs(FIXTURES_DIR)
    summary = build_session_summary(events)

    assert list(summary.columns) == [
        "session_id",
        "player_id",
        "scene_name",
        "puzzle_id",
        "clear_time_seconds",
        "hint_count",
        "wrong_action_count",
        "repeated_question_count",
        "solved",
        "stuck_score",
    ]
    assert len(summary) == 3

    kitchen_a = summary[
        (summary["session_id"] == "sess-a") & (summary["puzzle_id"] == "Kitchen")
    ].iloc[0]
    assert kitchen_a["player_id"] == "anon-player-1"
    assert kitchen_a["hint_count"] == 1
    assert kitchen_a["wrong_action_count"] == 1
    assert kitchen_a["repeated_question_count"] == 2
    assert kitchen_a["clear_time_seconds"] == 480.0
    assert bool(kitchen_a["solved"]) is True
    assert 0 <= kitchen_a["stuck_score"] <= 100

    kitchen_b = summary[
        (summary["session_id"] == "sess-b") & (summary["puzzle_id"] == "Kitchen")
    ].iloc[0]
    assert kitchen_b["hint_count"] == 2
    assert bool(kitchen_b["solved"]) is False
    assert kitchen_b["clear_time_seconds"] == 1200.0


def test_stuck_score_formula_weights() -> None:
    score = compute_stuck_score(
        clear_time_seconds=600.0,
        hint_count=5,
        wrong_action_count=10,
        repeated_question_count=5,
    )
    assert score == pytest.approx(100.0)

    low_score = compute_stuck_score(0.0, 0, 0, 0)
    assert low_score == pytest.approx(0.0)


def test_build_puzzle_difficulty_aggregates_sessions() -> None:
    events = load_play_logs(FIXTURES_DIR)
    session_summary = build_session_summary(events)
    difficulty = build_puzzle_difficulty(session_summary)

    kitchen = difficulty[difficulty["puzzle_id"] == "Kitchen"].iloc[0]
    assert kitchen["session_count"] == 2
    assert kitchen["clear_rate"] == pytest.approx(0.5)
    assert kitchen["abandon_rate"] == pytest.approx(0.5)
    assert kitchen["avg_hint_count"] == pytest.approx(1.5)
    assert 0 <= kitchen["difficulty_score"] <= 100

    study = difficulty[difficulty["puzzle_id"] == "StudyRoom"].iloc[0]
    assert study["session_count"] == 1
    assert study["clear_rate"] == pytest.approx(1.0)
    assert study["median_clear_time"] == 180.0


def test_analyze_play_logs_writes_outputs() -> None:
    output_dir = FIXTURES_DIR.parent / "_tmp_outputs"
    if output_dir.exists():
        for child in output_dir.iterdir():
            child.unlink()
    else:
        output_dir.mkdir(parents=True)

    session_summary, puzzle_difficulty = analyze_play_logs(FIXTURES_DIR)
    events = load_play_logs(FIXTURES_DIR)
    session_path, puzzle_path, context_path = write_outputs(
        session_summary,
        puzzle_difficulty,
        output_dir,
        events=events,
    )

    assert session_path.is_file()
    assert puzzle_path.is_file()
    assert context_path.is_file()

    written_session = pd.read_csv(session_path)
    written_puzzle = pd.read_csv(puzzle_path)
    assert len(written_session) == len(session_summary)
    assert len(written_puzzle) == len(puzzle_difficulty)


def test_clamp100_bounds() -> None:
    assert clamp100(-5) == 0.0
    assert clamp100(150) == 100.0
    assert clamp100(42.5) == 42.5

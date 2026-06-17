from __future__ import annotations

from pathlib import Path
from unittest.mock import MagicMock, patch

import pandas as pd
import pytest

from upload_play_log_to_sheets import (
    CHAT_TEXT_COLUMNS,
    CHART_TOP_N,
    UploadStepError,
    apply_basic_formatting,
    build_chart_data_sections,
    build_top_difficult_puzzles,
    build_top_stuck_puzzles,
    dataframe_to_values,
    filter_chat_columns,
    layout_chart_data_horizontally,
    load_raw_logs_dataframe,
    resolve_spreadsheet_id,
    try_create_dashboard_charts,
    upload_dataframe_to_worksheet,
    upload_play_logs_to_sheets,
)

FIXTURES_DIR = Path(__file__).resolve().parent.parent / "fixtures" / "play_logs"
OUTPUTS_DIR = FIXTURES_DIR.parent / "_tmp_outputs"


def test_resolve_spreadsheet_id_prefers_cli() -> None:
    assert resolve_spreadsheet_id("cli-id") == "cli-id"


def test_resolve_spreadsheet_id_uses_env(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("GOOGLE_SHEET_ID", "env-id")
    assert resolve_spreadsheet_id(None) == "env-id"


def test_resolve_spreadsheet_id_missing_raises() -> None:
    with pytest.raises(UploadStepError, match="resolve_spreadsheet_id"):
        resolve_spreadsheet_id(None)


def test_filter_chat_columns_excludes_by_default() -> None:
    frame = pd.DataFrame(
        {
            "session_id": ["a"],
            "user_message": ["secret"],
            "bot_response": ["reply"],
        }
    )
    filtered = filter_chat_columns(frame, include_chat_text=False)
    assert "user_message" not in filtered.columns
    assert "bot_response" not in filtered.columns
    assert list(filtered.columns) == ["session_id"]


def test_filter_chat_columns_includes_when_requested() -> None:
    frame = pd.DataFrame({"user_message": ["secret"], "bot_response": ["reply"]})
    filtered = filter_chat_columns(frame, include_chat_text=True)
    assert list(filtered.columns) == list(CHAT_TEXT_COLUMNS)


def test_load_raw_logs_dataframe_drops_chat_columns_by_default() -> None:
    frame = load_raw_logs_dataframe(FIXTURES_DIR, include_chat_text=False)
    assert "user_message" not in frame.columns
    assert "bot_response" not in frame.columns
    assert not frame.empty


def test_load_raw_logs_dataframe_keeps_chat_columns_when_enabled() -> None:
    frame = load_raw_logs_dataframe(FIXTURES_DIR, include_chat_text=True)
    assert "user_message" in frame.columns
    assert "bot_response" in frame.columns


def test_dataframe_to_values_includes_header() -> None:
    frame = pd.DataFrame({"a": [1], "b": [2]})
    values = dataframe_to_values(frame)
    assert values[0] == ["a", "b"]
    assert values[1] == ["1", "2"]


def test_upload_dataframe_to_worksheet_clears_and_updates() -> None:
    worksheet = MagicMock()
    spreadsheet = MagicMock()
    spreadsheet.worksheet.return_value = worksheet

    upload_dataframe_to_worksheet(
        spreadsheet,
        "SessionSummary",
        pd.DataFrame({"x": [1]}),
        step="test_upload",
    )

    worksheet.clear.assert_called_once()
    worksheet.resize.assert_called_once()
    worksheet.update.assert_called_once()
    worksheet.freeze.assert_called_once_with(rows=1)
    worksheet.format.assert_called_once()


def test_apply_basic_formatting_noop_for_empty_columns() -> None:
    worksheet = MagicMock()
    apply_basic_formatting(worksheet, 0)
    worksheet.freeze.assert_not_called()


def test_build_top_difficult_puzzles_sorts_and_limits() -> None:
    puzzle = pd.read_csv(OUTPUTS_DIR / "puzzle_difficulty.csv")
    result = build_top_difficult_puzzles(puzzle)
    assert list(result.columns) == [
        "scene_name",
        "puzzle_id",
        "difficulty_score",
        "clear_rate",
        "median_clear_time",
    ]
    assert len(result) <= CHART_TOP_N
    assert result.iloc[0]["difficulty_score"] >= result.iloc[-1]["difficulty_score"]


def test_build_top_stuck_puzzles_aggregates_sessions() -> None:
    sessions = pd.read_csv(OUTPUTS_DIR / "session_summary.csv")
    result = build_top_stuck_puzzles(sessions)
    assert list(result.columns) == [
        "scene_name",
        "puzzle_id",
        "avg_stuck_score",
        "session_count",
    ]
    kitchen = result[result["puzzle_id"] == "Kitchen"].iloc[0]
    assert kitchen["session_count"] == 2


def test_build_chart_data_sections_layout_separates_blocks() -> None:
    sessions = pd.read_csv(OUTPUTS_DIR / "session_summary.csv")
    puzzle = pd.read_csv(OUTPUTS_DIR / "puzzle_difficulty.csv")
    sections = build_chart_data_sections(sessions, puzzle)
    grid, layouts = layout_chart_data_horizontally(sections)

    assert len(sections) == 4
    assert len(layouts) == 4
    assert layouts[0].title == "TopDifficultPuzzles"
    assert layouts[1].start_col > layouts[0].end_col
    assert grid[0][layouts[0].start_col - 1] == "TopDifficultPuzzles"
    assert grid[1][layouts[0].start_col - 1] == "scene_name"


def test_try_create_dashboard_charts_swallows_errors() -> None:
    spreadsheet = MagicMock()
    spreadsheet.batch_update.side_effect = RuntimeError("chart api unavailable")
    chart_worksheet = MagicMock()
    chart_worksheet.id = 123

    try_create_dashboard_charts(
        spreadsheet,
        chart_worksheet,
        [
            MagicMock(
                title="TopDifficultPuzzles",
                column_source_range=lambda sheet_id, column_name: {},
            ),
            MagicMock(
                title="ClearRateByPuzzle",
                column_source_range=lambda sheet_id, column_name: {},
            ),
        ],
    )

    spreadsheet.batch_update.assert_called_once()


@patch("upload_play_log_to_sheets.try_create_dashboard_charts")
@patch("upload_play_log_to_sheets.open_spreadsheet")
@patch("upload_play_log_to_sheets.authorize_client")
@patch("upload_play_log_to_sheets.resolve_credentials_path")
def test_upload_play_logs_to_sheets_end_to_end_with_mock_client(
    mock_credentials: MagicMock,
    mock_authorize: MagicMock,
    mock_open_spreadsheet: MagicMock,
    mock_dashboard_charts: MagicMock,
) -> None:
    mock_credentials.return_value = Path("fake.json")
    client = MagicMock()
    mock_authorize.return_value = client
    spreadsheet = MagicMock()
    mock_open_spreadsheet.return_value = spreadsheet

    worksheet = MagicMock()
    spreadsheet.worksheet.return_value = worksheet

    upload_play_logs_to_sheets(
        summary_dir=OUTPUTS_DIR,
        raw_dir=FIXTURES_DIR,
        spreadsheet_id="sheet-id",
        include_chat_text=False,
        client=client,
    )

    assert worksheet.clear.call_count >= 4
    assert worksheet.update.call_count >= 4
    mock_dashboard_charts.assert_called_once()
    mock_open_spreadsheet.assert_called_once_with(client, "sheet-id")

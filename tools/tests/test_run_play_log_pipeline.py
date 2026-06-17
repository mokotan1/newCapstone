from __future__ import annotations

from pathlib import Path
from unittest.mock import MagicMock, patch

import pandas as pd
import pytest

from run_play_log_pipeline import (
    EXIT_ANALYZE_FAILED,
    EXIT_SUCCESS,
    EXIT_UPLOAD_FAILED,
    PipelineStepError,
    build_top_difficulty_rows,
    count_raw_log_files,
    resolve_include_chat_text,
    run_play_log_pipeline,
)

FIXTURES_DIR = Path(__file__).resolve().parent.parent / "fixtures" / "play_logs"
OUTPUTS_DIR = FIXTURES_DIR.parent / "_tmp_pipeline_outputs"


def _local_output_dir(name: str) -> Path:
    path = OUTPUTS_DIR / name
    path.mkdir(parents=True, exist_ok=True)
    return path


def test_count_raw_log_files_directory() -> None:
    assert count_raw_log_files(FIXTURES_DIR) >= 1


def test_resolve_include_chat_text_defaults_false(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.delenv("INCLUDE_CHAT_TEXT", raising=False)
    assert resolve_include_chat_text(False) is False


def test_resolve_include_chat_text_from_env(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("INCLUDE_CHAT_TEXT", "true")
    assert resolve_include_chat_text(False) is True


def test_resolve_include_chat_text_cli_overrides_env(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("INCLUDE_CHAT_TEXT", "false")
    assert resolve_include_chat_text(True) is True


def test_build_top_difficulty_rows_limits_to_five() -> None:
    puzzle = pd.DataFrame(
        {
            "scene_name": [f"S{i}" for i in range(8)],
            "puzzle_id": [f"P{i}" for i in range(8)],
            "difficulty_score": list(range(8)),
            "clear_rate": [0.1] * 8,
        }
    )
    rows = build_top_difficulty_rows(puzzle, limit=5)
    assert len(rows) == 5
    assert rows[0]["difficulty_score"] == 7


@patch("run_play_log_pipeline.upload_play_logs_to_sheets")
@patch("run_play_log_pipeline.resolve_spreadsheet_id")
def test_run_play_log_pipeline_success(
    mock_resolve_spreadsheet_id: MagicMock,
    mock_upload: MagicMock,
) -> None:
    output_dir = _local_output_dir("success")
    mock_resolve_spreadsheet_id.return_value = "sheet-123"

    summary = run_play_log_pipeline(
        input_path=FIXTURES_DIR,
        output_dir=output_dir,
        spreadsheet_id="sheet-123",
        include_chat_text=False,
    )

    assert summary.raw_log_file_count >= 1
    assert summary.session_count >= 1
    assert summary.puzzle_count >= 1
    assert summary.spreadsheet_id == "sheet-123"
    assert summary.session_summary_path.is_file()
    assert summary.puzzle_difficulty_path.is_file()
    assert summary.player_context_path.is_file()
    mock_upload.assert_called_once()


@patch("run_play_log_pipeline.upload_play_logs_to_sheets")
def test_run_play_log_pipeline_upload_failure_exit_code(
    mock_upload: MagicMock,
) -> None:
    from upload_play_log_to_sheets import UploadStepError

    output_dir = _local_output_dir("upload_fail")
    mock_upload.side_effect = UploadStepError("authorize_client", "bad credentials")

    with pytest.raises(PipelineStepError) as exc_info:
        run_play_log_pipeline(
            input_path=FIXTURES_DIR,
            output_dir=output_dir,
            spreadsheet_id="sheet-123",
            include_chat_text=False,
        )

    assert exc_info.value.stage == "upload"
    assert exc_info.value.exit_code == EXIT_UPLOAD_FAILED


def test_run_play_log_pipeline_analyze_failure_on_missing_input() -> None:
    missing = OUTPUTS_DIR / "missing_logs_dir"
    output_dir = _local_output_dir("analyze_fail")

    with pytest.raises(PipelineStepError) as exc_info:
        run_play_log_pipeline(
            input_path=missing,
            output_dir=output_dir,
            spreadsheet_id="sheet-123",
            include_chat_text=False,
        )

    assert exc_info.value.stage == "analyze"
    assert exc_info.value.exit_code == EXIT_ANALYZE_FAILED


@patch("run_play_log_pipeline.run_play_log_pipeline")
def test_main_returns_stage_exit_code(mock_run: MagicMock) -> None:
    from run_play_log_pipeline import main

    mock_run.side_effect = PipelineStepError("upload", "failed", EXIT_UPLOAD_FAILED)
    assert main(["--input", str(FIXTURES_DIR), "--output", str(OUTPUTS_DIR), "--spreadsheet-id", "x"]) == EXIT_UPLOAD_FAILED


@patch("run_play_log_pipeline.run_play_log_pipeline")
def test_main_success(mock_run: MagicMock) -> None:
    from run_play_log_pipeline import PipelineSummary, main

    mock_run.return_value = PipelineSummary(
        raw_log_file_count=1,
        session_count=1,
        puzzle_count=1,
        top_difficulty_rows=[],
        session_summary_path=OUTPUTS_DIR / "session_summary.csv",
        puzzle_difficulty_path=OUTPUTS_DIR / "puzzle_difficulty.csv",
        player_context_path=OUTPUTS_DIR / "player_context_summary.json",
        spreadsheet_id="sheet-123",
    )
    assert main(["--input", str(FIXTURES_DIR), "--output", str(OUTPUTS_DIR), "--spreadsheet-id", "x"]) == EXIT_SUCCESS

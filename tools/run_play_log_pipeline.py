#!/usr/bin/env python3
"""Run play-log analysis and Google Sheets upload in one command."""

from __future__ import annotations

import argparse
import logging
import os
import sys
from dataclasses import dataclass
from pathlib import Path

import pandas as pd

from analyze_play_logs import (
    build_puzzle_difficulty,
    build_session_summary,
    load_play_logs,
    write_outputs,
)
from upload_play_log_to_sheets import (
    UploadStepError,
    resolve_spreadsheet_id,
    upload_play_logs_to_sheets,
)

logger = logging.getLogger(__name__)

EXIT_SUCCESS = 0
EXIT_ANALYZE_FAILED = 1
EXIT_UPLOAD_FAILED = 2

TOP_DIFFICULTY_COUNT = 5


class PipelineStepError(RuntimeError):
    """Pipeline failure with stage name and exit code."""

    def __init__(self, stage: str, message: str, exit_code: int) -> None:
        super().__init__(message)
        self.stage = stage
        self.exit_code = exit_code


@dataclass(frozen=True)
class PipelineSummary:
    raw_log_file_count: int
    session_count: int
    puzzle_count: int
    top_difficulty_rows: list[dict[str, object]]
    session_summary_path: Path
    puzzle_difficulty_path: Path
    player_context_path: Path
    spreadsheet_id: str


def count_raw_log_files(input_path: Path) -> int:
    if input_path.is_file():
        return 1
    if not input_path.is_dir():
        return 0
    return len(list(input_path.glob("*.csv")))


def resolve_include_chat_text(cli_include: bool) -> bool:
    if cli_include:
        return True
    env_value = os.environ.get("INCLUDE_CHAT_TEXT", "false").strip().lower()
    return env_value in {"1", "true", "yes", "on"}


def build_top_difficulty_rows(puzzle_difficulty: pd.DataFrame, limit: int = TOP_DIFFICULTY_COUNT) -> list[dict[str, object]]:
    if puzzle_difficulty.empty:
        return []

    if "difficulty_score" not in puzzle_difficulty.columns:
        return []

    ranked = puzzle_difficulty.sort_values("difficulty_score", ascending=False).head(limit)
    rows: list[dict[str, object]] = []
    for _, row in ranked.iterrows():
        clear_rate = row.get("clear_rate", "")
        rows.append(
            {
                "scene_name": row.get("scene_name", ""),
                "puzzle_id": row.get("puzzle_id", ""),
                "difficulty_score": row.get("difficulty_score", ""),
                "clear_rate": clear_rate,
            }
        )
    return rows


def run_analyze_step(input_path: Path, output_dir: Path) -> tuple[pd.DataFrame, pd.DataFrame, Path, Path, Path]:
    logger.info("Pipeline step 1/2: analyze play logs (%s)", input_path)
    try:
        events = load_play_logs(input_path)
        session_summary = build_session_summary(events)
        puzzle_difficulty = build_puzzle_difficulty(session_summary)
        session_path, puzzle_path, context_path = write_outputs(
            session_summary,
            puzzle_difficulty,
            output_dir,
            events=events,
        )
    except (ValueError, FileNotFoundError) as exc:
        raise PipelineStepError("analyze", str(exc), EXIT_ANALYZE_FAILED) from exc
    except Exception as exc:  # noqa: BLE001
        raise PipelineStepError("analyze", str(exc), EXIT_ANALYZE_FAILED) from exc

    logger.info(
        "Analyze complete: %s sessions, %s puzzles",
        len(session_summary),
        len(puzzle_difficulty),
    )
    return session_summary, puzzle_difficulty, session_path, puzzle_path, context_path


def run_upload_step(
    *,
    summary_dir: Path,
    raw_dir: Path,
    spreadsheet_id: str,
    include_chat_text: bool,
) -> None:
    logger.info("Pipeline step 2/2: upload to Google Sheets (%s)", spreadsheet_id)
    try:
        upload_play_logs_to_sheets(
            summary_dir=summary_dir,
            raw_dir=raw_dir,
            spreadsheet_id=spreadsheet_id,
            include_chat_text=include_chat_text,
        )
    except UploadStepError as exc:
        raise PipelineStepError("upload", str(exc), EXIT_UPLOAD_FAILED) from exc
    except Exception as exc:  # noqa: BLE001
        raise PipelineStepError("upload", str(exc), EXIT_UPLOAD_FAILED) from exc


def run_play_log_pipeline(
    *,
    input_path: Path,
    output_dir: Path,
    spreadsheet_id: str | None,
    include_chat_text: bool,
) -> PipelineSummary:
    raw_log_file_count = count_raw_log_files(input_path)
    if raw_log_file_count == 0:
        raise PipelineStepError(
            "analyze",
            f"No CSV play logs found at {input_path}",
            EXIT_ANALYZE_FAILED,
        )

    session_summary, puzzle_difficulty, session_path, puzzle_path, context_path = run_analyze_step(
        input_path,
        output_dir,
    )

    try:
        resolved_spreadsheet_id = resolve_spreadsheet_id(spreadsheet_id)
    except UploadStepError as exc:
        raise PipelineStepError("upload", str(exc), EXIT_UPLOAD_FAILED) from exc

    run_upload_step(
        summary_dir=output_dir,
        raw_dir=input_path,
        spreadsheet_id=resolved_spreadsheet_id,
        include_chat_text=include_chat_text,
    )

    return PipelineSummary(
        raw_log_file_count=raw_log_file_count,
        session_count=len(session_summary),
        puzzle_count=len(puzzle_difficulty),
        top_difficulty_rows=build_top_difficulty_rows(puzzle_difficulty),
        session_summary_path=session_path,
        puzzle_difficulty_path=puzzle_path,
        player_context_path=context_path,
        spreadsheet_id=resolved_spreadsheet_id,
    )


def format_clear_rate(value: object) -> str:
    if value == "" or value is None or (isinstance(value, float) and pd.isna(value)):
        return "n/a"
    try:
        numeric = float(value)
    except (TypeError, ValueError):
        return str(value)
    return f"{numeric * 100:.1f}%"


def print_pipeline_summary(summary: PipelineSummary) -> None:
    print("")
    print("=== Play Log Pipeline Summary ===")
    print(f"Raw log files processed : {summary.raw_log_file_count}")
    print(f"Sessions                : {summary.session_count}")
    print(f"Puzzles                 : {summary.puzzle_count}")
    print(f"Session summary CSV     : {summary.session_summary_path}")
    print(f"Puzzle difficulty CSV   : {summary.puzzle_difficulty_path}")
    print(f"Player context JSON     : {summary.player_context_path}")
    print(f"Spreadsheet ID          : {summary.spreadsheet_id}")
    print("")
    print(f"Top {TOP_DIFFICULTY_COUNT} difficulty_score:")
    if not summary.top_difficulty_rows:
        print("  (no puzzle difficulty data)")
    else:
        for index, row in enumerate(summary.top_difficulty_rows, start=1):
            scene = row.get("scene_name", "")
            puzzle = row.get("puzzle_id", "")
            score = row.get("difficulty_score", "")
            clear_rate = format_clear_rate(row.get("clear_rate"))
            print(
                f"  {index}. {scene} / {puzzle} — "
                f"difficulty_score {score}, clear_rate {clear_rate}"
            )
    print("")


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Analyze Unity play-log CSV files and upload results to Google Sheets. "
            "Developer/CI tool only — never embed Google credentials in Unity."
        ),
    )
    parser.add_argument(
        "--input",
        "-i",
        type=Path,
        required=True,
        help="Directory or file containing raw play-log CSV files.",
    )
    parser.add_argument(
        "--output",
        "-o",
        type=Path,
        required=True,
        help="Output directory for analysis CSVs (also used as Sheets summary source).",
    )
    parser.add_argument(
        "--spreadsheet-id",
        type=str,
        default=None,
        help="Google Spreadsheet ID (fallback: GOOGLE_SHEET_ID env var).",
    )
    parser.add_argument(
        "--include-chat-text",
        action="store_true",
        help=(
            "Upload user_message and bot_response to RawLogs. "
            "Default follows INCLUDE_CHAT_TEXT env (false unless true/1/yes/on)."
        ),
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    logging.basicConfig(level=logging.INFO, format="%(levelname)s %(message)s")
    args = parse_args(argv)
    include_chat_text = resolve_include_chat_text(args.include_chat_text)

    try:
        summary = run_play_log_pipeline(
            input_path=args.input,
            output_dir=args.output,
            spreadsheet_id=args.spreadsheet_id,
            include_chat_text=include_chat_text,
        )
    except PipelineStepError as exc:
        logger.error("Pipeline failed at stage '%s': %s", exc.stage, exc)
        print(
            f"ERROR: stage={exc.stage} exit_code={exc.exit_code} message={exc}",
            file=sys.stderr,
        )
        return exc.exit_code
    except UploadStepError as exc:
        logger.error("Pipeline failed at stage 'upload': [%s] %s", exc.step, exc)
        print(
            f"ERROR: stage=upload exit_code={EXIT_UPLOAD_FAILED} message=[{exc.step}] {exc}",
            file=sys.stderr,
        )
        return EXIT_UPLOAD_FAILED
    except Exception as exc:  # noqa: BLE001
        logger.exception("Pipeline failed with unexpected error")
        print(f"ERROR: stage=unknown exit_code={EXIT_ANALYZE_FAILED} message={exc}", file=sys.stderr)
        return EXIT_ANALYZE_FAILED

    print_pipeline_summary(summary)
    logger.info("Pipeline completed successfully.")
    return EXIT_SUCCESS


if __name__ == "__main__":
    raise SystemExit(main())

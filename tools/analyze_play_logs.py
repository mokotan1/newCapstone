#!/usr/bin/env python3
"""Aggregate Unity play-log CSV files into session and puzzle difficulty summaries."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import pandas as pd

PLAYER_CONTEXT_SUMMARY_FILE = "player_context_summary.json"

REQUIRED_COLUMNS: tuple[str, ...] = (
    "session_id",
    "anonymous_player_id",
    "scene_name",
    "puzzle_id",
    "event_time",
    "event_type",
    "time_since_scene_start",
    "attempt_count",
    "wrong_action_count",
    "repeated_question_count",
    "solved",
)

SESSION_SUMMARY_COLUMNS: tuple[str, ...] = (
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
)

PUZZLE_DIFFICULTY_COLUMNS: tuple[str, ...] = (
    "scene_name",
    "puzzle_id",
    "session_count",
    "clear_rate",
    "median_clear_time",
    "avg_hint_count",
    "avg_wrong_action_count",
    "repeat_question_rate",
    "abandon_rate",
    "difficulty_score",
)

# Normalization caps (seconds / counts) — values at cap map to score 100.
TIME_CAP_SECONDS = 600.0
HINT_CAP = 5.0
REPEAT_CAP = 5.0
WRONG_ACTION_CAP = 10.0

STUCK_WEIGHTS: dict[str, float] = {
    "no_progress_time": 0.35,
    "repeated_question": 0.25,
    "hint_dependency": 0.20,
    "wrong_attempt": 0.20,
}

DIFFICULTY_WEIGHTS: dict[str, float] = {
    "time": 0.30,
    "hint": 0.25,
    "fail": 0.20,
    "repeat": 0.15,
    "abandon": 0.10,
}


def clamp100(value: float) -> float:
    return float(max(0.0, min(100.0, value)))


def normalize_ratio(value: float, cap: float) -> float:
    if cap <= 0:
        return 0.0
    return clamp100(100.0 * float(value) / cap)


def parse_bool_series(series: pd.Series) -> pd.Series:
    return series.astype(str).str.strip().str.lower().isin({"true", "1", "yes"})


def validate_columns(df: pd.DataFrame, source: Path) -> None:
    missing = [col for col in REQUIRED_COLUMNS if col not in df.columns]
    if missing:
        missing_list = ", ".join(missing)
        raise ValueError(
            f"Missing required columns in {source}: {missing_list}. "
            f"Expected columns include: {', '.join(REQUIRED_COLUMNS)}"
        )


def load_play_logs(input_path: Path) -> pd.DataFrame:
    if input_path.is_file():
        files = [input_path]
    elif input_path.is_dir():
        files = sorted(input_path.glob("*.csv"))
        if not files:
            raise FileNotFoundError(f"No CSV files found in directory: {input_path}")
    else:
        raise FileNotFoundError(f"Input path does not exist: {input_path}")

    frames: list[pd.DataFrame] = []
    for csv_file in files:
        frame = pd.read_csv(csv_file, encoding="utf-8-sig")
        validate_columns(frame, csv_file)
        frame["time_since_scene_start"] = pd.to_numeric(
            frame["time_since_scene_start"], errors="coerce"
        ).fillna(0.0)
        frame["wrong_action_count"] = pd.to_numeric(
            frame["wrong_action_count"], errors="coerce"
        ).fillna(0).astype(int)
        frame["repeated_question_count"] = pd.to_numeric(
            frame["repeated_question_count"], errors="coerce"
        ).fillna(0).astype(int)
        frame["_source_file"] = str(csv_file)
        frames.append(frame)

    combined = pd.concat(frames, ignore_index=True)
    combined["solved_bool"] = parse_bool_series(combined["solved"])
    combined["event_type"] = combined["event_type"].astype(str)
    return combined


def resolve_clear_time_seconds(group: pd.DataFrame) -> float:
    solved_events = group[group["event_type"] == "puzzle_solved"]
    if not solved_events.empty:
        return float(solved_events["time_since_scene_start"].max())

    if group["solved_bool"].any():
        solved_rows = group[group["solved_bool"]]
        return float(solved_rows["time_since_scene_start"].max())

    return float(group["time_since_scene_start"].max())


def compute_stuck_score(
    clear_time_seconds: float,
    hint_count: int,
    wrong_action_count: int,
    repeated_question_count: int,
) -> float:
    no_progress_time_score = normalize_ratio(clear_time_seconds, TIME_CAP_SECONDS)
    repeated_question_score = normalize_ratio(repeated_question_count, REPEAT_CAP)
    hint_dependency_score = normalize_ratio(hint_count, HINT_CAP)
    wrong_attempt_score = normalize_ratio(wrong_action_count, WRONG_ACTION_CAP)

    weighted = (
        STUCK_WEIGHTS["no_progress_time"] * no_progress_time_score
        + STUCK_WEIGHTS["repeated_question"] * repeated_question_score
        + STUCK_WEIGHTS["hint_dependency"] * hint_dependency_score
        + STUCK_WEIGHTS["wrong_attempt"] * wrong_attempt_score
    )
    return clamp100(weighted)


def build_session_summary(df: pd.DataFrame) -> pd.DataFrame:
    rows: list[dict[str, object]] = []
    group_cols = ["session_id", "scene_name", "puzzle_id"]

    for keys, group in df.groupby(group_cols, dropna=False):
        session_id, scene_name, puzzle_id = keys
        player_id = str(group["anonymous_player_id"].iloc[0])
        hint_count = int((group["event_type"] == "give_hint").sum())
        wrong_action_count = int(group["wrong_action_count"].max())
        repeated_question_count = int(group["repeated_question_count"].max())
        solved = bool(
            group["solved_bool"].any() or (group["event_type"] == "puzzle_solved").any()
        )
        clear_time_seconds = resolve_clear_time_seconds(group)
        stuck_score = compute_stuck_score(
            clear_time_seconds,
            hint_count,
            wrong_action_count,
            repeated_question_count,
        )

        rows.append(
            {
                "session_id": session_id,
                "player_id": player_id,
                "scene_name": scene_name,
                "puzzle_id": puzzle_id,
                "clear_time_seconds": round(clear_time_seconds, 3),
                "hint_count": hint_count,
                "wrong_action_count": wrong_action_count,
                "repeated_question_count": repeated_question_count,
                "solved": solved,
                "stuck_score": round(stuck_score, 3),
            }
        )

    if not rows:
        return pd.DataFrame(columns=list(SESSION_SUMMARY_COLUMNS))

    summary = pd.DataFrame(rows)
    return summary[list(SESSION_SUMMARY_COLUMNS)]


def compute_difficulty_score(
    median_clear_time: float,
    avg_hint_count: float,
    clear_rate: float,
    repeat_question_rate: float,
    abandon_rate: float,
) -> float:
    time_score = (
        normalize_ratio(median_clear_time, TIME_CAP_SECONDS)
        if pd.notna(median_clear_time)
        else 100.0
    )
    hint_score = normalize_ratio(avg_hint_count, HINT_CAP)
    fail_score = clamp100((1.0 - clear_rate) * 100.0)
    repeat_score = clamp100(repeat_question_rate * 100.0)
    abandon_score = clamp100(abandon_rate * 100.0)

    weighted = (
        DIFFICULTY_WEIGHTS["time"] * time_score
        + DIFFICULTY_WEIGHTS["hint"] * hint_score
        + DIFFICULTY_WEIGHTS["fail"] * fail_score
        + DIFFICULTY_WEIGHTS["repeat"] * repeat_score
        + DIFFICULTY_WEIGHTS["abandon"] * abandon_score
    )
    return clamp100(weighted)


def build_puzzle_difficulty(session_summary: pd.DataFrame) -> pd.DataFrame:
    if session_summary.empty:
        return pd.DataFrame(columns=list(PUZZLE_DIFFICULTY_COLUMNS))

    rows: list[dict[str, object]] = []
    for (scene_name, puzzle_id), group in session_summary.groupby(
        ["scene_name", "puzzle_id"], dropna=False
    ):
        session_count = int(len(group))
        clear_rate = float(group["solved"].astype(bool).mean())
        abandon_rate = 1.0 - clear_rate
        solved_times = group.loc[group["solved"].astype(bool), "clear_time_seconds"]
        median_clear_time = (
            float(solved_times.median()) if not solved_times.empty else float("nan")
        )
        avg_hint_count = float(group["hint_count"].mean())
        avg_wrong_action_count = float(group["wrong_action_count"].mean())
        repeat_question_rate = float((group["repeated_question_count"] >= 2).mean())
        difficulty_score = compute_difficulty_score(
            median_clear_time,
            avg_hint_count,
            clear_rate,
            repeat_question_rate,
            abandon_rate,
        )

        rows.append(
            {
                "scene_name": scene_name,
                "puzzle_id": puzzle_id,
                "session_count": session_count,
                "clear_rate": round(clear_rate, 4),
                "median_clear_time": round(median_clear_time, 3)
                if pd.notna(median_clear_time)
                else "",
                "avg_hint_count": round(avg_hint_count, 3),
                "avg_wrong_action_count": round(avg_wrong_action_count, 3),
                "repeat_question_rate": round(repeat_question_rate, 4),
                "abandon_rate": round(abandon_rate, 4),
                "difficulty_score": round(difficulty_score, 3),
            }
        )

    difficulty = pd.DataFrame(rows)
    return difficulty[list(PUZZLE_DIFFICULTY_COLUMNS)]


def analyze_play_logs(input_path: Path) -> tuple[pd.DataFrame, pd.DataFrame]:
    events = load_play_logs(input_path)
    session_summary = build_session_summary(events)
    puzzle_difficulty = build_puzzle_difficulty(session_summary)
    return session_summary, puzzle_difficulty


def write_outputs(
    session_summary: pd.DataFrame,
    puzzle_difficulty: pd.DataFrame,
    output_dir: Path,
    *,
    events: pd.DataFrame | None = None,
) -> tuple[Path, Path, Path]:
    output_dir.mkdir(parents=True, exist_ok=True)
    session_path = output_dir / "session_summary.csv"
    puzzle_path = output_dir / "puzzle_difficulty.csv"
    session_summary.to_csv(session_path, index=False, encoding="utf-8-sig")
    puzzle_difficulty.to_csv(puzzle_path, index=False, encoding="utf-8-sig")

    if events is None:
        events = pd.DataFrame()

    from player_context_summary import write_player_context_summary

    context_path = write_player_context_summary(events, session_summary, output_dir)
    return session_path, puzzle_path, context_path


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Analyze Unity play-log CSV files and emit difficulty summaries.",
    )
    parser.add_argument(
        "--input",
        "-i",
        type=Path,
        required=True,
        help="Directory containing *.csv play logs, or a single CSV file.",
    )
    parser.add_argument(
        "--output",
        "-o",
        type=Path,
        required=True,
        help="Output directory for session_summary.csv, puzzle_difficulty.csv, "
        f"and {PLAYER_CONTEXT_SUMMARY_FILE}.",
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        events = load_play_logs(args.input)
        session_summary = build_session_summary(events)
        puzzle_difficulty = build_puzzle_difficulty(session_summary)
        session_path, puzzle_path, context_path = write_outputs(
            session_summary,
            puzzle_difficulty,
            args.output,
            events=events,
        )
    except (ValueError, FileNotFoundError) as exc:
        print(f"Error: {exc}", file=sys.stderr)
        return 1

    print(f"Wrote {session_path} ({len(session_summary)} rows)")
    print(f"Wrote {puzzle_path} ({len(puzzle_difficulty)} rows)")
    print(f"Wrote {context_path} ({len(session_summary)} sessions)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

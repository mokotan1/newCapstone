"""Build compact player/session context JSON for chatbot prompt enrichment."""

from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import pandas as pd

PLAYER_CONTEXT_SUMMARY_FILE = "player_context_summary.json"
PLAYER_CONTEXT_VERSION = 1

HINT_POLICY_NORMAL = "normal"
HINT_POLICY_LIGHT = "light_hint"
HINT_POLICY_DIRECT = "direct_hint"


def _clamp100(value: float) -> float:
    return float(max(0.0, min(100.0, value)))

PLAYER_CONTEXT_FIELDS: tuple[str, ...] = (
    "session_id",
    "player_id",
    "current_scene",
    "current_puzzle",
    "stuck_score",
    "hint_count",
    "wrong_action_count",
    "repeated_question_count",
    "recommended_hint_policy",
    "solved",
)


def recommend_hint_policy(stuck_score: float) -> str:
    score = int(round(_clamp100(stuck_score)))
    if score <= 39:
        return HINT_POLICY_NORMAL
    if score <= 69:
        return HINT_POLICY_LIGHT
    return HINT_POLICY_DIRECT


def resolve_latest_scene_puzzle(events: pd.DataFrame, session_id: str) -> tuple[str, str]:
    session_events = events.loc[events["session_id"] == session_id]
    if session_events.empty:
        return "", ""

    ordered = session_events.sort_values("event_time", kind="mergesort")
    last_row = ordered.iloc[-1]
    return str(last_row["scene_name"]), str(last_row["puzzle_id"])


def build_player_context_entry(
    *,
    session_id: str,
    player_id: str,
    current_scene: str,
    current_puzzle: str,
    stuck_score: float,
    hint_count: int,
    wrong_action_count: int,
    repeated_question_count: int,
    solved: bool,
) -> dict[str, Any]:
    rounded_stuck_score = int(round(_clamp100(stuck_score)))
    return {
        "session_id": session_id,
        "player_id": player_id,
        "current_scene": current_scene,
        "current_puzzle": current_puzzle,
        "stuck_score": rounded_stuck_score,
        "hint_count": int(hint_count),
        "wrong_action_count": int(wrong_action_count),
        "repeated_question_count": int(repeated_question_count),
        "recommended_hint_policy": recommend_hint_policy(rounded_stuck_score),
        "solved": bool(solved),
    }


def build_player_context_summary(
    events: pd.DataFrame,
    session_summary: pd.DataFrame,
) -> dict[str, Any]:
    if session_summary.empty:
        return {
            "version": PLAYER_CONTEXT_VERSION,
            "generated_at": datetime.now(timezone.utc).isoformat(),
            "sessions": [],
        }

    sessions: list[dict[str, Any]] = []
    for session_id in sorted(session_summary["session_id"].unique()):
        current_scene, current_puzzle = resolve_latest_scene_puzzle(events, str(session_id))
        matching = session_summary.loc[session_summary["session_id"] == session_id]

        if current_scene and current_puzzle:
            row_candidates = matching.loc[
                (matching["scene_name"] == current_scene)
                & (matching["puzzle_id"] == current_puzzle)
            ]
            row = row_candidates.iloc[0] if not row_candidates.empty else matching.iloc[0]
        else:
            row = matching.iloc[0]
            current_scene = str(row["scene_name"])
            current_puzzle = str(row["puzzle_id"])

        sessions.append(
            build_player_context_entry(
                session_id=str(row["session_id"]),
                player_id=str(row["player_id"]),
                current_scene=current_scene,
                current_puzzle=current_puzzle,
                stuck_score=float(row["stuck_score"]),
                hint_count=int(row["hint_count"]),
                wrong_action_count=int(row["wrong_action_count"]),
                repeated_question_count=int(row["repeated_question_count"]),
                solved=bool(row["solved"]),
            )
        )

    return {
        "version": PLAYER_CONTEXT_VERSION,
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "sessions": sessions,
    }


def write_player_context_summary(
    events: pd.DataFrame,
    session_summary: pd.DataFrame,
    output_dir: Path,
) -> Path:
    output_dir.mkdir(parents=True, exist_ok=True)
    payload = build_player_context_summary(events, session_summary)
    output_path = output_dir / PLAYER_CONTEXT_SUMMARY_FILE
    output_path.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    return output_path


def lookup_session_context(
    payload: dict[str, Any],
    session_id: str,
) -> dict[str, Any] | None:
    for entry in payload.get("sessions", []):
        if entry.get("session_id") == session_id:
            return entry
    return None


def format_prompt_snippet(context: dict[str, Any]) -> str:
    """Compact one-line summary for backend system prompt injection."""
    return (
        f"[player_context] session={context['session_id']} "
        f"scene={context['current_scene']} puzzle={context['current_puzzle']} "
        f"stuck_score={context['stuck_score']} "
        f"hint_policy={context['recommended_hint_policy']} "
        f"hints={context['hint_count']} wrong={context['wrong_action_count']} "
        f"repeats={context['repeated_question_count']} solved={context['solved']}"
    )

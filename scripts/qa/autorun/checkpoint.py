"""Repair checkpoint snapshot for resume-after-patch (design §7)."""

from __future__ import annotations

import json
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any, Mapping


@dataclass(frozen=True)
class Checkpoint:
    scenario_step: str
    git_commit: str
    active_scene: str
    qa_profile_id: str
    snapshot: Mapping[str, Any]
    console_cursor: int
    capability_registry_version: str

    def to_dict(self) -> dict[str, Any]:
        payload = asdict(self)
        payload["snapshot"] = dict(self.snapshot)
        return payload

    @classmethod
    def from_dict(cls, data: Mapping[str, Any]) -> Checkpoint:
        required = (
            "scenario_step",
            "git_commit",
            "active_scene",
            "qa_profile_id",
            "snapshot",
            "console_cursor",
            "capability_registry_version",
        )
        missing = [key for key in required if key not in data]
        if missing:
            raise ValueError(f"checkpoint missing fields: {missing}")
        return cls(
            scenario_step=str(data["scenario_step"]),
            git_commit=str(data["git_commit"]),
            active_scene=str(data["active_scene"]),
            qa_profile_id=str(data["qa_profile_id"]),
            snapshot=dict(data["snapshot"] or {}),
            console_cursor=int(data["console_cursor"]),
            capability_registry_version=str(data["capability_registry_version"]),
        )


def save_checkpoint(path: Path | str, checkpoint: Checkpoint) -> None:
    if not checkpoint.git_commit.strip():
        raise ValueError("git_commit must be non-empty")
    target = Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(
        json.dumps(checkpoint.to_dict(), indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def load_checkpoint(path: Path | str) -> Checkpoint:
    payload = json.loads(Path(path).read_text(encoding="utf-8"))
    if not isinstance(payload, dict):
        raise ValueError("checkpoint JSON must be an object")
    return Checkpoint.from_dict(payload)

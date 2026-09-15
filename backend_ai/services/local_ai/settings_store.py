from __future__ import annotations

import json
import os
from dataclasses import asdict, dataclass
from pathlib import Path

from services.local_ai.types import (
    VALID_MODES,
    VALID_OFFLOADS,
    GpuOffload,
    RequestedMode,
)


@dataclass(frozen=True)
class LocalAiSettings:
    mode: RequestedMode = "gpu"
    gpu_offload: GpuOffload = "medium"
    runtime_id: str = ""
    model_id: str = ""


def load_settings(path: Path) -> LocalAiSettings:
    if not path.is_file():
        return LocalAiSettings()
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError, UnicodeDecodeError):
        return LocalAiSettings(mode="cpu")
    if not isinstance(payload, dict):
        return LocalAiSettings(mode="cpu")
    mode = payload.get("mode", "gpu")
    offload = payload.get("gpu_offload", "medium")
    if mode not in VALID_MODES:
        mode = "cpu"
    if offload not in VALID_OFFLOADS:
        offload = "medium"
    runtime_id = payload.get("runtime_id", "")
    model_id = payload.get("model_id", "")
    if not isinstance(runtime_id, str):
        runtime_id = ""
    if not isinstance(model_id, str):
        model_id = ""
    return LocalAiSettings(
        mode=mode,  # type: ignore[arg-type]
        gpu_offload=offload,  # type: ignore[arg-type]
        runtime_id=runtime_id,
        model_id=model_id,
    )


def save_settings(path: Path, settings: LocalAiSettings) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp_path = path.with_suffix(path.suffix + ".tmp")
    tmp_path.write_text(
        json.dumps(asdict(settings), ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    os.replace(tmp_path, path)

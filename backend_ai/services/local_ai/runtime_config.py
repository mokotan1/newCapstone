from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Literal

RuntimeBackend = Literal["cpu", "gpu"]


def write_game_runtime_config(
    path: Path,
    *,
    backend: RuntimeBackend,
    model_id: str,
    max_num_tokens: int,
) -> Path:
    payload = {
        "default": {
            "backend": backend,
            "max_num_tokens": max_num_tokens,
        },
        "models": {
            model_id: {
                "backend": backend,
                "max_num_tokens": max_num_tokens,
            },
        },
    }
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp_path = path.with_suffix(path.suffix + ".tmp")
    tmp_path.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    os.replace(tmp_path, path)
    return path

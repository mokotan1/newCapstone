from __future__ import annotations

import os
from pathlib import Path


def default_local_ai_dir() -> Path:
    base = os.environ.get("LOCALAPPDATA") or str(Path.home() / "AppData" / "Local")
    return Path(base) / "Disputatio" / "local-ai"


def default_cuda_dir() -> Path:
    return default_local_ai_dir() / "cuda"


def resolve_cuda_dir(configured: str) -> Path:
    path = (configured or "").strip()
    return Path(path) if path else default_cuda_dir()

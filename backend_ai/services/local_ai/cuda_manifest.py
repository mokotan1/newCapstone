from __future__ import annotations

import json
from pathlib import Path


def cuda_manifest_is_pinned(path: Path) -> bool:
    """Enable CUDA only after Gate 1 writes gate_passed=true. Presence is not a pin."""
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError, UnicodeDecodeError):
        return False
    if not isinstance(payload, dict):
        return False
    return payload.get("gate_passed") is True

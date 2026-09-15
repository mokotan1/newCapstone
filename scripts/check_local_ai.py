"""Print loopback LiteRT/Gemma health for the desktop local-AI path."""

from __future__ import annotations

import json
import sys
from pathlib import Path

_BACKEND_DIR = Path(__file__).resolve().parents[1] / "backend_ai"
if str(_BACKEND_DIR) not in sys.path:
    sys.path.insert(0, str(_BACKEND_DIR))

from config import get_settings
from local_runtime import check_local_runtime


def main() -> int:
    settings = get_settings()
    status = check_local_runtime(settings)
    print(
        json.dumps(
            {
                "ai_provider": settings.ai_provider,
                "base_url": settings.local_ai_base_url,
                "model": settings.local_ai_model,
                "available": status.ollama_or_litert_available,
                "model_available": status.model_available,
                "error": status.error,
            },
            ensure_ascii=False,
        )
    )
    return 0 if status.model_available else 1


if __name__ == "__main__":
    raise SystemExit(main())

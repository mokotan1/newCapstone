"""Flushing progress lines for the Unity CLI status CMD and hop runners."""

from __future__ import annotations

import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import TextIO

ROOT = Path(__file__).resolve().parents[3]
DEFAULT_PROGRESS_PATH = ROOT / "docs" / "qa" / "runs" / "_progress-last.txt"
MAX_PROGRESS_LINES = 50


def emit_progress(
    message: str,
    *,
    stream: TextIO | None = None,
    path: Path | None = None,
) -> str:
    """UTC 시각과 메시지를 stdout에 즉시 찍고 최근 진행 파일에 남긴다."""
    line = f"{datetime.now(timezone.utc).strftime('%H:%M:%S')}Z {message}".rstrip()
    out = sys.stdout if stream is None else stream
    print(line, file=out, flush=True)
    target = DEFAULT_PROGRESS_PATH if path is None else path
    target.parent.mkdir(parents=True, exist_ok=True)
    previous: list[str] = []
    if target.is_file():
        previous = target.read_text(encoding="utf-8").splitlines()
    previous.append(line)
    target.write_text(
        "\n".join(previous[-MAX_PROGRESS_LINES:]) + "\n",
        encoding="utf-8",
    )
    return line


def read_progress_lines(path: Path | None = None, limit: int = 15) -> list[str]:
    """상태 창에 보여줄 최근 진행 줄을 읽는다."""
    target = DEFAULT_PROGRESS_PATH if path is None else path
    if not target.is_file():
        return []
    lines = [item for item in target.read_text(encoding="utf-8").splitlines() if item.strip()]
    return lines[-max(1, limit) :]

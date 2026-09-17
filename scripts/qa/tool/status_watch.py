"""One snapshot for the Unity CLI Status CMD: HTTP code, editor state, hop progress."""

from __future__ import annotations

import os
import subprocess
import sys
from collections.abc import Mapping, Sequence
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[3]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.qa.tool.live import (
    DEFAULT_PROJECT_PATH,
    format_health_line,
    parse_heartbeat,
    probe_health_detail,
    read_heartbeat_file,
    should_fetch_unity_console,
)
from scripts.qa.tool.progress import read_progress_lines

CLI_EXE = Path(os.environ.get("LOCALAPPDATA", "")) / "unity-cli" / "unity-cli.exe"
STATUS_TIMEOUT_SECONDS = 8


def build_watch_report(
    *,
    heartbeat: Mapping[str, Any],
    health: Mapping[str, Any],
    status_text: str,
    progress_lines: Sequence[str],
    console_text: str | None,
) -> str:
    """상태 창에 그릴 텍스트. console_text가 None이면 리스너 다운으로 console을 생략한다."""
    parsed = parse_heartbeat(heartbeat)
    lines = [
        "Unity CLI status watcher",
        f"Time: {datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M:%S')}Z",
        (
            f"Heartbeat: state={parsed.get('state') or '-'} "
            f"pid={parsed.get('pid') or '-'} port={parsed.get('port') or 0}"
        ),
        format_health_line(health),
        "",
        str(status_text).rstrip() or "(no unity-cli status)",
        "",
        "Hop progress (latest)",
        "------------------------------------------------------------",
    ]
    if progress_lines:
        lines.extend(str(item) for item in progress_lines)
    else:
        lines.append("(none yet)")
    lines.append("------------------------------------------------------------")
    lines.append("")
    if console_text is None:
        lines.append("Skip console: listener HTTP is not 2xx (avoids 90s hang).")
    else:
        lines.append("Recent Unity console")
        lines.append("------------------------------------------------------------")
        lines.append(console_text.rstrip() or "(empty)")
        lines.append("------------------------------------------------------------")
    return "\n".join(lines) + "\n"


def _run_cli(args: list[str]) -> str:
    """짧은 timeout으로 unity-cli 한 명령을 실행한다. hang 시 그 사실을 반환한다."""
    if not CLI_EXE.is_file():
        return f"unity-cli.exe missing: {CLI_EXE}"
    try:
        completed = subprocess.run(
            [
                str(CLI_EXE),
                "--project",
                "disputatio",
                "--timeout",
                str(STATUS_TIMEOUT_SECONDS * 1000),
                *args,
            ],
            cwd=str(ROOT),
            capture_output=True,
            check=False,
            timeout=STATUS_TIMEOUT_SECONDS + 2,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
    except subprocess.TimeoutExpired:
        return f"unity-cli timed out: {' '.join(args)}"
    stdout = completed.stdout or ""
    stderr = completed.stderr or ""
    return (stdout + stderr).strip()


def main() -> None:
    """heartbeat + GET /health HTTP 코드 + status, health가 살아 있을 때만 console."""
    beat = read_heartbeat_file(DEFAULT_PROJECT_PATH)
    parsed = parse_heartbeat(beat)
    health = probe_health_detail(int(parsed.get("port") or 0))
    status_text = _run_cli(["status"])
    console_text = None
    if should_fetch_unity_console(bool(health.get("ok"))):
        console_text = _run_cli(
            ["console", "--type", "error,warning,log", "--lines", "15"]
        )
    report = build_watch_report(
        heartbeat=beat,
        health=health,
        status_text=status_text,
        progress_lines=read_progress_lines(),
        console_text=console_text,
    )
    sys.stdout.write(report)
    sys.stdout.flush()
    raise SystemExit(0 if health.get("ok") or parsed.get("state") else 1)


if __name__ == "__main__":
    main()

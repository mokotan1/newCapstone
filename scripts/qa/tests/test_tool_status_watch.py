"""Unity CLI status CMD snapshot (HTTP code + progress, no hang on dead listener)."""

from __future__ import annotations

import io
from pathlib import Path

from scripts.qa.tool.progress import emit_progress
from scripts.qa.tool.status_watch import build_watch_report


def test_build_watch_report_shows_http_status_and_skips_console() -> None:
    report = build_watch_report(
        heartbeat={"state": "playing", "port": 8103, "pid": 45700},
        health={
            "ok": False,
            "status_code": None,
            "error": "timeout",
            "url": "http://127.0.0.1:8103/health",
        },
        status_text="Unity: playing\n  PID:     45700\n",
        progress_lines=["13:24:01 hop hall.nav.probe"],
        console_text=None,
    )
    assert "HTTP ---" in report
    assert "timeout" in report
    assert "playing" in report
    assert "45700" in report
    assert "hall.nav.probe" in report
    assert "skip console" in report.lower()


def test_build_watch_report_includes_console_when_health_ok() -> None:
    report = build_watch_report(
        heartbeat={"state": "ready", "port": 8097, "pid": 1},
        health={
            "ok": True,
            "status_code": 200,
            "error": "",
            "url": "http://127.0.0.1:8097/health",
        },
        status_text="Unity: ready\n",
        progress_lines=[],
        console_text="[Log] hello",
    )
    assert "HTTP 200" in report
    assert "[Log] hello" in report
    assert "skip console" not in report.lower()


def test_emit_progress_flushes_and_appends_file(tmp_path: Path) -> None:
    stream = io.StringIO()
    log_path = tmp_path / "progress.txt"
    emit_progress("reset-api", stream=stream, path=log_path)
    assert "reset-api" in stream.getvalue()
    assert "reset-api" in log_path.read_text(encoding="utf-8")

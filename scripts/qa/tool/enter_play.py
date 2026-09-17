"""Enter Play Mode for the Hall live vertical. Poll status; do not hang on exec."""

from __future__ import annotations

import os
import subprocess
import time
from pathlib import Path

from scripts.qa.tool.live import (
    probe_health_http,
    read_heartbeat_file,
    wait_until_http_ready,
)

ROOT = Path(__file__).resolve().parents[3]
CLI_EXE = Path(os.environ.get("LOCALAPPDATA", "")) / "unity-cli" / "unity-cli.exe"
OUT = ROOT / "docs" / "qa" / "runs" / "_play-last.txt"


def _run(args: list[str], timeout: int) -> subprocess.CompletedProcess[bytes]:
    """unity-cli 한 명령을 바이트로 받아 타임아웃 시 로그 파일에 남긴다."""
    # TODO(AC19): play --wait는 connection closed가 나도 status가 playing/reloading이면 성공으로 본다.
    try:
        return subprocess.run(
            [str(CLI_EXE), "--project", "disputatio", *args],
            cwd=str(ROOT),
            capture_output=True,
            check=False,
            timeout=timeout,
        )
    except subprocess.TimeoutExpired as exc:
        payload = (
            b"timeout\nstdout:\n"
            + (exc.stdout or b"")
            + b"\nstderr:\n"
            + (exc.stderr or b"")
        )
        OUT.write_bytes(payload)
        raise


def main() -> None:
    """Play Mode에 들어가고 HTTP health가 살아날 때까지 폴링한다."""
    # TODO(AC22): play --wait connection closed 후에도 heartbeat 포트로 health를 확인한다.
    OUT.parent.mkdir(parents=True, exist_ok=True)
    play = _run(["--timeout", "45000", "editor", "play", "--wait"], timeout=50)

    health = wait_until_http_ready(
        read_heartbeat=read_heartbeat_file,
        probe_health=probe_health_http,
        sleep=time.sleep,
        timeout_seconds=45.0,
        interval_seconds=1.0,
    )
    status = _run(["status"], timeout=15)
    OUT.write_bytes(
        b"play_rc="
        + str(play.returncode).encode("ascii")
        + b"\nplay_stdout:\n"
        + (play.stdout or b"")
        + b"\nplay_stderr:\n"
        + (play.stderr or b"")
        + b"\nhealth="
        + str(health).encode("utf-8", errors="replace")
        + b"\nstatus_rc="
        + str(status.returncode).encode("ascii")
        + b"\nstatus:\n"
        + (status.stdout or b"")
        + b"\nstatus_stderr:\n"
        + (status.stderr or b"")
        + b"\n"
    )
    print("play_rc", play.returncode)
    print("health", health)
    print("wrote play log")
    raise SystemExit(0 if health.get("ok") else 1)


if __name__ == "__main__":
    main()

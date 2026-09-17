"""Enter Play Mode then immediately run Hall hops. Avoid the health-death gap."""

from __future__ import annotations

import json
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.qa.tool.enter_play import CLI_EXE
from scripts.qa.tool.live import (
    format_health_line,
    parse_status,
    probe_health_detail,
    read_heartbeat_file,
    wait_until_http_ready,
)
from scripts.qa.tool.live_vertical import run_live_vertical, utc_run_root
from scripts.qa.tool.progress import emit_progress
from scripts.qa.tool.run_live_now import list_capabilities, read_status_text

DISABLE_DOMAIN_RELOAD = (
    "UnityEditor.EditorSettings.enterPlayModeOptionsEnabled = true; "
    "UnityEditor.EditorSettings.enterPlayModeOptions = "
    "UnityEditor.EnterPlayModeOptions.DisableDomainReload; "
    "return UnityEditor.EditorSettings.enterPlayModeOptionsEnabled.ToString();"
)


def _cli(args: list[str], timeout: int) -> subprocess.CompletedProcess[bytes]:
    """unity-cli.exe 한 명령을 바이트로 실행한다."""
    return subprocess.run(
        [str(CLI_EXE), "--project", "disputatio", *args],
        cwd=str(ROOT),
        capture_output=True,
        check=False,
        timeout=timeout,
    )


def _wait_health(timeout_seconds: float) -> dict[str, object]:
    """하트비트 포트의 GET /health가 살아날 때까지 폴링하고 HTTP 코드를 로그한다."""

    def probe_health(port: int) -> bool:
        detail = probe_health_detail(port)
        emit_progress(format_health_line(detail))
        return bool(detail["ok"])

    return wait_until_http_ready(
        read_heartbeat=read_heartbeat_file,
        probe_health=probe_health,
        sleep=time.sleep,
        timeout_seconds=timeout_seconds,
        interval_seconds=1.0,
    )


def main() -> None:
    """도메인 리로드를 끈 뒤 Play Mode에 들어가 즉시 수직 실행한다."""
    # TODO(AC19): DisableDomainReload는 EditorSettings를 건드린다. 끝나면 복원할 것.
    emit_progress("run_play_hops start")
    health = _wait_health(15.0)
    if not health.get("ok"):
        emit_progress(f"pre-play health blocked {health}")
        print("pre-play health blocked", health)
        raise SystemExit(2)
    emit_progress("disable domain reload")
    disable = _cli(["--timeout", "30000", "exec", DISABLE_DOMAIN_RELOAD], timeout=40)
    print("disable_domain_reload", disable.returncode, disable.stdout[:200])
    emit_progress("editor play")
    play = _cli(["--timeout", "45000", "editor", "play", "--wait"], timeout=55)
    print("play_rc", play.returncode)
    health = _wait_health(45.0)
    print("play_health", health)
    emit_progress(f"play_health ok={health.get('ok')} port={health.get('port')} state={health.get('state')}")
    if not health.get("ok"):
        raise SystemExit(3)
    status_text = read_status_text()
    parsed = parse_status(status_text)
    pid = str(parsed.get("editorPid") or "")
    emit_progress(f"status pid={pid}")
    caps = list_capabilities()
    root = utc_run_root("run-hall-to-kitchen")
    emit_progress(f"vertical {root}")
    result = run_live_vertical(
        run_root=root,
        status_text=status_text,
        capability_payload=caps,
        previous_connection={
            "editorPid": pid,
            "leaseId": pid,
            "editorConnected": True,
        },
    )
    (root / "play-health.json").write_text(
        json.dumps({"health": health, "pid": pid}, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print("run", root)
    print(result.get("reasonCode"), result.get("runVerdict"), result.get("featureVerified"))
    emit_progress(
        f"done {result.get('runVerdict')} {result.get('reasonCode')} verified={result.get('featureVerified')}"
    )
    emit_progress("editor stop")
    stop = _cli(["--timeout", "30000", "editor", "stop"], timeout=40)
    print("stop_rc", stop.returncode)
    raise SystemExit(0)


if __name__ == "__main__":
    main()

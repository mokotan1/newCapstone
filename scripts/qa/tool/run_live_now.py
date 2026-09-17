"""Hall nav probe/vertical. Play Mode는 health endpoint를 죽일 수 있어 Edit Mode에서 먼저 친다."""

from __future__ import annotations

import json
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.qa.tool.live import (
    UnityCliGateway,
    _run_cli,
    parse_status,
    probe_health_http,
    read_heartbeat_file,
    wait_until_http_ready,
)
from scripts.qa.tool.live_vertical import run_live_vertical, utc_run_root


def read_status_text() -> str:
    """unity-cli status 원문을 읽는다. Play Mode 중에도 레지스트리 status는 살아 있을 수 있다."""
    completed = _run_cli(["status"])
    return str(completed.get("stdout") or "")


def list_capabilities() -> dict[str, object]:
    """live DeveloperQa capability.list를 호출한다."""
    gateway = UnityCliGateway()
    return gateway.invoke("capability", "list", "")


def probe_hall_route() -> dict[str, object]:
    """Hall probe capability로 현재 씬/컨트롤러 스냅샷을 읽는다. Kitchen 도착 판정이 아니다."""
    # TODO(AC19): Play Mode에서 unity-cli exec는 리스너를 죽일 수 있다. qa_dev_exec만 사용.
    # TODO(AC19): Play Mode 중 qa_dev_exec도 timeout 나면 Edit Mode로 되돌리거나 Editor를 재시작한다.
    gateway = UnityCliGateway()
    return gateway.invoke("interaction", "invoke", "hall.nav.probe")


def main() -> None:
    """status → HTTP health → capability.list → probe → 수직 러너 보고서를 docs/qa/runs에 쓴다."""
    # TODO(AC19): screenshot·console delta·editor stop·qa_recover는 러너 이후에 이어서 할 것.
    # TODO(AC07): Play Mode HTTP health가 죽으면 Kitchen 도착 증거는 이 스크립트만으로 완성되지 않는다.
    # TODO(AC23): 이 스크립트는 featureVerified를 True로 올리면 안 된다.
    status_text = read_status_text()
    parsed = parse_status(status_text)
    pid = str(parsed.get("editorPid") or "")
    health = wait_until_http_ready(
        read_heartbeat=read_heartbeat_file,
        probe_health=probe_health_http,
        sleep=time.sleep,
        timeout_seconds=20.0,
        interval_seconds=0.5,
    )
    if not health.get("ok"):
        root = utc_run_root("run-hall-to-kitchen")
        root.mkdir(parents=True, exist_ok=True)
        (root / "probe.json").write_text(
            json.dumps({"status": parsed, "health": health}, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        print("pid", pid)
        print("health blocked", health)
        raise SystemExit(2)
    caps = list_capabilities()
    probe = probe_hall_route()
    root = utc_run_root("run-hall-to-kitchen")
    root.mkdir(parents=True, exist_ok=True)
    (root / "probe.json").write_text(
        json.dumps(
            {
                "status": parsed,
                "capabilities": caps,
                "probe": probe,
            },
            indent=2,
            sort_keys=True,
            default=str,
        )
        + "\n",
        encoding="utf-8",
    )
    print("pid", pid)
    print("probe_code", probe.get("code"))
    print("probe_scene", (probe.get("data") or {}).get("activeScene"))
    if probe.get("code") == "EnvironmentBlocked" or probe.get("returncode") == 124:
        print("probe blocked; skip remaining clicks")
        raise SystemExit(2)
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
    print("run", root)
    print(result.get("reasonCode"), result.get("runVerdict"), result.get("featureVerified"))
    raise SystemExit(0)


if __name__ == "__main__":
    main()

"""One-shot live Hall→Kitchen driver. Does not treat stub output as PASS."""

from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path
from time import sleep
from typing import Any

from scripts.qa.tool.live import (
    UnityCliGateway,
    build_live_snapshot,
    parse_capability_ids,
)
from scripts.qa.tool.progress import emit_progress
from scripts.qa.tool.runner import run_hall_to_kitchen


def run_stale_pid_probe(run_root: Path, current_pid: str, previous_pid: str) -> dict[str, Any]:
    """이전 Editor PID가 남았을 때 stale-pid로 BLOCKED 되는지 확인한다. 게이트웨이는 호출하지 않는다."""
    snapshot = build_live_snapshot(
        f"Unity: ready\n  PID:     {current_pid}\n",
        ["hall.nav.click-kitchen-entry", "hall.nav.assert-route"],
        lease_id=current_pid,
    )

    class _NoInvokeGateway:
        kind = "live"

        def invoke(self, family: str, name: str, target: str) -> dict[str, Any]:
            """stale-pid 경로에서는 어떤 Unity mutation도 나가면 안 된다."""
            raise AssertionError("stale-pid must not invoke the gateway")

    return run_hall_to_kitchen(
        run_root=run_root,
        gateway=_NoInvokeGateway(),
        snapshot=snapshot,
        previous_connection={
            "editorPid": previous_pid,
            "leaseId": current_pid,
            "editorConnected": True,
        },
    )


def run_live_vertical(
    *,
    run_root: Path,
    status_text: str,
    capability_payload: dict[str, Any],
    previous_connection: dict[str, Any] | None = None,
) -> dict[str, Any]:
    """라이브 UnityCliGateway로 Hall→Kitchen 수직 실행을 돌린다. Kitchen 미도착이면 PASS가 아니다."""
    # TODO(AC19): screenshot 파일 수집·console classify·qa_recover는 아직 이 함수 밖이다.
    # TODO(AC06): event-system 레이어는 아직 api invoke와 동일하다. interaction.pointer로 분리할 것.
    snapshot = build_live_snapshot(
        status_text,
        parse_capability_ids(capability_payload),
        lease_id=str(previous_connection.get("leaseId") if previous_connection else ""),
    )
    if previous_connection is None:
        snapshot["leaseId"] = str(snapshot.get("editorPid") or "")
    gateway = UnityCliGateway(progress=emit_progress)
    return run_hall_to_kitchen(
        run_root=run_root,
        gateway=gateway,
        snapshot=snapshot,
        previous_connection=previous_connection,
        wait_attempts=40,
        wait_sleep=sleep,
    )


def utc_run_root(suffix: str) -> Path:
    """UTC 시각 스탬프가 붙은 docs/qa/runs 디렉터리 경로를 만든다."""
    stamp = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H-%M-%SZ")
    return Path("docs/qa/runs") / f"{stamp}-{suffix}"

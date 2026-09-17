"""One-shot live Hall→Kitchen driver. Does not treat stub output as PASS."""

from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path
from time import sleep
from typing import Any

from scripts.qa.tool.coordinator import Coordinator
from scripts.qa.tool.dialogue import settle_dialogue
from scripts.qa.tool.lease import pid_is_alive
from scripts.qa.tool.lifecycle import UnityCliLifecycle
from scripts.qa.tool.live import (
    UnityCliGateway,
    _run_cli,
    build_live_snapshot,
    parse_capability_ids,
    probe_health_http,
    read_heartbeat_file,
)
from scripts.qa.tool.progress import emit_progress
from scripts.qa.tool.runner import run_hall_to_kitchen
from scripts.qa.tool.transport import TRANSPORT_DOWN_CODE, TransportGuard

LEASE_PATH = Path("docs/qa/runs/_lease.json")
LEASE_OWNER = "qa-tool"


class _LifecycleQaGateway:
    """Coordinator cleanup가 실제 qa_recover를 호출하도록 연결한다."""

    def __init__(self, lifecycle: UnityCliLifecycle) -> None:
        self._lifecycle = lifecycle

    def mutate(self, command_id: str, name: str) -> dict[str, Any]:
        """라이브 hop은 VerticalGateway가 담당한다. 여기선 저널용 성공만 남긴다."""
        return {"ok": True, "commandId": command_id, "name": name}

    def query_command(self, command_id: str) -> dict[str, Any]:
        """유실 mutation 재전송 금지. 상태만 있다고 보고한다."""
        return {"found": True, "commandId": command_id}

    def switch_profile(self, profile_id: str) -> dict[str, Any]:
        """프로필 전환은 qa_status 격리가 담당. 부분 실패 테스트용 계약만 맞춘다."""
        return {"ok": True, "acquired": [profile_id]}

    def release_profile(self, profile_id: str) -> None:
        """부분 획득 롤백. 라이브 경로에서는 switch가 성공만 반환한다."""
        return

    def cleanup(self) -> dict[str, Any]:
        """qa_recover 결과를 coordinator complete 규칙으로 변환한다."""
        result = self._lifecycle.recover()
        if str(result.get("code") or "") == TRANSPORT_DOWN_CODE:
            return {"ok": False, "uncertain": True}
        if result.get("uncertain"):
            return {"ok": False, "uncertain": True}
        return {"ok": bool(result.get("ok")), "uncertain": False}


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
    """TransportGuard + lease + journal + qa_recover + 대사 진행 + 증거 수집으로 수직 실행한다."""
    snapshot = build_live_snapshot(
        status_text,
        parse_capability_ids(capability_payload),
        lease_id=str(previous_connection.get("leaseId") if previous_connection else ""),
    )
    if previous_connection is None:
        snapshot["leaseId"] = str(snapshot.get("editorPid") or "")

    heartbeat = read_heartbeat_file()
    try:
        port = int(heartbeat.get("port") or 0)
    except (TypeError, ValueError):
        port = 0

    def health_probe() -> bool:
        """heartbeat 포트의 /health가 200이어야 다음 CLI를 보낸다."""
        return port > 0 and probe_health_http(port)

    inner = UnityCliGateway(progress=emit_progress)
    gateway = TransportGuard(inner, health_probe=health_probe)
    lifecycle = UnityCliLifecycle(runner=_run_cli, health_probe=health_probe)
    coordinator = Coordinator(run_root=run_root, gateway=_LifecycleQaGateway(lifecycle))
    now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    result = run_hall_to_kitchen(
        run_root=run_root,
        gateway=gateway,
        snapshot=snapshot,
        previous_connection=previous_connection,
        wait_attempts=40,
        wait_sleep=sleep,
        lease_path=LEASE_PATH,
        lease_owner=LEASE_OWNER,
        pid_alive=pid_is_alive,
        now=now,
        coordinator=coordinator,
        lifecycle=lifecycle,
        settle_dialogue=settle_dialogue,
    )
    result["transportTripped"] = bool(gateway.tripped)
    result["transportReason"] = gateway.trip_reason
    return result


def utc_run_root(suffix: str) -> Path:
    """UTC 시각 스탬프가 붙은 docs/qa/runs 디렉터리 경로를 만든다."""
    stamp = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H-%M-%SZ")
    return Path("docs/qa/runs") / f"{stamp}-{suffix}"

"""Live unity-cli gateway parsing (design AC19)."""

from __future__ import annotations

import urllib.error
from pathlib import Path

from scripts.qa.tool.live import (
    UnityCliGateway,
    build_live_snapshot,
    format_health_line,
    heartbeat_path,
    parse_capability_ids,
    parse_heartbeat,
    parse_status,
    probe_health_detail,
    probe_health_http,
    should_fetch_unity_console,
    wait_until_http_ready,
)
from scripts.qa.tool.live_vertical import run_stale_pid_probe


def test_parse_status_treats_reloading_as_connected() -> None:
    snapshot = parse_status("Unity: reloading\n  PID:     16840\n")
    assert snapshot["editorConnected"] is True
    assert snapshot["editorPid"] == "16840"
    snapshot = parse_status("Unity: playing\n  PID:     36340\n")
    assert snapshot["editorConnected"] is True
    assert snapshot["editorPid"] == "36340"


def test_parse_status_extracts_ready_pid() -> None:
    snapshot = parse_status(
        "Unity: ready\n  Project: D:/Capstone/newCapstone/disputatio\n  PID:     45388\n"
    )
    assert snapshot["editorConnected"] is True
    assert snapshot["editorPid"] == "45388"


def test_gateway_invoke_sends_qa_dev_exec_params() -> None:
    seen: list[list[str]] = []

    def runner(args: list[str]) -> dict[str, object]:
        seen.append(list(args))
        return {
            "returncode": 0,
            "stdout": '{"code":"AssertionFailed","data":{"activeScene":"Hall_playerble"}}',
            "stderr": "",
        }

    gateway = UnityCliGateway(runner=runner)
    payload = gateway.invoke("interaction", "invoke", "hall.nav.assert-route")
    assert seen[0][0] == "qa_dev_exec"
    assert seen[0][2] == "interaction"
    assert seen[0][-1] == "hall.nav.assert-route"
    assert payload["code"] == "AssertionFailed"
    assert payload["ok"] is False


def test_gateway_parses_json_when_cli_prints_update_trailer() -> None:
    gateway = UnityCliGateway(
        runner=lambda args: {
            "returncode": 0,
            "stdout": (
                '{"code":"Ok","data":{"activeScene":"Hall_playerble"}}\n'
                "Update available: v0.3.21 → v0.4.1\n"
            ),
            "stderr": "",
        }
    )
    payload = gateway.invoke("interaction", "invoke", "hall.nav.probe")
    assert payload["ok"] is True
    assert payload["code"] == "Ok"
    assert payload["data"]["activeScene"] == "Hall_playerble"


def test_gateway_parses_assertion_failed_from_error_details() -> None:
    gateway = UnityCliGateway(
        runner=lambda args: {
            "returncode": 1,
            "stdout": "",
            "stderr": (
                "Error: Hall nav assert-route failed: destination-mismatch\n"
                'Details: {"code":"AssertionFailed","data":{"activeScene":"Hall_playerble"}}\n'
            ),
        }
    )
    payload = gateway.invoke("interaction", "invoke", "hall.nav.assert-route")
    assert payload["ok"] is False
    assert payload["code"] == "AssertionFailed"
    assert payload["data"]["activeScene"] == "Hall_playerble"


def test_parse_capability_ids_splits_registry_csv() -> None:
    ids = parse_capability_ids(
        {
            "data": {
                "current_capabilities": (
                    "hall.nav.click-kitchen-entry,hall.nav.assert-route,studyroom.mirror.grant-bookmark"
                )
            }
        }
    )
    assert "hall.nav.click-kitchen-entry" in ids
    assert "hall.nav.assert-route" in ids


def test_build_live_snapshot_uses_ready_pid_and_live_caps() -> None:
    snapshot = build_live_snapshot(
        "Unity: ready\n  PID:     16840\n",
        ["hall.nav.click-kitchen-entry", "hall.nav.assert-route"],
        lease_id="lease-live",
    )
    assert snapshot["editorConnected"] is True
    assert snapshot["editorPid"] == "16840"
    assert snapshot["leaseId"] == "lease-live"
    assert snapshot["projectPath"] == "disputatio"
    assert snapshot["liveCapabilityIds"] == [
        "hall.nav.click-kitchen-entry",
        "hall.nav.assert-route",
    ]


def test_stale_pid_probe_blocks_without_gateway_calls(tmp_path: Path) -> None:
    result = run_stale_pid_probe(tmp_path, current_pid="16840", previous_pid="36340")
    assert result["reasonCode"] == "stale-pid"
    assert result["runVerdict"] == "BLOCKED"
    assert result["featureVerified"] is False


def test_parse_heartbeat_reads_port_and_listener_flag() -> None:
    parsed = parse_heartbeat(
        {
            "state": "playing",
            "port": 8094,
            "pid": 44620,
            "listenerRunning": True,
        }
    )
    assert parsed["port"] == 8094
    assert parsed["pid"] == "44620"
    assert parsed["state"] == "playing"
    assert parsed["listenerRunning"] is True


def test_heartbeat_path_uses_md5_prefix_like_connector() -> None:
    path = heartbeat_path("D:/Capstone/newCapstone/disputatio")
    assert path.name.endswith(".json")
    assert path.parent.name == "instances"


def test_wait_until_http_ready_rejects_playing_without_health() -> None:
    result = wait_until_http_ready(
        read_heartbeat=lambda: {
            "state": "playing",
            "port": 8090,
            "pid": 1,
            "listenerRunning": False,
        },
        probe_health=lambda port: False,
        sleep=lambda _seconds: None,
        timeout_seconds=0.01,
        interval_seconds=0.01,
    )
    assert result["ok"] is False
    assert result["reasonCode"] == "http-listener-down"


def test_wait_until_http_ready_accepts_health_on_heartbeat_port() -> None:
    probes: list[int] = []

    def probe_health(port: int) -> bool:
        probes.append(port)
        return port == 8097

    result = wait_until_http_ready(
        read_heartbeat=lambda: {
            "state": "playing",
            "port": 8097,
            "pid": 44620,
            "listenerRunning": True,
        },
        probe_health=probe_health,
        sleep=lambda _seconds: None,
        timeout_seconds=1.0,
        interval_seconds=0.01,
    )
    assert result["ok"] is True
    assert result["port"] == 8097
    assert probes == [8097]


class _FakeHealthResponse:
    def __init__(self, status: int) -> None:
        self.status = status

    def __enter__(self):
        return self

    def __exit__(self, *_args: object) -> bool:
        return False


def test_probe_health_detail_returns_http_200() -> None:
    def opener(_request: object, timeout: float = 2) -> _FakeHealthResponse:
        assert timeout == 2
        return _FakeHealthResponse(200)

    detail = probe_health_detail(8103, opener=opener)
    assert detail["ok"] is True
    assert detail["status_code"] == 200
    assert "8103/health" in str(detail["url"])
    assert probe_health_http(8103, opener=opener) is True


def test_probe_health_detail_records_http_503() -> None:
    def opener(_request: object, timeout: float = 2) -> _FakeHealthResponse:
        raise urllib.error.HTTPError(
            "http://127.0.0.1:8103/health",
            503,
            "unavailable",
            hdrs=None,  # type: ignore[arg-type]
            fp=None,
        )

    detail = probe_health_detail(8103, opener=opener)
    assert detail["ok"] is False
    assert detail["status_code"] == 503


def test_probe_health_detail_records_timeout_without_status() -> None:
    def opener(_request: object, timeout: float = 2) -> _FakeHealthResponse:
        raise urllib.error.URLError("timed out")

    detail = probe_health_detail(8103, opener=opener)
    assert detail["ok"] is False
    assert detail["status_code"] is None
    assert "timeout" in str(detail["error"])


def test_format_health_line_includes_http_status() -> None:
    line = format_health_line(
        {
            "ok": True,
            "status_code": 200,
            "error": "",
            "url": "http://127.0.0.1:8103/health",
        }
    )
    assert "HTTP 200" in line
    assert "/health" in line
    missing = format_health_line(
        {
            "ok": False,
            "status_code": None,
            "error": "timeout",
            "url": "http://127.0.0.1:8103/health",
        }
    )
    assert "HTTP ---" in missing
    assert "timeout" in missing


def test_should_fetch_unity_console_only_when_health_ok() -> None:
    assert should_fetch_unity_console(True) is True
    assert should_fetch_unity_console(False) is False

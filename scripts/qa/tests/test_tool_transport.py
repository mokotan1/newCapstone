"""TransportGuard: first transport failure trips the guard; no CLI call afterwards."""

from __future__ import annotations

from typing import Any

from scripts.qa.tool.transport import (
    TRANSPORT_DOWN_CODE,
    TransportGuard,
    is_transport_failure,
)


class _Inner:
    kind = "live"

    def __init__(self, responses: list[dict[str, Any]]) -> None:
        self.calls: list[tuple[str, str, str]] = []
        self._responses = list(responses)

    def invoke(self, family: str, name: str, target: str) -> dict[str, Any]:
        self.calls.append((family, name, target))
        return dict(self._responses.pop(0))


def _ok(scene: str = "Hall_playerble") -> dict[str, Any]:
    return {"ok": True, "code": "Ok", "data": {"activeScene": scene}}


def test_is_transport_failure_detects_cli_timeout_and_no_json() -> None:
    assert is_transport_failure({"ok": False, "code": "EnvironmentBlocked", "returncode": 124})
    assert is_transport_failure({"ok": False, "code": "EnvironmentBlocked", "returncode": 1})
    assert is_transport_failure({"ok": False, "code": TRANSPORT_DOWN_CODE})


def test_is_transport_failure_ignores_gameplay_failures() -> None:
    assert not is_transport_failure({"ok": False, "code": "AssertionFailed", "data": {}})
    assert not is_transport_failure({"ok": False, "code": "EnvironmentBlocked", "data": {"clicked": "False"}})
    assert not is_transport_failure(_ok())


def test_guard_trips_on_timeout_and_refuses_following_calls() -> None:
    inner = _Inner([_ok(), {"ok": False, "code": "EnvironmentBlocked", "returncode": 124}])
    guard = TransportGuard(inner, health_probe=lambda: True)

    assert guard.invoke("interaction", "invoke", "a")["ok"] is True
    failed = guard.invoke("interaction", "invoke", "b")
    assert failed["code"] == TRANSPORT_DOWN_CODE
    assert guard.tripped is True

    refused = guard.invoke("interaction", "invoke", "c")
    assert refused["code"] == TRANSPORT_DOWN_CODE
    assert refused["refused"] is True
    assert len(inner.calls) == 2


def test_guard_trips_before_call_when_health_is_down() -> None:
    inner = _Inner([_ok()])
    guard = TransportGuard(inner, health_probe=lambda: False)

    result = guard.invoke("interaction", "invoke", "a")
    assert result["code"] == TRANSPORT_DOWN_CODE
    assert guard.tripped is True
    assert inner.calls == []
    assert guard.trip_reason == "health-down"


def test_guard_keeps_kind_and_records_trip_target() -> None:
    inner = _Inner([{"ok": False, "code": "EnvironmentBlocked", "returncode": 124}])
    guard = TransportGuard(inner, health_probe=lambda: True)
    assert guard.kind == "live"
    guard.invoke("interaction", "invoke", "hall.nav.probe")
    assert guard.trip_target == "hall.nav.probe"
    assert guard.trip_reason == "cli-timeout"

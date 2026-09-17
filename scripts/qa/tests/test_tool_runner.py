"""Hall→Kitchen vertical runner (design AC19, AC23)."""

from __future__ import annotations

from pathlib import Path
from typing import Any

from scripts.qa.tool.runner import run_hall_to_kitchen


class StubGateway:
    kind = "stub"

    def invoke(self, family: str, name: str, target: str) -> dict[str, Any]:
        return {"ok": True, "code": "Ok", "family": family, "name": name, "target": target}


class RecordingLiveGateway:
    kind = "live"

    def __init__(self, responses: list[dict[str, Any]] | None = None) -> None:
        self.calls: list[dict[str, str]] = []
        self._responses = list(responses or [])

    def invoke(self, family: str, name: str, target: str) -> dict[str, Any]:
        self.calls.append({"family": family, "name": name, "target": target})
        if self._responses:
            return dict(self._responses.pop(0))
        return {
            "ok": False,
            "code": "AssertionFailed",
            "data": {
                "activeScene": "Hall_playerble",
                "controllerFound": "True",
                "transitionPending": "False",
                "inputGateBlocked": "False",
            },
        }


def _ready_snapshot() -> dict[str, Any]:
    return {
        "editorConnected": True,
        "projectPath": "disputatio",
        "expectedProjectPath": "disputatio",
        "compiling": False,
        "dirtyScene": False,
        "currentLeaseOwner": "qa-tool",
        "requestedOwner": "qa-tool",
        "requiredCapabilityIds": [
            "hall.nav.click-kitchen-entry",
            "hall.nav.execute-front",
            "hall.nav.execute-door",
            "hall.nav.reset-to-hall",
            "hall.nav.assert-route",
        ],
        "liveCapabilityIds": [
            "hall.nav.click-kitchen-entry",
            "hall.nav.execute-front",
            "hall.nav.execute-door",
            "hall.nav.reset-to-hall",
            "hall.nav.assert-route",
        ],
        "editorPid": 111,
        "leaseId": "lease-a",
    }


def test_stub_gateway_cannot_complete_vertical_run(tmp_path: Path) -> None:
    result = run_hall_to_kitchen(
        run_root=tmp_path,
        gateway=StubGateway(),
        snapshot=_ready_snapshot(),
    )
    assert result["runVerdict"] != "PASS"
    assert result["reasonCode"] == "stub-forbidden"
    assert result["featureVerified"] is False
    assert (tmp_path / "report.json").is_file()
    assert (tmp_path / "report.md").is_file()


def test_stale_pid_blocks_before_mutations(tmp_path: Path) -> None:
    gateway = RecordingLiveGateway()
    result = run_hall_to_kitchen(
        run_root=tmp_path,
        gateway=gateway,
        snapshot=_ready_snapshot(),
        previous_connection={"editorPid": 999, "leaseId": "lease-a", "editorConnected": True},
    )
    assert result["reasonCode"] == "stale-pid"
    assert result["runVerdict"] == "BLOCKED"
    assert gateway.calls == []


def test_live_gateway_records_real_invokes_and_cannot_pass_without_kitchen(
    tmp_path: Path,
) -> None:
    gateway = RecordingLiveGateway()
    result = run_hall_to_kitchen(
        run_root=tmp_path,
        gateway=gateway,
        snapshot=_ready_snapshot(),
        previous_connection={"editorPid": 111, "leaseId": "lease-a", "editorConnected": True},
    )
    assert gateway.calls
    assert any(call["target"] == "hall.nav.assert-route" for call in gateway.calls)
    assert result["runVerdict"] in {"FAIL", "BLOCKED"}
    assert result["featureVerified"] is False
    assert result["review"]["status"] == "missing"


def test_live_gateway_runs_three_hops_and_pointer_layer(tmp_path: Path) -> None:
    gateway = RecordingLiveGateway()
    run_hall_to_kitchen(
        run_root=tmp_path,
        gateway=gateway,
        snapshot=_ready_snapshot(),
        previous_connection={"editorPid": 111, "leaseId": "lease-a", "editorConnected": True},
    )
    targets = [call["target"] for call in gateway.calls]
    names = [call["name"] for call in gateway.calls]
    assert "hall.nav.click-kitchen-entry" in targets
    assert "hall.nav.execute-front" in targets
    assert "hall.nav.execute-door" in targets
    assert "hall.nav.reset-to-hall" in targets
    assert "hall.kitchen-entry" in targets
    assert "pointer" in names


def test_missing_independent_review_forbids_feature_verified(tmp_path: Path) -> None:
    kitchen = {
        "ok": True,
        "code": "Ok",
        "data": {
            "activeScene": "Kitchen",
            "controllerFound": "False",
            "transitionPending": "False",
            "inputGateBlocked": "False",
            "assertPassed": "True",
        },
        "scenesVisited": [
            "Hall_playerble",
            "Hall_Left",
            "Hall_Left2",
            "Kitchen",
        ],
        "driver": "api",
        "inputLayer": "api",
    }
    gateway = RecordingLiveGateway(responses=[kitchen] * 20)
    result = run_hall_to_kitchen(
        run_root=tmp_path,
        gateway=gateway,
        snapshot=_ready_snapshot(),
        previous_connection={"editorPid": 111, "leaseId": "lease-a", "editorConnected": True},
    )
    assert result["featureVerified"] is False
    assert result["review"]["status"] == "missing"

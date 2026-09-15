"""Preflight blocks unsafe Editor states without mutations (design AC02–AC04)."""

from __future__ import annotations

from scripts.qa.rooms.preflight import missing_required_capabilities
from scripts.qa.tool.preflight import acquire_lease, evaluate_preflight


def _ready_snapshot(**overrides: object) -> dict[str, object]:
    snapshot: dict[str, object] = {
        "editorConnected": True,
        "projectPath": "D:/Capstone/newCapstone/disputatio",
        "expectedProjectPath": "disputatio",
        "compiling": False,
        "dirtyScene": False,
        "currentLeaseOwner": None,
        "requestedOwner": "qa-run-1",
        "requiredCapabilityIds": [
            "hall.nav.click-kitchen-entry",
            "hall.nav.assert-route",
        ],
        "liveCapabilityIds": [
            "hall.nav.click-kitchen-entry",
            "hall.nav.assert-route",
            "hall.nav.probe",
        ],
    }
    snapshot.update(overrides)
    return snapshot


def test_disconnected_editor_is_blocked_without_mutations() -> None:
    result = evaluate_preflight(_ready_snapshot(editorConnected=False))
    assert result["executionStatus"] == "blocked"
    assert result["reasonCode"] == "editor-disconnected"
    assert result["mutationCalls"] == []


def test_wrong_project_is_blocked_without_mutations() -> None:
    result = evaluate_preflight(
        _ready_snapshot(projectPath="D:/OtherProject")
    )
    assert result["executionStatus"] == "blocked"
    assert result["reasonCode"] == "invalid-project"
    assert result["mutationCalls"] == []


def test_compiling_editor_is_blocked_without_mutations() -> None:
    result = evaluate_preflight(_ready_snapshot(compiling=True))
    assert result["executionStatus"] == "blocked"
    assert result["reasonCode"] == "compiling"
    assert result["mutationCalls"] == []


def test_dirty_scene_is_blocked_without_mutations() -> None:
    result = evaluate_preflight(_ready_snapshot(dirtyScene=True))
    assert result["executionStatus"] == "blocked"
    assert result["reasonCode"] == "dirty-scene"
    assert result["mutationCalls"] == []


def test_foreign_lease_is_blocked_without_mutations() -> None:
    result = evaluate_preflight(
        _ready_snapshot(currentLeaseOwner="qa-playtester", requestedOwner="qa-run-2")
    )
    assert result["executionStatus"] == "blocked"
    assert result["reasonCode"] == "ownership"
    assert result["mutationCalls"] == []


def test_missing_capabilities_block_without_silent_api_fallback() -> None:
    required = ["hall.nav.click-kitchen-entry", "hall.nav.event-system.click"]
    live = ["hall.nav.click-kitchen-entry"]
    result = evaluate_preflight(
        _ready_snapshot(requiredCapabilityIds=required, liveCapabilityIds=live)
    )
    assert result["executionStatus"] == "blocked"
    assert result["reasonCode"] == "missing-capability"
    assert result["missingCapabilities"] == ["hall.nav.event-system.click"]
    assert result["substitutedInputLayer"] is None
    assert missing_required_capabilities(required, live) == [
        "hall.nav.event-system.click"
    ]
    assert result["mutationCalls"] == []


def test_second_owner_is_rejected_while_first_heartbeat_holds() -> None:
    store: dict[str, object] = {}
    first = acquire_lease(store, owner="qa-run-1", heartbeat_at="2026-09-15T06:00:00Z")
    second = acquire_lease(store, owner="qa-run-2", heartbeat_at="2026-09-15T06:00:01Z")
    again = acquire_lease(store, owner="qa-run-1", heartbeat_at="2026-09-15T06:00:02Z")
    assert first["executionStatus"] == "acquired"
    assert first["owner"] == "qa-run-1"
    assert second["executionStatus"] == "blocked"
    assert second["reasonCode"] == "ownership"
    assert again["executionStatus"] == "acquired"
    assert store["owner"] == "qa-run-1"
    assert store["heartbeatAt"] == "2026-09-15T06:00:02Z"

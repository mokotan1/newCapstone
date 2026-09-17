"""Coordinator cancel, lost mutation, cleanup, and crash recovery (design AC12–AC15)."""

from __future__ import annotations

from pathlib import Path
from typing import Any

from scripts.qa.tool.coordinator import Coordinator


class RecordingGateway:
    """In-memory Gateway double. Records calls; never talks to Unity."""

    def __init__(
        self,
        *,
        drop_mutations: bool = False,
        supports_query: bool = True,
        cleanup_ok: bool = True,
        cleanup_uncertain: bool = False,
        profile_switch_partial: bool = False,
    ) -> None:
        self.calls: list[dict[str, str]] = []
        self.drop_mutations = drop_mutations
        self.supports_query = supports_query
        self.cleanup_ok = cleanup_ok
        self.cleanup_uncertain = cleanup_uncertain
        self.profile_switch_partial = profile_switch_partial
        self.released_resources: list[str] = []

    def mutate(self, command_id: str, name: str) -> dict[str, Any] | None:
        self.calls.append({"kind": "mutation", "name": name, "commandId": command_id})
        if self.drop_mutations:
            return None
        return {"ok": True, "commandId": command_id}

    def query_command(self, command_id: str) -> dict[str, Any] | None:
        self.calls.append({"kind": "query", "name": command_id, "commandId": command_id})
        if not self.supports_query:
            return None
        return {"found": True, "commandId": command_id}

    def switch_profile(self, profile_id: str) -> dict[str, Any]:
        self.calls.append({"kind": "profile-switch", "name": profile_id})
        if self.profile_switch_partial:
            return {"ok": False, "acquired": [profile_id]}
        return {"ok": True, "acquired": [profile_id]}

    def release_profile(self, profile_id: str) -> None:
        self.calls.append({"kind": "profile-release", "name": profile_id})
        self.released_resources.append(profile_id)

    def cleanup(self) -> dict[str, Any]:
        self.calls.append({"kind": "cleanup", "name": "cleanup"})
        if self.cleanup_uncertain:
            return {"ok": False, "uncertain": True}
        if not self.cleanup_ok:
            return {"ok": False, "uncertain": False}
        return {"ok": True, "uncertain": False}

    def restart_editor(self) -> None:
        self.calls.append({"kind": "editor-restart", "name": "restart"})


def _connected_snapshot() -> dict[str, object]:
    return {
        "editorConnected": True,
        "projectPath": "D:/Capstone/newCapstone/disputatio",
        "expectedProjectPath": "disputatio",
        "compiling": False,
        "dirtyScene": False,
        "currentLeaseOwner": None,
        "requestedOwner": "qa-run-1",
        "requiredCapabilityIds": ["hall.nav.click-kitchen-entry"],
        "liveCapabilityIds": ["hall.nav.click-kitchen-entry"],
    }


def _kinds(gateway: RecordingGateway, kind: str) -> list[str]:
    return [item["name"] for item in gateway.calls if item["kind"] == kind]


def test_cancel_stops_new_gameplay_and_forbids_pass(tmp_path: Path) -> None:
    gateway = RecordingGateway()
    coordinator = Coordinator(run_root=tmp_path, gateway=gateway)
    coordinator.start_run(_connected_snapshot())
    coordinator.enter_running()
    first = coordinator.dispatch_gameplay("click-kitchen")
    cancelled = coordinator.cancel(cancelled_at="2026-09-15T06:10:00Z")
    second = coordinator.dispatch_gameplay("click-kitchen-again")

    assert first["executionStatus"] == "succeeded"
    assert cancelled["executionStatus"] == "cancelled"
    assert cancelled["cancelledAt"] == "2026-09-15T06:10:00Z"
    assert cancelled["lastCommand"] == "click-kitchen"
    assert cancelled["scenarioVerdict"] != "PASS"
    assert "cancelled" in cancelled["reasonCodes"]
    assert "cleanup" in _kinds(gateway, "cleanup")
    assert second["executionStatus"] == "blocked"
    assert _kinds(gateway, "mutation") == ["click-kitchen"]


def test_lost_mutation_is_queried_not_resent(tmp_path: Path) -> None:
    gateway = RecordingGateway(drop_mutations=True, supports_query=True)
    coordinator = Coordinator(run_root=tmp_path, gateway=gateway)
    coordinator.start_run(_connected_snapshot())
    coordinator.enter_running()
    result = coordinator.dispatch_gameplay("click-kitchen")

    assert result["executionStatus"] == "timed-out"
    assert _kinds(gateway, "mutation") == ["click-kitchen"]
    assert len(_kinds(gateway, "query")) == 1


def test_lost_mutation_without_query_is_recovery_required(tmp_path: Path) -> None:
    gateway = RecordingGateway(drop_mutations=True, supports_query=False)
    coordinator = Coordinator(run_root=tmp_path, gateway=gateway)
    coordinator.start_run(_connected_snapshot())
    coordinator.enter_running()
    result = coordinator.dispatch_gameplay("click-kitchen")

    assert result["executionStatus"] == "recovery-required"
    assert _kinds(gateway, "mutation") == ["click-kitchen"]
    assert coordinator.state == "recovery-required"


def test_failures_always_attempt_cleanup_and_uncertain_cleanup_blocks_next(
    tmp_path: Path,
) -> None:
    run_fail = RecordingGateway()
    run_coord = Coordinator(run_root=tmp_path / "run-fail", gateway=run_fail)
    run_coord.start_run(_connected_snapshot())
    run_coord.enter_running()
    failed = run_coord.fail_run("gameplay-failed")
    assert "cleanup" in _kinds(run_fail, "cleanup")
    assert failed["cleanupStatus"] == "restored"

    profile = RecordingGateway(profile_switch_partial=True)
    profile_coord = Coordinator(run_root=tmp_path / "profile", gateway=profile)
    profile_coord.start_run(_connected_snapshot())
    switched = profile_coord.acquire_profile("qa-isolated")
    assert switched["executionStatus"] == "blocked"
    assert profile.released_resources == ["qa-isolated"]
    assert "cleanup" in _kinds(profile, "cleanup")

    dirty = RecordingGateway(cleanup_uncertain=True)
    dirty_root = tmp_path / "cleanup-fail"
    dirty_coord = Coordinator(run_root=dirty_root, gateway=dirty)
    dirty_coord.start_run(_connected_snapshot())
    dirty_coord.enter_running()
    uncertain = dirty_coord.fail_run("gameplay-failed")
    assert uncertain["cleanupStatus"] == "uncertain"
    assert dirty_coord.state == "recovery-required"

    next_coord = Coordinator(run_root=dirty_root, gateway=RecordingGateway())
    blocked = next_coord.start_run(_connected_snapshot())
    assert blocked["executionStatus"] == "blocked"
    assert blocked["reasonCode"] == "recovery-required"


def test_incomplete_journal_blocks_new_run_until_recover_without_editor_restart(
    tmp_path: Path,
) -> None:
    gateway = RecordingGateway()
    first = Coordinator(run_root=tmp_path, gateway=gateway)
    first.start_run(_connected_snapshot())
    first.enter_running()
    first.dispatch_gameplay("click-kitchen")
    assert first.state == "running"

    restarted_gateway = RecordingGateway()
    second = Coordinator(run_root=tmp_path, gateway=restarted_gateway)
    blocked = second.start_run(_connected_snapshot())
    assert blocked["executionStatus"] == "blocked"
    assert blocked["reasonCode"] == "recovery-required"
    assert "restart" not in _kinds(restarted_gateway, "editor-restart")

    recovered = second.recover()
    assert recovered["executionStatus"] == "recovered"
    assert "restart" not in _kinds(restarted_gateway, "editor-restart")
    allowed = second.start_run(_connected_snapshot())
    assert allowed["executionStatus"] == "ready"


def test_uncertain_recover_does_not_mark_journal_complete(tmp_path: Path) -> None:
    gateway = RecordingGateway(cleanup_uncertain=True)
    coordinator = Coordinator(run_root=tmp_path, gateway=gateway)
    coordinator.start_run(_connected_snapshot())
    coordinator.enter_running()
    recovered = coordinator.recover()

    assert recovered["executionStatus"] == "recovery-failed"
    assert recovered["cleanupStatus"] == "uncertain"
    assert coordinator.state == "recovery-required"
    blocked = Coordinator(run_root=tmp_path, gateway=RecordingGateway()).start_run(
        _connected_snapshot()
    )
    assert blocked["reasonCode"] == "recovery-required"


def test_finish_run_cleanup_failure_is_not_complete(tmp_path: Path) -> None:
    gateway = RecordingGateway(cleanup_ok=False)
    coordinator = Coordinator(run_root=tmp_path, gateway=gateway)
    coordinator.start_run(_connected_snapshot())
    coordinator.enter_running()
    finished = coordinator.finish_run()
    assert finished["executionStatus"] == "recovery-failed"
    assert finished["cleanupStatus"] == "failed"
    assert coordinator.state == "recovery-required"


def test_preflight_block_does_not_call_cleanup(tmp_path: Path) -> None:
    gateway = RecordingGateway()
    coordinator = Coordinator(run_root=tmp_path, gateway=gateway)
    snapshot = _connected_snapshot()
    snapshot["editorConnected"] = False
    result = coordinator.start_run(snapshot)
    assert result["executionStatus"] == "blocked"
    assert result["cleanupStatus"] == "not-applicable"
    assert _kinds(gateway, "cleanup") == []
    assert _kinds(gateway, "mutation") == []

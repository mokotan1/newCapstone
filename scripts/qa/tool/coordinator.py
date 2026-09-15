"""QA run coordinator: cancel, mutation timeout, cleanup, crash recovery (AC12–AC15)."""

from __future__ import annotations

import json
import uuid
from collections.abc import Mapping
from pathlib import Path
from typing import Any, Protocol

from scripts.qa.tool.preflight import evaluate_preflight
from scripts.qa.tool.verdict import judge_scenario


class QaGateway(Protocol):
    def mutate(self, command_id: str, name: str) -> dict[str, Any] | None:
        ...

    def query_command(self, command_id: str) -> dict[str, Any] | None:
        ...

    def switch_profile(self, profile_id: str) -> dict[str, Any]:
        ...

    def release_profile(self, profile_id: str) -> None:
        ...

    def cleanup(self) -> dict[str, Any]:
        ...


class Coordinator:
    """Persist an append-only journal so a crash cannot be assumed to have finalized."""

    def __init__(self, *, run_root: Path, gateway: QaGateway) -> None:
        self._run_root = run_root
        self._gateway = gateway
        self._journal_path = run_root / "journal.json"
        self._run_root.mkdir(parents=True, exist_ok=True)
        self._journal: dict[str, Any] = self._load_journal()
        self.state: str = str(self._journal.get("state") or "planned")
        self._cancelled = bool(self._journal.get("cancelled"))
        self._last_command: str | None = self._journal.get("lastCommand")

    def _load_journal(self) -> dict[str, Any]:
        if not self._journal_path.is_file():
            return {
                "complete": True,
                "state": "planned",
                "commands": [],
                "cancelled": False,
            }
        payload = json.loads(self._journal_path.read_text(encoding="utf-8"))
        if not isinstance(payload, dict):
            return {"complete": False, "state": "recovery-required", "commands": []}
        return payload

    def _save_journal(self) -> None:
        self._journal["state"] = self.state
        self._journal["cancelled"] = self._cancelled
        self._journal["lastCommand"] = self._last_command
        self._journal_path.write_text(
            json.dumps(self._journal, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )

    def _incomplete(self) -> bool:
        return self._journal.get("complete") is False

    def start_run(self, snapshot: Mapping[str, Any]) -> dict[str, Any]:
        if self._incomplete():
            return {
                "executionStatus": "blocked",
                "reasonCode": "recovery-required",
                "verificationStatus": "blocked",
            }

        preflight = evaluate_preflight(snapshot)
        if preflight.get("executionStatus") == "blocked":
            preflight["cleanupStatus"] = "not-applicable"
            self.state = "planned"
            return preflight

        self.state = "acquiring"
        self._cancelled = False
        self._last_command = None
        self._journal = {
            "complete": False,
            "state": self.state,
            "commands": [],
            "cancelled": False,
            "lastCommand": None,
        }
        self._save_journal()
        return {
            "executionStatus": "ready",
            "reasonCode": "ok",
            "verificationStatus": "not-applicable",
            "cleanupStatus": "not-applicable",
        }

    def enter_running(self) -> None:
        self.state = "running"
        self._save_journal()

    def dispatch_gameplay(self, name: str) -> dict[str, Any]:
        if self._cancelled or self.state != "running":
            return {
                "executionStatus": "blocked",
                "reasonCode": "cancelled" if self._cancelled else "not-running",
            }

        command_id = uuid.uuid4().hex
        commands = list(self._journal.get("commands") or [])
        commands.append({"commandId": command_id, "name": name, "response": None})
        self._journal["commands"] = commands
        self._last_command = name
        self._save_journal()

        response = self._gateway.mutate(command_id, name)
        if response is None:
            queried = self._gateway.query_command(command_id)
            if queried is None:
                self.state = "recovery-required"
                self._journal["complete"] = False
                self._save_journal()
                return {
                    "executionStatus": "recovery-required",
                    "commandId": command_id,
                }
            return {
                "executionStatus": "timed-out",
                "commandId": command_id,
            }

        commands[-1]["response"] = dict(response)
        self._save_journal()
        return {
            "executionStatus": "succeeded",
            "commandId": command_id,
        }

    def cancel(self, *, cancelled_at: str) -> dict[str, Any]:
        self._cancelled = True
        cleanup_status = self._run_cleanup()
        judgment = judge_scenario(
            {
                "scenarioId": "qa.tool.hall-to-kitchen",
                "started": True,
                "cancelled": True,
                "requiredStepIds": ["navigate"],
                "executedStepIds": [self._last_command] if self._last_command else [],
                "cleanupStatus": cleanup_status,
            }
        )
        self.state = "finalized"
        self._journal["complete"] = cleanup_status != "uncertain"
        self._save_journal()
        return {
            "executionStatus": "cancelled",
            "cancelledAt": cancelled_at,
            "lastCommand": self._last_command,
            "cleanupStatus": cleanup_status,
            "scenarioVerdict": judgment["scenarioVerdict"],
            "reasonCodes": judgment["reasonCodes"],
        }

    def fail_run(self, reason: str) -> dict[str, Any]:
        cleanup_status = self._run_cleanup()
        if cleanup_status == "uncertain":
            self.state = "recovery-required"
            self._journal["complete"] = False
        else:
            self.state = "finalized"
            self._journal["complete"] = True
        self._save_journal()
        return {
            "executionStatus": "failed",
            "reasonCode": reason,
            "cleanupStatus": cleanup_status,
        }

    def acquire_profile(self, profile_id: str) -> dict[str, Any]:
        switched = self._gateway.switch_profile(profile_id)
        acquired = [str(item) for item in (switched.get("acquired") or [])]
        if switched.get("ok"):
            return {"executionStatus": "acquired", "profileId": profile_id}

        for resource in acquired:
            self._gateway.release_profile(resource)
        cleanup_status = self._run_cleanup()
        self.state = "finalized" if cleanup_status != "uncertain" else "recovery-required"
        self._journal["complete"] = cleanup_status != "uncertain"
        self._save_journal()
        return {
            "executionStatus": "blocked",
            "reasonCode": "profile-switch-partial",
            "cleanupStatus": cleanup_status,
        }

    def recover(self) -> dict[str, Any]:
        cleanup_status = self._run_cleanup()
        self.state = "recovered"
        self._cancelled = False
        self._journal["complete"] = True
        self._save_journal()
        return {
            "executionStatus": "recovered",
            "cleanupStatus": cleanup_status,
        }

    def _run_cleanup(self) -> str:
        self.state = "restoring"
        result = self._gateway.cleanup()
        if result.get("uncertain"):
            return "uncertain"
        if result.get("ok"):
            return "restored"
        return "failed"

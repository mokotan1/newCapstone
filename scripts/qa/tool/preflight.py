"""Preflight evaluation for the QA tool (design AC02–AC04).

Reads a status snapshot only. Never issues Unity mutations (save, scene load, Play).
"""

from __future__ import annotations

from collections.abc import Mapping, MutableMapping
from typing import Any

from scripts.qa.rooms.preflight import missing_required_capabilities
from scripts.unity_harness.result_contract import (
    refuse_if_invalid_project,
    refuse_if_lease_conflict,
)


def _blocked(reason_code: str, **extra: Any) -> dict[str, Any]:
    payload: dict[str, Any] = {
        "executionStatus": "blocked",
        "verificationStatus": "blocked",
        "reasonCode": reason_code,
        "mutationCalls": [],
        "substitutedInputLayer": None,
        "missingCapabilities": extra.get("missingCapabilities", []),
    }
    payload.update(extra)
    return payload


def _ready() -> dict[str, Any]:
    return {
        "executionStatus": "ready",
        "verificationStatus": "not-applicable",
        "reasonCode": "ok",
        "mutationCalls": [],
        "substitutedInputLayer": None,
        "missingCapabilities": [],
    }


def evaluate_preflight(snapshot: Mapping[str, Any]) -> dict[str, Any]:
    """Classify Editor/project/lease/capability blockers. mutationCalls stays empty."""
    if not snapshot.get("editorConnected"):
        return _blocked("editor-disconnected")

    project = refuse_if_invalid_project(
        requested_path=str(snapshot.get("projectPath") or ""),
        expected_path=str(snapshot.get("expectedProjectPath") or "disputatio"),
    )
    if project.get("executionStatus") == "blocked":
        return _blocked("invalid-project")

    if snapshot.get("compiling"):
        return _blocked("compiling")
    if snapshot.get("dirtyScene"):
        return _blocked("dirty-scene")

    lease = refuse_if_lease_conflict(
        current_owner=snapshot.get("currentLeaseOwner"),
        requested_owner=snapshot.get("requestedOwner"),
    )
    if lease.get("errorCategory") == "ownership":
        return _blocked("ownership")

    required = list(snapshot.get("requiredCapabilityIds") or [])
    live = list(snapshot.get("liveCapabilityIds") or [])
    missing = missing_required_capabilities(required, live)
    if missing:
        return _blocked("missing-capability", missingCapabilities=missing)

    return _ready()


def acquire_lease(
    store: MutableMapping[str, Any],
    *,
    owner: str,
    heartbeat_at: str,
) -> dict[str, Any]:
    """Grant a single in-memory lease. A second owner is blocked; the current owner may heartbeat."""
    current = store.get("owner")
    if current and current != owner:
        return {
            "executionStatus": "blocked",
            "reasonCode": "ownership",
            "owner": current,
        }
    store["owner"] = owner
    store["heartbeatAt"] = heartbeat_at
    return {
        "executionStatus": "acquired",
        "reasonCode": "ok",
        "owner": owner,
        "heartbeatAt": heartbeat_at,
    }

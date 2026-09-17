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
    """preflight 차단 결과를 만든다. mutationCalls는 항상 빈 목록이다."""
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
    """Editor/lease/capability가 준비됐을 때의 preflight 성공 스냅샷이다."""
    return {
        "executionStatus": "ready",
        "verificationStatus": "not-applicable",
        "reasonCode": "ok",
        "mutationCalls": [],
        "substitutedInputLayer": None,
        "missingCapabilities": [],
    }


def evaluate_preflight(snapshot: Mapping[str, Any]) -> dict[str, Any]:
    """Editor 연결·프로젝트·컴파일·dirty·lease·capability 누락을 실행 전에 분류한다."""
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
    """인메모리 lease를 한 owner만 갖게 한다. 다른 owner는 ownership으로 막는다."""
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


def evaluate_injected_preflight_matrix(baseline: Mapping[str, Any]) -> dict[str, Any]:
    """라이브 스냅샷을 복사해 AC02–AC04 차단 사유를 각각 주입한다. mutation은 0회다."""
    base = dict(baseline)
    lease_store: dict[str, Any] = {}
    first = acquire_lease(lease_store, owner="qa-tool-owner-a", heartbeat_at="live")
    second = acquire_lease(lease_store, owner="qa-tool-owner-b", heartbeat_at="live+1")
    return {
        "disconnected": evaluate_preflight({**base, "editorConnected": False}),
        "invalid-project": evaluate_preflight({**base, "projectPath": "other-project"}),
        "compiling": evaluate_preflight({**base, "compiling": True}),
        "dirty-scene": evaluate_preflight({**base, "dirtyScene": True}),
        "ownership": evaluate_preflight(
            {**base, "currentLeaseOwner": "qa-playtester", "requestedOwner": "qa-tool"}
        ),
        "missing-capability": evaluate_preflight({**base, "liveCapabilityIds": []}),
        "lease-first": first,
        "lease-second": second,
    }

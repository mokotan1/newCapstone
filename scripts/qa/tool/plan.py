"""Freeze and validate QA execution plans (design AC01)."""

from __future__ import annotations

import hashlib
import json
from collections.abc import Mapping
from copy import deepcopy
from typing import Any

from scripts.qa.tool.errors import PlanError
from scripts.qa.tool.hall_route import WIRED_HALL_TO_KITCHEN, expected_scenes

SCHEMA_VERSION = 1

DEFAULT_TIMEOUTS: dict[str, int] = {
    "preflightSeconds": 60,
    "stepSeconds": 30,
    "scenarioSeconds": 300,
    "cleanupSeconds": 60,
}

_REQUIRED_KEYS: tuple[str, ...] = (
    "planId",
    "schemaVersion",
    "requirementIds",
    "scenarioIds",
    "scenarioHashes",
    "target",
    "requiredChecks",
    "timeouts",
    "exclusions",
    "steps",
)

_REQUIRED_TARGET_KEYS: tuple[str, ...] = (
    "startScene",
    "destinationScene",
    "expectedScenes",
    "interactionId",
    "targetId",
)

_HALL_SCENARIO_ID = "qa.tool.hall-to-kitchen"


def _canonicalize(value: Any) -> Any:
    """planHash용으로 키를 정렬하고 mapping list를 안정 정렬한다."""
    if isinstance(value, Mapping):
        return {key: _canonicalize(value[key]) for key in sorted(value)}
    if isinstance(value, list):
        items = [_canonicalize(item) for item in value]
        if items and all(isinstance(item, dict) for item in items):
            return sorted(
                items,
                key=lambda item: json.dumps(item, sort_keys=True, separators=(",", ":")),
            )
        return items
    return value


def canonical_json_hash(payload: Mapping[str, Any] | str | bytes) -> str:
    """정규 JSON의 SHA-256 hex digest를 반환한다. 키 정렬·매핑 리스트 정렬을 포함한다."""
    if isinstance(payload, bytes):
        canonical = payload
    elif isinstance(payload, str):
        canonical = payload.encode("utf-8")
    else:
        canonical = json.dumps(
            _canonicalize(dict(payload)),
            ensure_ascii=False,
            separators=(",", ":"),
        ).encode("utf-8")
    return hashlib.sha256(canonical).hexdigest()


def _require_non_empty_string(value: Any, label: str) -> str:
    """빈 문자열이 아닌 필수 문자열을 꺼낸다. 아니면 PlanError."""
    if not isinstance(value, str) or not value.strip():
        raise PlanError(f"{label} must be a non-empty string")
    return value


def _require_unique_ids(values: Any, label: str) -> list[str]:
    """ID 목록에 빈 값·중복이 없는지 검사한다."""
    if not isinstance(values, list) or not values:
        raise PlanError(f"{label} must be a non-empty list")
    ids: list[str] = []
    seen: set[str] = set()
    for item in values:
        item_id = _require_non_empty_string(item, label)
        if item_id in seen:
            raise PlanError(f"duplicate {label}: {item_id}")
        seen.add(item_id)
        ids.append(item_id)
    return ids


def build_plan(payload: Mapping[str, Any]) -> dict[str, Any]:
    """필수 필드·중복 ID·timeout을 검사해 실행 계획을 고정한다."""
    if not isinstance(payload, Mapping):
        raise PlanError("plan must be a JSON object")

    missing = [key for key in _REQUIRED_KEYS if key not in payload]
    if missing:
        raise PlanError(f"missing required keys: {', '.join(missing)}")

    plan = deepcopy(dict(payload))
    _require_non_empty_string(plan["planId"], "planId")
    if plan.get("schemaVersion") != SCHEMA_VERSION:
        raise PlanError("schemaVersion must be 1")

    requirement_ids = _require_unique_ids(plan["requirementIds"], "requirementIds")
    scenario_ids = _require_unique_ids(plan["scenarioIds"], "scenarioIds")
    plan["requirementIds"] = requirement_ids
    plan["scenarioIds"] = scenario_ids

    hashes = plan["scenarioHashes"]
    if not isinstance(hashes, Mapping) or not hashes:
        raise PlanError("scenarioHashes must be a non-empty object")
    for scenario_id in scenario_ids:
        digest = hashes.get(scenario_id)
        if not isinstance(digest, str) or not digest.strip():
            raise PlanError(f"scenarioHashes missing digest for {scenario_id}")

    target = plan["target"]
    if not isinstance(target, Mapping):
        raise PlanError("target must be an object")
    for key in _REQUIRED_TARGET_KEYS:
        if key == "expectedScenes":
            scenes = target.get(key)
            if not isinstance(scenes, list) or not scenes:
                raise PlanError("target.expectedScenes must be a non-empty list")
            for scene in scenes:
                _require_non_empty_string(scene, "target.expectedScenes")
        else:
            _require_non_empty_string(target.get(key), f"target.{key}")

    checks = plan["requiredChecks"]
    if not isinstance(checks, list) or not checks:
        raise PlanError("requiredChecks must be a non-empty list")
    for index, check in enumerate(checks):
        if not isinstance(check, Mapping):
            raise PlanError(f"requiredChecks[{index}] must be an object")
        _require_non_empty_string(check.get("inputLayer"), "requiredChecks.inputLayer")

    timeouts = plan["timeouts"]
    if not isinstance(timeouts, Mapping):
        raise PlanError("timeouts must be an object")
    for key, default in DEFAULT_TIMEOUTS.items():
        value = timeouts.get(key, default)
        if not isinstance(value, int) or value <= 0:
            raise PlanError(f"timeouts.{key} must be a positive integer")

    exclusions = plan["exclusions"]
    if not isinstance(exclusions, list):
        raise PlanError("exclusions must be a list")
    exclusion_ids: set[str] = set()
    for item in exclusions:
        if not isinstance(item, Mapping):
            raise PlanError("exclusions items must be objects")
        exclusion_id = _require_non_empty_string(item.get("id"), "exclusions.id")
        if exclusion_id in exclusion_ids:
            raise PlanError(f"duplicate exclusions.id: {exclusion_id}")
        exclusion_ids.add(exclusion_id)

    steps = plan["steps"]
    if not isinstance(steps, list) or not steps:
        raise PlanError("steps must be a non-empty list")
    step_ids: list[str] = []
    seen_steps: set[str] = set()
    for index, step in enumerate(steps):
        if not isinstance(step, Mapping):
            raise PlanError(f"steps[{index}] must be an object")
        step_id = _require_non_empty_string(step.get("stepId"), "steps.stepId")
        if step_id in seen_steps:
            raise PlanError(f"duplicate stepId: {step_id}")
        seen_steps.add(step_id)
        step_ids.append(step_id)

    hashed = {key: value for key, value in plan.items() if key != "planHash"}
    plan["planHash"] = canonical_json_hash(hashed)
    return plan


def _hall_to_kitchen_scenario() -> dict[str, Any]:
    """1단계 홀→주방 시나리오 본문이다. hop은 씬 YAML 고정값을 그대로 쓴다."""
    hops = list(WIRED_HALL_TO_KITCHEN["hops"])
    return {
        "id": _HALL_SCENARIO_ID,
        "requirementId": "REQ-QA-HALL-KITCHEN-NAV",
        "startScene": "Hall_playerble",
        "destinationScene": "Kitchen",
        "interactionId": "left",
        "targetId": "hall.kitchen-entry",
        "expectedScenes": expected_scenes(),
        "hops": hops,
        "inputLayers": ["api", "event-system"],
        "steps": [
            {"stepId": "preflight", "action": "preflight", "timeoutSeconds": 60},
            {"stepId": "acquire-lease", "action": "acquire", "timeoutSeconds": 30},
            {"stepId": "reset-initial", "action": "reset", "timeoutSeconds": 30},
            {
                "stepId": "navigate-api",
                "action": "interact",
                "inputLayer": "api",
                "timeoutSeconds": 30,
            },
            {
                "stepId": "assert-kitchen-api",
                "action": "assert-scene",
                "expected": "Kitchen",
                "timeoutSeconds": 30,
            },
            {
                "stepId": "reset-between-layers",
                "action": "reset",
                "timeoutSeconds": 30,
            },
            {
                "stepId": "navigate-event-system",
                "action": "interact",
                "inputLayer": "event-system",
                "timeoutSeconds": 30,
            },
            {
                "stepId": "assert-kitchen-event-system",
                "action": "assert-scene",
                "expected": "Kitchen",
                "timeoutSeconds": 30,
            },
            {
                "stepId": "capture",
                "action": "capture",
                "requiredArtifactKinds": ["screenshot"],
                "timeoutSeconds": 30,
            },
            {"stepId": "cleanup", "action": "cleanup", "timeoutSeconds": 60},
        ],
    }


def build_hall_to_kitchen_plan() -> dict[str, Any]:
    """1단계 Hall→Kitchen 실행 계획을 반환한다. 직접 Kitchen hop은 넣지 않는다."""
    scenario = _hall_to_kitchen_scenario()
    first_hop = WIRED_HALL_TO_KITCHEN["hops"][0]
    payload: dict[str, Any] = {
        "planId": "hall-to-kitchen.v1",
        "schemaVersion": SCHEMA_VERSION,
        "requirementIds": ["REQ-QA-HALL-KITCHEN-NAV"],
        "scenarioIds": [_HALL_SCENARIO_ID],
        "scenarioHashes": {_HALL_SCENARIO_ID: canonical_json_hash(scenario)},
        "target": {
            "startScene": "Hall_playerble",
            "destinationScene": "Kitchen",
            "expectedScenes": expected_scenes(),
            "hops": list(WIRED_HALL_TO_KITCHEN["hops"]),
            "interactionId": first_hop["interactionId"],
            "targetId": first_hop["targetId"],
        },
        "requiredChecks": [
            {"inputLayer": "api"},
            {"inputLayer": "event-system"},
        ],
        "timeouts": dict(DEFAULT_TIMEOUTS),
        "exclusions": [
            {
                "id": "player-input",
                "reason": "phase-2-player-input",
            }
        ],
        "steps": list(scenario["steps"]),
    }
    return build_plan(payload)

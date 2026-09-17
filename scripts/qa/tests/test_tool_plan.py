"""Plan builder contract (design AC01)."""

from __future__ import annotations

import pytest

from scripts.qa.tool.errors import PlanError
from scripts.qa.tool.plan import (
    DEFAULT_TIMEOUTS,
    SCHEMA_VERSION,
    build_hall_to_kitchen_plan,
    build_plan,
    canonical_json_hash,
)


def test_valid_hall_to_kitchen_plan_fixes_required_fields() -> None:
    plan = build_hall_to_kitchen_plan()

    assert plan["planId"] == "hall-to-kitchen.v1"
    assert plan["schemaVersion"] == SCHEMA_VERSION
    assert plan["requirementIds"] == ["REQ-QA-HALL-KITCHEN-NAV"]
    assert plan["scenarioIds"] == ["qa.tool.hall-to-kitchen"]
    assert plan["target"]["startScene"] == "Hall_playerble"
    assert plan["target"]["destinationScene"] == "Kitchen"
    assert plan["target"]["expectedScenes"] == [
        "Hall_playerble",
        "Hall_Left",
        "Hall_Left2",
        "Kitchen",
    ]
    assert plan["target"]["hops"][0]["interactionId"] == "left"
    assert plan["target"]["hops"][0]["nextScene"] == "Hall_Left"
    assert plan["target"]["hops"][1]["fungusBlock"] == "Front_clicked"
    assert plan["target"]["hops"][1]["nextScene"] == "Hall_Left2"
    assert plan["target"]["hops"][2]["fungusBlock"] == "Door_Clicked"
    assert plan["target"]["hops"][2]["nextScene"] == "Kitchen"
    assert plan["target"]["targetId"] == "hall.kitchen-entry"
    assert {check["inputLayer"] for check in plan["requiredChecks"]} == {
        "api",
        "event-system",
    }
    assert plan["timeouts"] == DEFAULT_TIMEOUTS
    assert DEFAULT_TIMEOUTS == {
        "preflightSeconds": 60,
        "stepSeconds": 30,
        "scenarioSeconds": 300,
        "cleanupSeconds": 60,
    }
    assert "qa.tool.hall-to-kitchen" in plan["scenarioHashes"]
    assert plan["scenarioHashes"]["qa.tool.hall-to-kitchen"]
    assert plan["planHash"]
    step_ids = [step["stepId"] for step in plan["steps"]]
    assert len(step_ids) == len(set(step_ids))
    assert "player-input" in {item["id"] for item in plan["exclusions"]}


def test_build_plan_rejects_empty_required_fields() -> None:
    plan = build_hall_to_kitchen_plan()
    plan["requirementIds"] = []
    with pytest.raises(PlanError, match="requirementIds"):
        build_plan(plan)


def test_build_plan_rejects_duplicate_ids() -> None:
    plan = build_hall_to_kitchen_plan()
    plan["steps"].append(dict(plan["steps"][0]))
    with pytest.raises(PlanError, match="duplicate"):
        build_plan(plan)


def test_scenario_hash_is_canonical_and_stable() -> None:
    payload = {"id": "qa.tool.hall-to-kitchen", "steps": [{"stepId": "a"}, {"stepId": "b"}]}
    first = canonical_json_hash(payload)
    second = canonical_json_hash({"steps": [{"stepId": "b"}, {"stepId": "a"}], "id": "qa.tool.hall-to-kitchen"})
    assert first == second
    assert len(first) == 64

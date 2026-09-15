"""Hall→Kitchen wired route vs plan and dual-layer attempts (design AC06–AC08)."""

from __future__ import annotations

from scripts.qa.tool.hall_route import (
    WIRED_HALL_TO_KITCHEN,
    compare_plan_to_wiring,
    judge_hall_attempt,
    record_dual_layer_attempts,
)
from scripts.qa.tool.plan import build_hall_to_kitchen_plan


def test_wired_route_includes_left_corridor_hops() -> None:
    scenes = [hop["scene"] for hop in WIRED_HALL_TO_KITCHEN["hops"]] + [
        WIRED_HALL_TO_KITCHEN["destination"]
    ]
    assert scenes == ["Hall_playerble", "Hall_Left", "Hall_Left2", "Kitchen"]
    assert WIRED_HALL_TO_KITCHEN["hops"][0]["interactionId"] == "left"
    assert WIRED_HALL_TO_KITCHEN["hops"][1]["fungusBlock"] == "Front_clicked"
    assert WIRED_HALL_TO_KITCHEN["hops"][2]["fungusBlock"] == "Door_Clicked"


def test_plan_that_skips_intermediate_scenes_is_spec_mismatch() -> None:
    plan = build_hall_to_kitchen_plan()
    plan["target"]["expectedScenes"] = ["Hall_playerble", "Kitchen"]
    plan["target"]["hops"] = [
        {
            "scene": "Hall_playerble",
            "interactionId": "left",
            "nextScene": "Kitchen",
        }
    ]
    result = compare_plan_to_wiring(plan)
    assert result["ok"] is False
    assert result["reasonCode"] == "spec-mismatch"


def test_canonical_plan_matches_wired_route() -> None:
    result = compare_plan_to_wiring(build_hall_to_kitchen_plan())
    assert result["ok"] is True
    assert result["reasonCode"] == "ok"


def test_controller_found_alone_cannot_pass() -> None:
    result = judge_hall_attempt(
        {
            "attemptId": "a-api",
            "inputLayer": "api",
            "driver": "api",
            "initialScene": "Hall_playerble",
            "scenesVisited": ["Hall_playerble"],
            "destinationScene": "Hall_playerble",
            "transitionPending": False,
            "inputGateBlocked": False,
            "controllerFound": True,
        }
    )
    assert result["attemptVerdict"] != "PASS"
    assert "controller-only" in result["reasonCodes"]


def test_dual_layer_attempts_reset_and_record_distinct_drivers() -> None:
    attempts = record_dual_layer_attempts(
        [
            {
                "inputLayer": "api",
                "driver": "api",
                "initialScene": "Hall_playerble",
                "scenesVisited": [
                    "Hall_playerble",
                    "Hall_Left",
                    "Hall_Left2",
                    "Kitchen",
                ],
                "destinationScene": "Kitchen",
                "transitionPending": False,
                "inputGateBlocked": False,
                "controllerFound": True,
            },
            {
                "inputLayer": "event-system",
                "driver": "event-system",
                "initialScene": "Hall_playerble",
                "scenesVisited": [
                    "Hall_playerble",
                    "Hall_Left",
                    "Hall_Left2",
                    "Kitchen",
                ],
                "destinationScene": "Kitchen",
                "transitionPending": False,
                "inputGateBlocked": False,
                "controllerFound": True,
            },
        ]
    )
    assert len(attempts) == 2
    assert attempts[0]["attemptId"] != attempts[1]["attemptId"]
    assert attempts[0]["inputLayer"] == "api"
    assert attempts[1]["inputLayer"] == "event-system"
    assert attempts[0]["initialScene"] == "Hall_playerble"
    assert attempts[1]["initialScene"] == "Hall_playerble"
    assert attempts[0]["driver"] == "api"
    assert attempts[1]["driver"] == "event-system"
    judged = [judge_hall_attempt(item) for item in attempts]
    assert judged[0]["attemptVerdict"] == "PASS"
    assert judged[1]["attemptVerdict"] == "PASS"

"""Frozen Hall→Kitchen wiring from scene YAML (design AC06–AC08)."""

from __future__ import annotations

from collections.abc import Mapping, Sequence
from typing import Any

WIRED_HALL_TO_KITCHEN: dict[str, Any] = {
    "source": "scene-yaml-static-2026-09-15",
    "destination": "Kitchen",
    "hops": [
        {
            "scene": "Hall_playerble",
            "interactionId": "left",
            "fungusBlock": "Left_Clicked",
            "nextScene": "Hall_Left",
            "mechanism": "CorridorEntranceController.blockOutcomes",
            "targetId": "hall.kitchen-entry",
        },
        {
            "scene": "Hall_Left",
            "interactionId": None,
            "fungusBlock": "Front_clicked",
            "nextScene": "Hall_Left2",
            "mechanism": "Fungus.LoadScene",
        },
        {
            "scene": "Hall_Left2",
            "interactionId": None,
            "fungusBlock": "Door_Clicked",
            "nextScene": "Kitchen",
            "mechanism": "Fungus.LoadScene",
        },
    ],
}


def expected_scenes() -> list[str]:
    hops = WIRED_HALL_TO_KITCHEN["hops"]
    return [str(hop["scene"]) for hop in hops] + [
        str(WIRED_HALL_TO_KITCHEN["destination"])
    ]


def compare_plan_to_wiring(plan: Mapping[str, Any]) -> dict[str, Any]:
    """Reject plans that invent a direct Hall→Kitchen hop or omit wired corridors."""
    target = plan.get("target") if isinstance(plan.get("target"), Mapping) else {}
    plan_scenes = list(target.get("expectedScenes") or [])
    wired = expected_scenes()
    if plan_scenes != wired:
        return {
            "ok": False,
            "reasonCode": "spec-mismatch",
            "wiredScenes": wired,
            "planScenes": plan_scenes,
        }
    plan_hops = list(target.get("hops") or [])
    wired_hops = list(WIRED_HALL_TO_KITCHEN["hops"])
    if len(plan_hops) != len(wired_hops):
        return {"ok": False, "reasonCode": "spec-mismatch"}
    for plan_hop, wired_hop in zip(plan_hops, wired_hops, strict=True):
        if not isinstance(plan_hop, Mapping):
            return {"ok": False, "reasonCode": "spec-mismatch"}
        if str(plan_hop.get("nextScene")) != str(wired_hop["nextScene"]):
            return {"ok": False, "reasonCode": "spec-mismatch"}
        if str(plan_hop.get("scene")) != str(wired_hop["scene"]):
            return {"ok": False, "reasonCode": "spec-mismatch"}
    return {"ok": True, "reasonCode": "ok"}


def judge_hall_attempt(record: Mapping[str, Any]) -> dict[str, Any]:
    """Kitchen arrival, hop sequence, and input recovery. Controller presence is never enough."""
    reasons: list[str] = []
    wired = expected_scenes()
    visited = [str(item) for item in (record.get("scenesVisited") or [])]
    destination = str(record.get("destinationScene") or "")
    if record.get("controllerFound") and destination != "Kitchen":
        reasons.append("controller-only")
    if visited != wired:
        reasons.append("path-mismatch")
    if record.get("transitionPending"):
        reasons.append("transition-pending")
    if record.get("inputGateBlocked"):
        reasons.append("input-gate-blocked")
    if not str(record.get("driver") or "").strip():
        reasons.append("missing-driver")
    if str(record.get("initialScene") or "") != "Hall_playerble":
        reasons.append("initial-scene-mismatch")
    if destination != "Kitchen":
        reasons.append("destination-mismatch")

    verdict = "PASS" if not reasons else "FAIL"
    if "path-mismatch" in reasons and destination != "Kitchen":
        verdict = "FAIL"
    return {
        "attemptId": record.get("attemptId"),
        "attemptVerdict": verdict,
        "reasonCodes": reasons,
        "inputLayer": record.get("inputLayer"),
        "driver": record.get("driver"),
    }


def record_dual_layer_attempts(
    raw_attempts: Sequence[Mapping[str, Any]],
) -> list[dict[str, Any]]:
    """Stamp distinct attempt IDs after an implied reset to Hall_playerble."""
    recorded: list[dict[str, Any]] = []
    for index, raw in enumerate(raw_attempts):
        item = dict(raw)
        layer = str(item.get("inputLayer") or f"layer-{index}")
        item["attemptId"] = str(item.get("attemptId") or f"attempt-{index + 1}-{layer}")
        recorded.append(item)
    return recorded

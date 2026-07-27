"""Area orchestration order stub tests (design §13)."""

from __future__ import annotations

from scripts.qa.rooms.orchestrate_area import ORCHESTRATION_ORDER, orchestration_plan


def test_orchestration_order_matches_design_section_13() -> None:
    assert ORCHESTRATION_ORDER == (
        "audit",
        "smoke",
        "happy-guard",
        "transitions",
        "chained-traversal",
    )


def test_orchestration_plan_lists_room_packs_then_marks_chained_stub() -> None:
    plan = orchestration_plan(
        area_id="first-floor",
        room_ids=["hall", "kitchen", "maid-room"],
    )
    assert plan["areaId"] == "first-floor"
    assert plan["steps"][0]["phase"] == "audit"
    assert plan["steps"][1]["phase"] == "smoke"
    assert plan["steps"][1]["rooms"] == ["hall", "kitchen", "maid-room"]
    assert plan["steps"][-1]["phase"] == "chained-traversal"
    assert plan["steps"][-1]["status"] == "not implemented"

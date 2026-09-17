"""Defect packet contract (design AC20)."""

from __future__ import annotations

from scripts.qa.tool.defect import build_defect_packet


def _payload(**overrides: object) -> dict[str, object]:
    payload: dict[str, object] = {
        "requirementId": "REQ-QA-HALL-KITCHEN-NAV",
        "environment": {"os": "win32", "unity": "6000.0.36f1"},
        "reproduction": ["invoke hall.nav.click-kitchen-entry", "assert-route"],
        "expected": "Kitchen",
        "actual": "Hall_playerble",
        "evidenceIds": ["shot-kitchen-missing"],
        "productTreeBefore": "aaa",
        "productTreeAfter": "aaa",
    }
    payload.update(overrides)
    return payload


def test_defect_packet_includes_required_fields_when_product_unchanged() -> None:
    packet = build_defect_packet(_payload())
    assert packet["accepted"] is True
    assert packet["requirementId"] == "REQ-QA-HALL-KITCHEN-NAV"
    assert packet["environment"]["unity"] == "6000.0.36f1"
    assert packet["reproduction"]
    assert packet["expected"] == "Kitchen"
    assert packet["actual"] == "Hall_playerble"
    assert packet["evidenceIds"] == ["shot-kitchen-missing"]
    assert packet["reasonCodes"] == []


def test_defect_packet_rejected_when_product_tree_changed() -> None:
    packet = build_defect_packet(_payload(productTreeAfter="bbb"))
    assert packet["accepted"] is False
    assert "product-modified" in packet["reasonCodes"]

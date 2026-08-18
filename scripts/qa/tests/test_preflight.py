"""Preflight requiredCapabilities against a live capability id set."""

from __future__ import annotations

from scripts.qa.rooms.preflight import missing_required_capabilities

_KITCHEN_FAUCET_IDS = (
    "kitchen.faucet.preset.before-faucet",
    "kitchen.faucet.click",
    "kitchen.faucet.probe",
    "kitchen.faucet.assert-clicked",
    "kitchen.faucet.capture",
)


def test_empty_registry_reports_all_kitchen_caps_missing() -> None:
    missing = missing_required_capabilities(_KITCHEN_FAUCET_IDS, set())
    assert missing == list(_KITCHEN_FAUCET_IDS)


def test_kitchen_faucet_ids_present_yields_empty_missing() -> None:
    missing = missing_required_capabilities(_KITCHEN_FAUCET_IDS, list(_KITCHEN_FAUCET_IDS))
    assert missing == []


def test_partial_registry_reports_only_absent_ids() -> None:
    live = {
        "kitchen.faucet.preset.before-faucet",
        "kitchen.faucet.click",
        "kitchen.faucet.probe",
    }
    missing = missing_required_capabilities(_KITCHEN_FAUCET_IDS, live)
    assert missing == [
        "kitchen.faucet.assert-clicked",
        "kitchen.faucet.capture",
    ]

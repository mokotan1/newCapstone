"""Room manifest / scenario / transition schema validators."""

from __future__ import annotations

import pytest

from scripts.qa.rooms.schema import (
    SchemaError,
    validate_room_manifest,
    validate_room_scenario,
    validate_transition,
)


def test_manifest_requires_smoke_happy_and_guard() -> None:
    bad = {
        "schemaVersion": 1,
        "roomId": "kitchen",
        "areaId": "first-floor",
        "unityScenes": ["Kitchen"],
        "implementationStatus": "IMPLEMENTED",
        "entryPreset": "kitchen.before-bottle-fill",
        "requiredCapabilities": [],
        "scenarios": ["room.kitchen.smoke"],
        "exitContract": {"inventoryContains": [], "flags": {}, "unlocks": []},
    }
    with pytest.raises(SchemaError) as exc_info:
        validate_room_manifest(bad)
    message = str(exc_info.value).lower()
    assert "happy-path" in message or "guard" in message


def test_manifest_accepts_minimal_valid_kitchen() -> None:
    ok = {
        "schemaVersion": 1,
        "roomId": "kitchen",
        "areaId": "first-floor",
        "notionSource": "https://app.notion.com/p/32cea40d2678817b9f32fc52f944c472",
        "unityScenes": ["Kitchen"],
        "implementationStatus": "IMPLEMENTED",
        "entryPreset": "kitchen.before-bottle-fill",
        "requiredCapabilities": ["kitchen.faucet.probe"],
        "scenarios": [
            "room.kitchen.smoke",
            "room.kitchen.happy-path",
            "room.kitchen.guard.wrong-item",
            "room.kitchen.guard.reentry",
        ],
        "exitContract": {
            "inventoryContains": ["maid-room-key"],
            "flags": {"HaveMaidKey": True},
            "unlocks": ["maid-room"],
        },
    }
    validate_room_manifest(ok)


def test_manifest_rejects_unknown_implementation_status() -> None:
    bad = {
        "schemaVersion": 1,
        "roomId": "kitchen",
        "areaId": "first-floor",
        "unityScenes": ["Kitchen"],
        "implementationStatus": "DONE",
        "entryPreset": "kitchen.before-bottle-fill",
        "requiredCapabilities": [],
        "scenarios": [
            "room.kitchen.smoke",
            "room.kitchen.happy-path",
            "room.kitchen.guard.wrong-item",
        ],
        "exitContract": {"inventoryContains": [], "flags": {}, "unlocks": []},
    }
    with pytest.raises(SchemaError) as exc_info:
        validate_room_manifest(bad)
    assert "implementationstatus" in str(exc_info.value).lower()


def test_room_scenario_requires_stable_id_and_tier() -> None:
    with pytest.raises(SchemaError):
        validate_room_scenario({"schemaVersion": 1, "steps": []})
    validate_room_scenario(
        {
            "schemaVersion": 1,
            "id": "room.kitchen.smoke",
            "roomId": "kitchen",
            "tier": "smoke",
            "requiredCapabilities": [],
            "steps": [],
        }
    )


def test_transition_requires_regions_and_contracts() -> None:
    with pytest.raises(SchemaError) as exc_info:
        validate_transition(
            {
                "schemaVersion": 1,
                "id": "transition.kitchen-to-maid-room",
                "sourceRegion": "kitchen",
            }
        )
    message = str(exc_info.value).lower()
    assert "destination" in message or "prerequisites" in message

    validate_transition(
        {
            "schemaVersion": 1,
            "id": "transition.kitchen-to-maid-room",
            "sourceRegion": "kitchen",
            "destinationRegion": "maid-room",
            "entryPreset": "kitchen.before-bottle-fill",
            "prerequisites": ["inventory.opaque-bottle"],
            "lockedAssertions": ["door.maid.locked"],
            "sourceExitContract": ["inventory.maid-room-key", "flag.HaveMaidKey"],
            "destinationEntryContract": ["scene.MaidEntrance", "door.maid.unlocked"],
            "checkpointContract": ["resumeRegion.maid-room"],
        }
    )

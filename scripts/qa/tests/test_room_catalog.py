"""Canonical room catalog tests (design §5)."""

from __future__ import annotations

from scripts.qa.rooms.catalog import (
    IMPLEMENTATION_STATUS_PARTIAL,
    load_catalog,
    region_ids,
)


EXPECTED_REGION_IDS = {
    # §5.1 First floor
    "hall",
    "hall.left",
    "hall.right",
    "utility-room",
    "kitchen",
    "maid-room",
    "study-room",
    "study-bookcases",
    "prison",
    # §5.2 Second floor
    "second-floor.hall",
    "tutor-room",
    "child-room",
    "wife-room",
    "bed-room",
    # §5.3 Basement
    "basement.entry",
    "basement.hall",
    "basement.extraction",
    "basement.observation",
    "basement.brick",
    "basement.research",
}


def test_catalog_contains_all_section_5_regions() -> None:
    catalog = load_catalog()
    ids = set(region_ids(catalog))
    assert EXPECTED_REGION_IDS <= ids
    assert "kitchen" in ids
    assert "tutor-room" in ids
    assert "basement.research" in ids


def test_thin_wrap_rooms_are_partial() -> None:
    catalog = load_catalog()
    for room_id in (
        "kitchen",
        "hall",
        "maid-room",
        "study-room",
        "child-room",
        "wife-room",
        "bed-room",
    ):
        assert catalog["regions"][room_id]["implementationStatus"] == IMPLEMENTATION_STATUS_PARTIAL


def test_basement_and_detail_default_not_implemented() -> None:
    catalog = load_catalog()
    for room_id in (
        "study-bookcases",
        "basement.entry",
        "basement.hall",
        "basement.extraction",
        "basement.observation",
        "basement.brick",
        "basement.research",
    ):
        assert catalog["regions"][room_id]["implementationStatus"] == "NOT_IMPLEMENTED"


def test_kitchen_maps_to_kitchen_scene() -> None:
    catalog = load_catalog()
    assert catalog["regions"]["kitchen"]["unityScenes"] == ["Kitchen"]

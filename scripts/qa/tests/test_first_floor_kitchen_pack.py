"""First-floor kitchen room pack (design §8 reference)."""

from __future__ import annotations

import json
from pathlib import Path

from scripts.qa.rooms.schema import validate_room_manifest, validate_room_scenario

_REPO_ROOT = Path(__file__).resolve().parents[3]
_KITCHEN_DIR = (
    _REPO_ROOT
    / "disputatio"
    / "Assets"
    / "Resources"
    / "QA"
    / "Scenarios"
    / "Rooms"
    / "first-floor"
    / "kitchen"
)

_SCENARIO_FILES = (
    "smoke.json",
    "happy-path.json",
    "guard-wrong-item.json",
    "guard-reentry.json",
)


def test_kitchen_pack_files_exist() -> None:
    assert (_KITCHEN_DIR / "manifest.json").is_file()
    for name in _SCENARIO_FILES:
        assert (_KITCHEN_DIR / name).is_file(), name


def test_kitchen_manifest_validates() -> None:
    payload = json.loads((_KITCHEN_DIR / "manifest.json").read_text(encoding="utf-8"))
    validate_room_manifest(payload)
    assert payload["roomId"] == "kitchen"
    assert payload["areaId"] == "first-floor"
    assert payload["implementationStatus"] == "PARTIAL"
    assert "kitchen.faucet.click" in payload["requiredCapabilities"]


def test_kitchen_scenarios_validate_and_avoid_force_solve() -> None:
    for name in _SCENARIO_FILES:
        payload = json.loads((_KITCHEN_DIR / name).read_text(encoding="utf-8"))
        validate_room_scenario(payload)
        assert payload["roomId"] == "kitchen"
        blob = json.dumps(payload)
        assert "force-solve" not in blob
        assert "forceSolve" not in blob

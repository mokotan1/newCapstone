"""Second-floor room packs and transition stubs (design §5.2)."""

from __future__ import annotations

import json
from pathlib import Path

from scripts.qa.rooms.schema import (
    validate_room_manifest,
    validate_room_scenario,
    validate_transition,
)

_REPO_ROOT = Path(__file__).resolve().parents[3]
_ROOMS_ROOT = (
    _REPO_ROOT
    / "disputatio"
    / "Assets"
    / "Resources"
    / "QA"
    / "Scenarios"
    / "Rooms"
)
_SECOND_FLOOR = _ROOMS_ROOT / "second-floor"
_TRANSITIONS = _ROOMS_ROOT / "Transitions"

_PARTIAL_ROOMS = ("child-room", "wife-room", "bed-room")
_STUB_ROOMS = ("second-floor.hall", "tutor-room")
_SCENARIO_FILES = (
    "smoke.json",
    "happy-path.json",
    "guard-wrong-item.json",
    "guard-reentry.json",
)
_TRANSITION_FILES = (
    "transition.second-hall-to-child.json",
    "transition.child-to-wife.json",
    "transition.wife-to-bed.json",
)

_EXPECTED_CAPS: dict[str, tuple[str, ...]] = {
    "child-room": (
        "childroom.seals.click-seal5",
        "childroom.seals.probe",
        "childroom.seals.assert-controller",
        "childroom.seals.capture",
    ),
    "wife-room": (
        "wiferoom.wallclock.click",
        "wiferoom.wallclock.probe",
        "wiferoom.wallclock.assert-controller",
        "wiferoom.wallclock.capture",
    ),
    "bed-room": (
        "bedroom.book.click",
        "bedroom.book.probe",
        "bedroom.book.assert-controller",
        "bedroom.book.capture",
    ),
}


def test_partial_room_pack_files_exist_and_validate() -> None:
    for room_id in _PARTIAL_ROOMS:
        room_dir = _SECOND_FLOOR / room_id
        assert (room_dir / "manifest.json").is_file(), room_id
        for name in _SCENARIO_FILES:
            assert (room_dir / name).is_file(), f"{room_id}:{name}"

        manifest = json.loads((room_dir / "manifest.json").read_text(encoding="utf-8"))
        validate_room_manifest(manifest)
        assert manifest["roomId"] == room_id
        assert manifest["areaId"] == "second-floor"
        assert manifest["implementationStatus"] == "PARTIAL"
        for cap in _EXPECTED_CAPS[room_id]:
            assert cap in manifest["requiredCapabilities"]

        for name in _SCENARIO_FILES:
            scenario = json.loads((room_dir / name).read_text(encoding="utf-8"))
            validate_room_scenario(scenario)
            assert scenario["roomId"] == room_id
            blob = json.dumps(scenario)
            assert "force-solve" not in blob
            assert "forceSolve" not in blob


def test_partial_happy_paths_are_invoke_only_without_realinput() -> None:
    for room_id in _PARTIAL_ROOMS:
        payload = json.loads(
            (_SECOND_FLOOR / room_id / "happy-path.json").read_text(encoding="utf-8")
        )
        validate_room_scenario(payload)
        names = [(step["family"], step["name"]) for step in payload["steps"]]
        assert ("interaction", "pointer") not in names
        assert ("evidence", "capture") in names
        assert any(
            step.get("family") == "interaction" and step.get("name") == "invoke"
            for step in payload["steps"]
        )


def test_stub_rooms_have_not_implemented_smoke_only() -> None:
    for room_id in _STUB_ROOMS:
        room_dir = _SECOND_FLOOR / room_id
        assert (room_dir / "manifest.json").is_file(), room_id
        assert (room_dir / "smoke.json").is_file(), room_id
        for name in ("happy-path.json", "guard-wrong-item.json", "guard-reentry.json"):
            assert not (room_dir / name).exists(), f"{room_id} should not ship {name}"

        manifest = json.loads((room_dir / "manifest.json").read_text(encoding="utf-8"))
        validate_room_manifest(manifest)
        assert manifest["roomId"] == room_id
        assert manifest["areaId"] == "second-floor"
        assert manifest["implementationStatus"] == "NOT_IMPLEMENTED"
        assert manifest["requiredCapabilities"] == []

        smoke = json.loads((room_dir / "smoke.json").read_text(encoding="utf-8"))
        validate_room_scenario(smoke)
        assert smoke["steps"] == []
        assert smoke.get("expectedVerdict") == "NOT_IMPLEMENTED"


def test_second_floor_transitions_validate() -> None:
    for name in _TRANSITION_FILES:
        payload = json.loads((_TRANSITIONS / name).read_text(encoding="utf-8"))
        validate_transition(payload)
        assert payload["id"] == name.removesuffix(".json")

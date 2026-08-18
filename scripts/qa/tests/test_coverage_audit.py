"""Static room coverage audit tests (design §12)."""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from scripts.qa.rooms.coverage_audit import (
    CoverageAuditError,
    audit_coverage,
    parse_build_settings_scenes,
)


def test_parse_build_settings_extracts_scene_stems(tmp_path: Path) -> None:
    asset = tmp_path / "EditorBuildSettings.asset"
    asset.write_text(
        "%YAML 1.1\n"
        "EditorBuildSettings:\n"
        "  m_Scenes:\n"
        "  - enabled: 1\n"
        "    path: Assets/Scenes/Mokotan/First Floor/1foorLeft/Kitchen.unity\n"
        "  - enabled: 0\n"
        "    path: Assets/Scenes/Ignored.unity\n",
        encoding="utf-8",
    )
    scenes = parse_build_settings_scenes(asset)
    assert scenes == ["Kitchen"]


def test_report_only_lists_missing_manifests_for_empty_rooms_tree(tmp_path: Path) -> None:
    rooms_root = tmp_path / "Rooms"
    rooms_root.mkdir()
    catalog = {
        "schemaVersion": 1,
        "regions": {
            "kitchen": {
                "areaId": "first-floor",
                "unityScenes": ["Kitchen"],
                "implementationStatus": "PARTIAL",
            },
            "hall": {
                "areaId": "first-floor",
                "unityScenes": ["Hall_animate"],
                "implementationStatus": "PARTIAL",
            },
        },
    }
    catalog_path = tmp_path / "catalog.json"
    catalog_path.write_text(json.dumps(catalog), encoding="utf-8")
    exclusions_path = tmp_path / "exclusions.json"
    exclusions_path.write_text(
        json.dumps({"schemaVersion": 1, "scenes": {}}),
        encoding="utf-8",
    )
    build_settings = tmp_path / "EditorBuildSettings.asset"
    build_settings.write_text(
        "m_Scenes:\n"
        "  - enabled: 1\n"
        "    path: Assets/Scenes/Kitchen.unity\n"
        "  - enabled: 1\n"
        "    path: Assets/Scenes/Hall_animate.unity\n",
        encoding="utf-8",
    )

    report = audit_coverage(
        catalog_path=catalog_path,
        rooms_root=rooms_root,
        exclusions_path=exclusions_path,
        build_settings_path=build_settings,
        report_only=True,
    )
    missing = report["gaps"]["missingManifests"]
    assert "kitchen" in missing
    assert "hall" in missing


def test_strict_mode_raises_on_missing_manifests(tmp_path: Path) -> None:
    rooms_root = tmp_path / "Rooms"
    rooms_root.mkdir()
    catalog_path = tmp_path / "catalog.json"
    catalog_path.write_text(
        json.dumps(
            {
                "schemaVersion": 1,
                "regions": {
                    "kitchen": {
                        "areaId": "first-floor",
                        "unityScenes": ["Kitchen"],
                        "implementationStatus": "PARTIAL",
                    }
                },
            }
        ),
        encoding="utf-8",
    )
    exclusions_path = tmp_path / "exclusions.json"
    exclusions_path.write_text(
        json.dumps({"schemaVersion": 1, "scenes": {}}),
        encoding="utf-8",
    )
    build_settings = tmp_path / "EditorBuildSettings.asset"
    build_settings.write_text(
        "m_Scenes:\n  - enabled: 1\n    path: Assets/Scenes/Kitchen.unity\n",
        encoding="utf-8",
    )

    with pytest.raises(CoverageAuditError):
        audit_coverage(
            catalog_path=catalog_path,
            rooms_root=rooms_root,
            exclusions_path=exclusions_path,
            build_settings_path=build_settings,
            report_only=False,
        )


def test_implemented_room_accepts_guard_wrong_item_filename(tmp_path: Path) -> None:
    """Packs ship guard-wrong-item.json; audit must not require guard-wrong-input.json."""
    rooms_root = tmp_path / "Rooms"
    room_dir = rooms_root / "first-floor" / "kitchen"
    room_dir.mkdir(parents=True)
    (room_dir / "manifest.json").write_text(
        json.dumps(
            {
                "schemaVersion": 1,
                "roomId": "kitchen",
                "areaId": "first-floor",
                "unityScenes": ["Kitchen"],
                "implementationStatus": "IMPLEMENTED",
                "entryPreset": "kitchen.sink.preset.before-bottle-fill",
                "requiredCapabilities": ["kitchen.faucet.probe"],
                "scenarios": [
                    "room.kitchen.smoke",
                    "room.kitchen.happy-path",
                    "room.kitchen.guard.wrong-item",
                    "room.kitchen.guard.reentry",
                ],
                "exitContract": {
                    "inventoryContains": [],
                    "flags": {},
                    "unlocks": [],
                },
            }
        ),
        encoding="utf-8",
    )
    for name in (
        "smoke.json",
        "happy-path.json",
        "guard-wrong-item.json",
        "guard-reentry.json",
    ):
        (room_dir / name).write_text(
            json.dumps(
                {
                    "schemaVersion": 1,
                    "id": f"room.kitchen.{name.replace('.json', '').replace('-', '.')}",
                    "roomId": "kitchen",
                    "tier": "smoke" if name == "smoke.json" else "happy-path" if name == "happy-path.json" else "guard",
                    "requiredCapabilities": ["kitchen.faucet.probe"],
                    "steps": [
                        {
                            "id": "probe",
                            "family": "interaction",
                            "name": "invoke",
                            "targetId": "kitchen.faucet.probe",
                        }
                    ],
                }
            ),
            encoding="utf-8",
        )

    catalog_path = tmp_path / "catalog.json"
    catalog_path.write_text(
        json.dumps(
            {
                "schemaVersion": 1,
                "regions": {
                    "kitchen": {
                        "areaId": "first-floor",
                        "unityScenes": ["Kitchen"],
                        "implementationStatus": "IMPLEMENTED",
                    }
                },
            }
        ),
        encoding="utf-8",
    )
    exclusions_path = tmp_path / "exclusions.json"
    exclusions_path.write_text(
        json.dumps({"schemaVersion": 1, "scenes": {}}),
        encoding="utf-8",
    )
    build_settings = tmp_path / "EditorBuildSettings.asset"
    build_settings.write_text(
        "m_Scenes:\n  - enabled: 1\n    path: Assets/Scenes/Kitchen.unity\n",
        encoding="utf-8",
    )

    report = audit_coverage(
        catalog_path=catalog_path,
        rooms_root=rooms_root,
        exclusions_path=exclusions_path,
        build_settings_path=build_settings,
        report_only=True,
    )
    missing = report["gaps"]["missingScenarioFiles"]
    assert "kitchen:guard-wrong-input.json" not in missing
    assert "kitchen:guard-wrong-item.json" not in missing


def test_unmapped_build_scene_requires_exclusion_reason(tmp_path: Path) -> None:
    rooms_root = tmp_path / "Rooms"
    rooms_root.mkdir()
    catalog_path = tmp_path / "catalog.json"
    catalog_path.write_text(
        json.dumps({"schemaVersion": 1, "regions": {}}),
        encoding="utf-8",
    )
    exclusions_path = tmp_path / "exclusions.json"
    exclusions_path.write_text(
        json.dumps({"schemaVersion": 1, "scenes": {}}),
        encoding="utf-8",
    )
    build_settings = tmp_path / "EditorBuildSettings.asset"
    build_settings.write_text(
        "m_Scenes:\n  - enabled: 1\n    path: Assets/Scenes/MainMenuScene.unity\n",
        encoding="utf-8",
    )

    report = audit_coverage(
        catalog_path=catalog_path,
        rooms_root=rooms_root,
        exclusions_path=exclusions_path,
        build_settings_path=build_settings,
        report_only=True,
    )
    assert "MainMenuScene" in report["gaps"]["unmappedBuildScenes"]

    exclusions_path.write_text(
        json.dumps(
            {
                "schemaVersion": 1,
                "scenes": {
                    "MainMenuScene": {
                        "reason": "Opening/menu — out of first delivery scope",
                        "category": "settings",
                    }
                },
            }
        ),
        encoding="utf-8",
    )
    report2 = audit_coverage(
        catalog_path=catalog_path,
        rooms_root=rooms_root,
        exclusions_path=exclusions_path,
        build_settings_path=build_settings,
        report_only=True,
    )
    assert "MainMenuScene" not in report2["gaps"]["unmappedBuildScenes"]

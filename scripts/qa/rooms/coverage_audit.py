"""Static Build Settings / catalog / manifest coverage audit (design §12)."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
from typing import Any, Mapping

from scripts.qa.rooms.catalog import default_catalog_path, load_catalog

_REPO_ROOT = Path(__file__).resolve().parents[3]
_DEFAULT_BUILD_SETTINGS = (
    _REPO_ROOT / "disputatio" / "ProjectSettings" / "EditorBuildSettings.asset"
)
_DEFAULT_ROOMS_ROOT = (
    _REPO_ROOT
    / "disputatio"
    / "Assets"
    / "Resources"
    / "QA"
    / "Scenarios"
    / "Rooms"
)
_DEFAULT_EXCLUSIONS = _DEFAULT_ROOMS_ROOT / "exclusions.json"

_PATH_RE = re.compile(r"^\s*path:\s*(.+)$")
_ENABLED_RE = re.compile(r"^\s*-\s*enabled:\s*([01])\s*$")


class CoverageAuditError(RuntimeError):
    """Raised when a strict coverage audit finds one or more gaps."""


def parse_build_settings_scenes(build_settings_path: Path | str) -> list[str]:
    """Parse enabled scene stems from EditorBuildSettings.asset YAML."""
    path = Path(build_settings_path)
    text = path.read_text(encoding="utf-8")
    scenes: list[str] = []
    enabled: bool | None = None
    for line in text.splitlines():
        enabled_match = _ENABLED_RE.match(line)
        if enabled_match:
            enabled = enabled_match.group(1) == "1"
            continue
        path_match = _PATH_RE.match(line)
        if path_match and enabled is True:
            raw = path_match.group(1).strip()
            stem = Path(raw).stem
            scenes.append(stem)
            enabled = None
        elif path_match:
            enabled = None
    return scenes


def _load_exclusions(exclusions_path: Path) -> dict[str, Any]:
    payload = json.loads(exclusions_path.read_text(encoding="utf-8"))
    if not isinstance(payload, Mapping):
        raise CoverageAuditError("exclusions.json must be an object")
    scenes = payload.get("scenes", {})
    if not isinstance(scenes, Mapping):
        raise CoverageAuditError("exclusions.scenes must be an object")
    for scene_name, meta in scenes.items():
        if not isinstance(meta, Mapping) or not str(meta.get("reason", "")).strip():
            raise CoverageAuditError(
                f"exclusion for {scene_name!r} must include a non-empty reason "
                "(silent exclusion forbidden)"
            )
    return dict(payload)


def _manifest_path(rooms_root: Path, area_id: str, room_id: str) -> Path:
    return rooms_root / area_id / room_id / "manifest.json"


# Canonical on-disk guard filename used by room packs. Accept the older
# design-doc name as an alias so either file satisfies coverage.
_GUARD_WRONG_ITEM = "guard-wrong-item.json"
_GUARD_WRONG_INPUT_ALIAS = "guard-wrong-input.json"


def _required_scenario_files(room_id: str) -> list[str]:
    return [
        "smoke.json",
        "happy-path.json",
        _GUARD_WRONG_ITEM,
        "guard-reentry.json",
    ]


def _scenario_file_present(room_dir: Path, filename: str) -> bool:
    """True when the required pack file exists, including guard-wrong-input alias."""
    if (room_dir / filename).is_file():
        return True
    if filename == _GUARD_WRONG_ITEM and (room_dir / _GUARD_WRONG_INPUT_ALIAS).is_file():
        return True
    return False


def audit_coverage(
    *,
    catalog_path: Path | str | None = None,
    rooms_root: Path | str | None = None,
    exclusions_path: Path | str | None = None,
    build_settings_path: Path | str | None = None,
    report_only: bool = True,
) -> dict[str, Any]:
    """
    Run static coverage audit.

    When report_only is True, return a structured gap report.
    When report_only is False, raise CoverageAuditError if any gap exists.
    """
    catalog_file = Path(catalog_path) if catalog_path else default_catalog_path()
    rooms = Path(rooms_root) if rooms_root else _DEFAULT_ROOMS_ROOT
    exclusions_file = Path(exclusions_path) if exclusions_path else _DEFAULT_EXCLUSIONS
    build_file = Path(build_settings_path) if build_settings_path else _DEFAULT_BUILD_SETTINGS

    catalog = load_catalog(str(catalog_file))

    exclusions = _load_exclusions(exclusions_file) if exclusions_file.exists() else {
        "schemaVersion": 1,
        "scenes": {},
    }
    excluded_scenes = set(exclusions.get("scenes", {}).keys())

    build_scenes = parse_build_settings_scenes(build_file)
    build_set = set(build_scenes)

    scene_to_region: dict[str, str] = {}
    for region_id, region in catalog.get("regions", {}).items():
        for scene in region.get("unityScenes", []):
            scene_to_region.setdefault(str(scene), str(region_id))

    missing_manifests: list[str] = []
    missing_scenario_files: list[str] = []
    missing_manifest_scenes: list[str] = []
    undeclared_capabilities: list[str] = []
    unmapped_build_scenes: list[str] = []

    for region_id, region in catalog.get("regions", {}).items():
        area_id = str(region.get("areaId", "unknown"))
        manifest = _manifest_path(rooms, area_id, region_id)
        if not manifest.is_file():
            missing_manifests.append(region_id)
            continue

        try:
            manifest_data = json.loads(manifest.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            raise CoverageAuditError(f"invalid manifest JSON: {manifest}") from exc

        for scene in manifest_data.get("unityScenes", []):
            if scene not in build_set:
                missing_manifest_scenes.append(f"{region_id}:{scene}")

        required_caps = set(manifest_data.get("requiredCapabilities", []))
        status = manifest_data.get("implementationStatus")
        room_dir = manifest.parent
        if status == "IMPLEMENTED":
            for filename in _required_scenario_files(region_id):
                if not _scenario_file_present(room_dir, filename):
                    missing_scenario_files.append(f"{region_id}:{filename}")
                    continue
                scenario_path = room_dir / filename
                if not scenario_path.is_file() and filename == _GUARD_WRONG_ITEM:
                    scenario_path = room_dir / _GUARD_WRONG_INPUT_ALIAS
                try:
                    scenario = json.loads(scenario_path.read_text(encoding="utf-8"))
                except json.JSONDecodeError:
                    undeclared_capabilities.append(f"{region_id}:{filename}:invalid-json")
                    continue
                for cap in scenario.get("requiredCapabilities", []):
                    if cap not in required_caps:
                        undeclared_capabilities.append(f"{region_id}:{cap}")

    for scene in build_scenes:
        if scene in excluded_scenes:
            continue
        if scene not in scene_to_region:
            unmapped_build_scenes.append(scene)

    gaps = {
        "missingManifests": sorted(set(missing_manifests)),
        "missingScenarioFiles": sorted(set(missing_scenario_files)),
        "manifestScenesMissingFromBuild": sorted(set(missing_manifest_scenes)),
        "undeclaredCapabilities": sorted(set(undeclared_capabilities)),
        "unmappedBuildScenes": sorted(set(unmapped_build_scenes)),
    }
    has_gaps = any(gaps[key] for key in gaps)
    report: dict[str, Any] = {
        "ok": not has_gaps,
        "gaps": gaps,
        "buildSceneCount": len(build_scenes),
        "catalogRegionCount": len(catalog.get("regions", {})),
        "excludedSceneCount": len(excluded_scenes),
    }

    if has_gaps and not report_only:
        summary = "; ".join(f"{key}={len(value)}" for key, value in gaps.items() if value)
        raise CoverageAuditError(f"coverage audit failed: {summary}")
    return report


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Static room coverage audit")
    parser.add_argument(
        "--report-only",
        action="store_true",
        default=True,
        help="Return structured gaps without raising (default)",
    )
    parser.add_argument(
        "--strict",
        action="store_true",
        help="Raise CoverageAuditError when gaps exist",
    )
    args = parser.parse_args(argv)
    report_only = not args.strict
    report = audit_coverage(report_only=report_only)
    print(json.dumps(report, indent=2, ensure_ascii=False))
    return 0 if report["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())

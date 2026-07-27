"""Canonical room / region catalog loader (design §5)."""

from __future__ import annotations

import json
from functools import lru_cache
from pathlib import Path
from typing import Any, Mapping

IMPLEMENTATION_STATUS_PARTIAL = "PARTIAL"
IMPLEMENTATION_STATUS_NOT_IMPLEMENTED = "NOT_IMPLEMENTED"
IMPLEMENTATION_STATUS_IMPLEMENTED = "IMPLEMENTED"
IMPLEMENTATION_STATUS_SPEC_MISMATCH = "SPEC_MISMATCH"

_REPO_ROOT = Path(__file__).resolve().parents[3]
_DEFAULT_CATALOG_PATH = (
    _REPO_ROOT
    / "disputatio"
    / "Assets"
    / "Resources"
    / "QA"
    / "Scenarios"
    / "Rooms"
    / "catalog.json"
)


def default_catalog_path() -> Path:
    return _DEFAULT_CATALOG_PATH


@lru_cache(maxsize=1)
def load_catalog(catalog_path: str | None = None) -> dict[str, Any]:
    """Load the machine-readable region catalog."""
    path = Path(catalog_path) if catalog_path else _DEFAULT_CATALOG_PATH
    payload = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(payload, Mapping) or "regions" not in payload:
        raise ValueError(f"catalog missing regions object: {path}")
    regions = payload["regions"]
    if not isinstance(regions, Mapping) or not regions:
        raise ValueError(f"catalog regions must be a non-empty object: {path}")
    return dict(payload)


def region_ids(catalog: Mapping[str, Any] | None = None) -> list[str]:
    data = catalog if catalog is not None else load_catalog()
    return sorted(str(key) for key in data["regions"].keys())


def region_scene_map(catalog: Mapping[str, Any] | None = None) -> dict[str, str]:
    """Map Unity scene stem -> region id (first claimant wins; audit detects duplicates)."""
    data = catalog if catalog is not None else load_catalog()
    mapping: dict[str, str] = {}
    for region_id, region in data["regions"].items():
        scenes = region.get("unityScenes", [])
        if not isinstance(scenes, list):
            continue
        for scene in scenes:
            if scene not in mapping:
                mapping[str(scene)] = str(region_id)
    return mapping

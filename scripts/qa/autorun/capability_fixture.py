"""In-memory capability registry fixture for autorun E2E (Task 12).

Simulates Unity DeveloperQaCapabilityRegistry without requiring an Editor.
Used to demonstrate: missing capability → patch register → resume invoke.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

PLACE_BOOKMARK_CAPABILITY_ID = "studyroom.mirror.place-bookmark"


class InMemoryCapabilityRegistry:
    """Minimal capability catalog used by the Python orchestrator E2E slice."""

    def __init__(self, initial_ids: set[str] | None = None) -> None:
        self._ids: set[str] = set(initial_ids or set())
        self._version_number = 0 if not self._ids else 1

    @property
    def version(self) -> str:
        return str(self._version_number)

    def list_ids(self) -> set[str]:
        return set(self._ids)

    def has(self, capability_id: str) -> bool:
        if not capability_id or not capability_id.strip():
            return False
        return capability_id in self._ids

    def register(self, capability_id: str) -> None:
        if not capability_id or not capability_id.strip():
            raise ValueError("capability_id must be non-empty")
        self._ids.add(capability_id)
        self._version_number += 1

    def invoke(self, capability_id: str) -> dict[str, Any]:
        """Return structured evidence shaped like DeveloperQaResult fields."""
        if not capability_id or not capability_id.strip():
            return {
                "result_code": "InvalidCommand",
                "invalid_schema": True,
            }
        if capability_id not in self._ids:
            return {
                "result_code": "MissingCapability",
                "missing_capability_id": capability_id,
                "capability_executed": False,
                "current_capabilities": ",".join(sorted(self._ids)),
            }
        return {
            "result_code": "Ok",
            "capability_id": capability_id,
            "capability_executed": True,
        }


def apply_fixture_capability_patch(
    registry: InMemoryCapabilityRegistry,
    *,
    capability_id: str,
    patch_dir: Path | str,
) -> dict[str, Any]:
    """Simulate a QA capability patch: write patch metadata + register in-memory.

    Does not touch git. Returns metadata suitable for journal/report.
    """
    if not capability_id or not capability_id.strip():
        raise ValueError("capability_id must be non-empty")

    target = Path(patch_dir)
    target.mkdir(parents=True, exist_ok=True)
    safe_name = capability_id.replace(".", "_").replace("/", "_")
    patch_path = target / f"add-{safe_name}.json"
    payload = {
        "kind": "feat(qa)",
        "capability_id": capability_id,
        "action": "register",
        "note": "fixture simulation of Developer Mode capability patch",
    }
    patch_path.write_text(
        json.dumps(payload, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    registry.register(capability_id)
    return {
        "capability_id": capability_id,
        "patch_path": str(patch_path),
        "registry_version": registry.version,
    }

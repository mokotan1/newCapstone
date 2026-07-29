"""Area orchestration order stub (design §13)."""

from __future__ import annotations

import argparse
import json
from typing import Any, Sequence

from scripts.qa.rooms.catalog import load_catalog
from scripts.qa.rooms.coverage_audit import audit_coverage

ORCHESTRATION_ORDER: tuple[str, ...] = (
    "audit",
    "smoke",
    "happy-guard",
    "transitions",
    "chained-traversal",
)


def orchestration_plan(*, area_id: str, room_ids: Sequence[str]) -> dict[str, Any]:
    """Return the deterministic within-area execution order (stub runners)."""
    rooms = list(room_ids)
    return {
        "areaId": area_id,
        "order": list(ORCHESTRATION_ORDER),
        "steps": [
            {
                "phase": "audit",
                "action": "static catalog and capability audit",
            },
            {
                "phase": "smoke",
                "action": "run all smoke scenarios",
                "rooms": rooms,
            },
            {
                "phase": "happy-guard",
                "action": "run happy-path and guard scenarios independently",
                "rooms": rooms,
            },
            {
                "phase": "transitions",
                "action": "run transition scenarios in progression order",
                "rooms": rooms,
            },
            {
                "phase": "chained-traversal",
                "action": "run area chained traversal",
                "status": "not implemented",
            },
        ],
    }


def list_room_packs_for_area(area_id: str) -> list[str]:
    catalog = load_catalog()
    rooms = [
        region_id
        for region_id, region in catalog.get("regions", {}).items()
        if region.get("areaId") == area_id
    ]
    return sorted(rooms)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Room-area orchestration order stub")
    parser.add_argument("--area", default="first-floor", help="Area id to list")
    parser.add_argument(
        "--skip-audit",
        action="store_true",
        help="Skip printing report-only coverage audit",
    )
    args = parser.parse_args(argv)

    if not args.skip_audit:
        report = audit_coverage(report_only=True)
        print("=== coverage audit (report-only) ===")
        print(json.dumps(report, indent=2, ensure_ascii=False))

    rooms = list_room_packs_for_area(args.area)
    plan = orchestration_plan(area_id=args.area, room_ids=rooms)
    print("=== orchestration order ===")
    print(json.dumps(plan, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

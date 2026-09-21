"""CLI entry for P0 inventory and scanner reports."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from fungus_deletion.inventory import scan_scenes, summarize
from fungus_deletion.scanner import scan_csharp_tree, top_files_by_hits
from fungus_deletion.simple_room_strip import (
    discover_start_only_scenes,
    strip_start_fade_scenes,
)


def _default_scenes_root(repo_root: Path) -> Path:
    primary = repo_root / "disputatio" / "Assets" / "Scenes"
    if primary.is_dir():
        return primary
    fallback = repo_root / "Assets" / "Scenes"
    return fallback if fallback.is_dir() else primary


def _default_script_root(repo_root: Path) -> Path:
    return repo_root / "disputatio" / "Assets"


def cmd_inventory(repo_root: Path) -> int:
    scenes_root = _default_scenes_root(repo_root)
    reports = scan_scenes(scenes_root)
    payload = {
        "summary": summarize(reports),
        "scenes": [
            {
                "path": r.scene_path,
                "block_count": r.block_count,
                "execute_block_refs": r.execute_block_refs,
                "clickable2d_refs": r.clickable2d_refs,
            }
            for r in reports
            if r.block_count > 0 or r.execute_block_refs > 0
        ],
    }
    print(json.dumps(payload, indent=2, ensure_ascii=False))
    return 0


def cmd_strip_start_fade(repo_root: Path, apply: bool) -> int:
    scenes_root = _default_scenes_root(repo_root)
    targets = discover_start_only_scenes(scenes_root)
    results = strip_start_fade_scenes(targets, dry_run=not apply)
    payload = {
        "apply": apply,
        "target_count": len(targets),
        "results": [
            {
                "path": r.scene_path,
                "applied": r.applied,
                "reason": r.reason,
                "target_alpha": r.target_alpha,
                "host_object_name": r.host_object_name,
            }
            for r in results
        ],
    }
    print(json.dumps(payload, indent=2, ensure_ascii=False))
    ok = sum(1 for r in results if r.applied)
    return 0 if ok == len(results) else 1


def cmd_scan(repo_root: Path) -> int:
    script_root = _default_script_root(repo_root)
    reports = scan_csharp_tree(script_root)
    payload = {
        "file_count": len(reports),
        "top_by_hits": [
            {
                "path": r.file_path,
                "execute_block": r.execute_block_hits,
                "flowchart": r.flowchart_hits,
                "variable": r.variable_hits,
            }
            for r in top_files_by_hits(reports)
        ],
    }
    print(json.dumps(payload, indent=2, ensure_ascii=False))
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Fungus deletion P0 inventory/scanner")
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path("."),
        help="Repository root (default: current directory)",
    )
    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("inventory", help="Scan scene YAML for Flowchart-related signals")
    sub.add_parser("scan", help="Scan C# for Fungus API references")

    strip_p = sub.add_parser(
        "strip-start-fade",
        help="Remove Start+Fade-only Flowchart; add BasementRoomEnterFade",
    )
    strip_p.add_argument(
        "--apply",
        action="store_true",
        help="Write scene files (default: dry-run only)",
    )

    args = parser.parse_args()
    repo_root = args.repo_root.resolve()
    if args.command == "inventory":
        return cmd_inventory(repo_root)
    if args.command == "scan":
        return cmd_scan(repo_root)
    if args.command == "strip-start-fade":
        return cmd_strip_start_fade(repo_root, apply=args.apply)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())

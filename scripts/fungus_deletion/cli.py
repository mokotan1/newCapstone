"""CLI entry for P0 inventory and scanner reports."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from fungus_deletion.inventory import scan_scenes, summarize
from fungus_deletion.scanner import scan_csharp_tree, top_files_by_hits


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
    parser.add_argument("--repo-root", type=Path, default=Path("."))
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("inventory", help="Scan scene YAML for Flowchart-related signals")
    sub.add_parser("scan", help="Scan C# for Fungus API references")
    args = parser.parse_args()
    repo_root = args.repo_root.resolve()
    if args.command == "inventory":
        return cmd_inventory(repo_root)
    if args.command == "scan":
        return cmd_scan(repo_root)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())

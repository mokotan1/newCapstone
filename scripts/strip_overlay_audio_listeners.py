#!/usr/bin/env python3
"""Remove AudioListener components from URP Overlay cameras in Unity YAML assets."""

from __future__ import annotations

import re
import sys
from pathlib import Path


GAMEOBJECT_HEADER = re.compile(r"^--- !u!1 &(\d+)")
COMPONENT_HEADER = re.compile(r"^--- !u!(\d+) &(\d+)")
AUDIO_LISTENER_TYPE = "81"
CAMERA_TYPE = "20"


def parse_blocks(text: str) -> list[tuple[str, str, list[str]]]:
    """Return (type_id, file_id, lines) for each YAML document block."""
    blocks: list[tuple[str, str, list[str]]] = []
    current_type = ""
    current_id = ""
    current_lines: list[str] = []

    for line in text.splitlines(keepends=True):
        match = COMPONENT_HEADER.match(line)
        if match:
            if current_lines:
                blocks.append((current_type, current_id, current_lines))
            current_type, current_id = match.group(1), match.group(2)
            current_lines = [line]
        else:
            current_lines.append(line)

    if current_lines:
        blocks.append((current_type, current_id, current_lines))

    return blocks


def get_game_object_id(lines: list[str]) -> str | None:
    for line in lines:
        if line.startswith("  m_GameObject:"):
            match = re.search(r"\{fileID:\s*(\d+)\}", line)
            if match:
                return match.group(1)
    return None


def is_overlay_camera_data(lines: list[str]) -> bool:
    for line in lines:
        if line.strip() == "m_CameraType: 1":
            return True
    return False


def strip_overlay_audio_listeners(text: str) -> tuple[str, int]:
    blocks = parse_blocks(text)
    if not blocks:
        return text, 0

    game_object_components: dict[str, list[str]] = {}
    component_to_game_object: dict[str, str] = {}
    overlay_game_objects: set[str] = set()
    audio_listener_ids: dict[str, str] = {}

    for type_id, file_id, lines in blocks:
        if type_id == "1":
            for line in lines:
                if line.startswith("  - component:"):
                    match = re.search(r"\{fileID:\s*(\d+)\}", line)
                    if match:
                        game_object_components.setdefault(file_id, []).append(match.group(1))
        else:
            game_object_id = get_game_object_id(lines)
            if game_object_id:
                component_to_game_object[file_id] = game_object_id
                if type_id == AUDIO_LISTENER_TYPE:
                    audio_listener_ids[file_id] = game_object_id
                elif type_id == "114" and is_overlay_camera_data(lines):
                    overlay_game_objects.add(game_object_id)

    removable_listener_ids = {
        listener_id
        for listener_id, game_object_id in audio_listener_ids.items()
        if game_object_id in overlay_game_objects
    }

    if not removable_listener_ids:
        return text, 0

    rebuilt_blocks: list[str] = []
    removed = 0
    for type_id, file_id, lines in blocks:
        if type_id == AUDIO_LISTENER_TYPE and file_id in removable_listener_ids:
            removed += 1
            continue

        if type_id == "1":
            filtered_lines: list[str] = []
            for line in lines:
                if line.startswith("  - component:"):
                    match = re.search(r"\{fileID:\s*(\d+)\}", line)
                    if match and match.group(1) in removable_listener_ids:
                        continue
                filtered_lines.append(line)
            rebuilt_blocks.extend(filtered_lines)
            continue

        rebuilt_blocks.extend(lines)

    return "".join(rebuilt_blocks), removed


def process_asset(path: Path) -> int:
    original = path.read_text(encoding="utf-8")
    updated, removed = strip_overlay_audio_listeners(original)
    if removed > 0:
        path.write_text(updated, encoding="utf-8", newline="\n")
    return removed


def main() -> int:
    root = Path(__file__).resolve().parents[1] / "disputatio" / "Assets"
    if not root.exists():
        print(f"Assets folder not found: {root}", file=sys.stderr)
        return 1

    total_removed = 0
    changed_files = 0
    for pattern in ("**/*.unity", "**/*.prefab"):
        for asset_path in sorted(root.glob(pattern)):
            removed = process_asset(asset_path)
            if removed > 0:
                changed_files += 1
                total_removed += removed
                print(f"{asset_path.relative_to(root.parent)}: removed {removed}")

    print(f"Done. Changed {changed_files} assets, removed {total_removed} AudioListener components.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

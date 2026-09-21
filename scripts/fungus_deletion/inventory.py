"""Scan Unity scene YAML for Flowchart block names and risk signals."""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from pathlib import Path


_FLOWCHART_MARKER = "Flowchart:"
_SCENE_GLOB = "**/*.unity"


@dataclass
class SceneFlowchartReport:
    scene_path: str
    block_names: list[str] = field(default_factory=list)
    execute_block_refs: int = 0
    clickable2d_refs: int = 0

    @property
    def block_count(self) -> int:
        return len(self.block_names)


def scan_scene_file(scene_path: Path) -> SceneFlowchartReport:
    text = scene_path.read_text(encoding="utf-8", errors="replace")
    rel = scene_path.as_posix()
    blocks: list[str] = []
    if "Flowchart" in text or "blockName:" in text:
        for match in re.finditer(r"^\s*blockName:\s*(.+)$", text, re.MULTILINE):
            name = match.group(1).strip()
            if name:
                blocks.append(name)
    execute_refs = text.count("ExecuteBlock")
    clickable_refs = text.count("Clickable2D")
    return SceneFlowchartReport(
        scene_path=rel,
        block_names=sorted(set(blocks)),
        execute_block_refs=execute_refs,
        clickable2d_refs=clickable_refs,
    )


def scan_scenes(scenes_root: Path) -> list[SceneFlowchartReport]:
    if not scenes_root.is_dir():
        return []
    reports: list[SceneFlowchartReport] = []
    for scene_path in sorted(scenes_root.glob(_SCENE_GLOB)):
        if scene_path.is_file():
            reports.append(scan_scene_file(scene_path))
    return reports


def summarize(reports: list[SceneFlowchartReport]) -> dict[str, int]:
    with_blocks = [r for r in reports if r.block_count > 0]
    return {
        "scene_files": len(reports),
        "scenes_with_named_blocks": len(with_blocks),
        "total_unique_block_names": len(
            {name for r in with_blocks for name in r.block_names}
        ),
        "execute_block_ref_total": sum(r.execute_block_refs for r in reports),
        "clickable2d_ref_total": sum(r.clickable2d_refs for r in reports),
    }

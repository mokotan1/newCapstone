from __future__ import annotations

from pathlib import Path

from fungus_deletion.inventory import scan_scene_file, summarize


def test_scan_scene_file_counts_execute_block(tmp_path: Path) -> None:
    scene = tmp_path / "Sample.unity"
    scene.write_text(
        "Flowchart:\n  blockName: Start\nExecuteBlock\nClickable2D\n",
        encoding="utf-8",
    )
    report = scan_scene_file(scene)
    assert "Start" in report.block_names
    assert report.execute_block_refs >= 1
    assert report.clickable2d_refs >= 1


def test_summarize_empty() -> None:
    assert summarize([])["scene_files"] == 0

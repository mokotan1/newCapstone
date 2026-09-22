from __future__ import annotations

from pathlib import Path

from fungus_deletion.scanner import scan_csharp_file


def test_scan_csharp_detects_fungus(tmp_path: Path) -> None:
    cs = tmp_path / "Bridge.cs"
    cs.write_text(
        "using Fungus;\nclass X { void M(Flowchart f) { f.ExecuteBlock(\"a\"); } }\n",
        encoding="utf-8",
    )
    report = scan_csharp_file(cs)
    assert report.uses_fungus_namespace
    assert report.execute_block_hits >= 1
    assert report.flowchart_hits >= 1

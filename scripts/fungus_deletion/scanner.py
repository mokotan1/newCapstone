"""Scan C# under disputatio for Fungus API usage (read-only audit helper)."""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from pathlib import Path


_FUNGUS_USING = re.compile(r"^\s*using\s+Fungus\s*;", re.MULTILINE)
_EXECUTE_BLOCK = re.compile(r"\bExecuteBlock\b")
_FLOWCHART = re.compile(r"\bFlowchart\b")
_VARIABLE = re.compile(r"\bVariable\b")


@dataclass
class CsharpFungusReport:
    file_path: str
    uses_fungus_namespace: bool = False
    execute_block_hits: int = 0
    flowchart_hits: int = 0
    variable_hits: int = 0
    lines: list[str] = field(default_factory=list)

    @property
    def total_hits(self) -> int:
        return self.execute_block_hits + self.flowchart_hits + self.variable_hits


def scan_csharp_file(path: Path) -> CsharpFungusReport:
    text = path.read_text(encoding="utf-8", errors="replace")
    report = CsharpFungusReport(
        file_path=path.as_posix(),
        uses_fungus_namespace=bool(_FUNGUS_USING.search(text)),
        execute_block_hits=len(_EXECUTE_BLOCK.findall(text)),
        flowchart_hits=len(_FLOWCHART.findall(text)),
        variable_hits=len(_VARIABLE.findall(text)),
    )
    if report.total_hits > 0 or report.uses_fungus_namespace:
        for index, line in enumerate(text.splitlines(), start=1):
            if "Fungus" in line or "ExecuteBlock" in line or "Flowchart" in line:
                report.lines.append(f"{index}:{line.strip()}")
    return report


def scan_csharp_tree(assets_script_root: Path) -> list[CsharpFungusReport]:
    if not assets_script_root.is_dir():
        return []
    reports: list[CsharpFungusReport] = []
    for cs_path in sorted(assets_script_root.rglob("*.cs")):
        if "/Editor/" in cs_path.as_posix() and "Tests" not in cs_path.as_posix():
            continue
        report = scan_csharp_file(cs_path)
        if report.uses_fungus_namespace or report.total_hits > 0:
            reports.append(report)
    return reports


def top_files_by_hits(reports: list[CsharpFungusReport], limit: int = 20) -> list[CsharpFungusReport]:
    return sorted(reports, key=lambda item: item.total_hits, reverse=True)[:limit]

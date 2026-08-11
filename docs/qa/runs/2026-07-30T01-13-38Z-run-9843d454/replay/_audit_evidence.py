#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path

replay = Path("docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/replay")
root = Path("docs/qa/runs")

print("=== replay gateway-runs ===")
for d in sorted((replay / "gateway-runs").glob("20260730T*")):
    jpath = d / "journal.jsonl"
    mpath = d / "manifest.json"
    lines = jpath.read_text(encoding="utf-8-sig").strip().splitlines() if jpath.exists() else []
    m = json.loads(mpath.read_text(encoding="utf-8-sig")) if mpath.exists() else {}
    print(
        f"=== {d.name} Verdict={m.get('Verdict')} Status={m.get('Status')} "
        f"assert={m.get('AssertionPassedCount')} shots={m.get('ScreenshotCount')}"
    )
    for ln in lines[:15]:
        e = json.loads(ln)
        print(f"  {e.get('Type')}:{e.get('CommandId')}:{(e.get('Message') or '')[:90]}")

print("\n=== scenario-results summary ===")
for p in sorted((replay / "scenario-results").glob("*.json")):
    data = json.loads(p.read_text(encoding="utf-8"))
    print(
        f"{data.get('scenarioId')}: verdict={data.get('verdict')} "
        f"copied={data.get('copiedEvidencePath')} "
        f"assert={data.get('assertionPassedCount')} shots={data.get('screenshotCount')}"
    )

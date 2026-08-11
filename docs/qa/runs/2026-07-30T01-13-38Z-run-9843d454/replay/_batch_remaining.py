#!/usr/bin/env python3
"""Batch remaining replay scenarios with isolation rules."""
from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(".").resolve()
RUNNER = ROOT / "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/replay/_run_one.py"
RESULTS = ROOT / "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/replay/scenario-results"


def load_verdict(sid: str) -> str | None:
    p = RESULTS / f"{sid}.json"
    if not p.exists():
        return None
    data = json.loads(p.read_text(encoding="utf-8"))
    return data.get("verdict")


def run(sid: str) -> str:
    print(f"==== RUN {sid} ====", flush=True)
    r = subprocess.run([sys.executable, str(RUNNER), sid], cwd=str(ROOT))
    v = load_verdict(sid)
    print(f"==== DONE {sid} verdict={v} exit={r.returncode} ====", flush=True)
    return v or "Unknown"


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    # Remaining Phase B smokes
    smokes = [
        "room.study-room.smoke",
        "room.child-room.smoke",
        "room.wife-room.smoke",
        "room.bed-room.smoke",
    ]
    smoke_pass: dict[str, bool] = {
        "room.hall.smoke": load_verdict("room.hall.smoke") == "Pass",
        "room.kitchen.smoke": load_verdict("room.kitchen.smoke") == "Pass",
        "room.maid-room.smoke": load_verdict("room.maid-room.smoke") == "Pass",
    }

    for sid in smokes:
        v = run(sid)
        smoke_pass[sid] = v == "Pass"

    # Phase C kitchen pack
    kitchen_ok = smoke_pass.get("room.kitchen.smoke", False)
    for sid in [
        "room.kitchen.happy-path",
        "room.kitchen.guard.wrong-item",
        "room.kitchen.guard.reentry",
    ]:
        if not kitchen_ok:
            print(f"SKIP {sid} kitchen smoke failed", flush=True)
            continue
        run(sid)

    # Phase D partial happys
    for sid, need in [
        ("room.hall.happy-path", "room.hall.smoke"),
        ("room.maid-room.happy-path", "room.maid-room.smoke"),
        ("room.study-room.happy-path", "room.study-room.smoke"),
        ("room.child-room.happy-path", "room.child-room.smoke"),
        ("room.wife-room.happy-path", "room.wife-room.smoke"),
        ("room.bed-room.happy-path", "room.bed-room.smoke"),
    ]:
        if not smoke_pass.get(need, False):
            print(f"SKIP {sid} blocked by {need}", flush=True)
            continue
        run(sid)

    print("BATCH COMPLETE", flush=True)
    print(json.dumps(smoke_pass, indent=2), flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

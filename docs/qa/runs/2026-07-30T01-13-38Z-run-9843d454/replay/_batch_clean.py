#!/usr/bin/env python3
"""Clean re-run of planned scenarios with correct attribution."""
from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(".").resolve()
RUNNER = ROOT / "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/replay/_run_one.py"
RESULTS = ROOT / "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/replay/scenario-results"
STATE = ROOT / "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/replay/batch-state.json"


def load_result(sid: str) -> dict:
    p = RESULTS / f"{sid}.json"
    if not p.exists():
        return {}
    return json.loads(p.read_text(encoding="utf-8"))


def is_pass(sid: str) -> bool:
    r = load_result(sid)
    return r.get("verdict") == "Pass" and r.get("evidenceMatch") == "matched"


def run(sid: str) -> dict:
    print(f"\n######## RUN {sid} ########", flush=True)
    subprocess.run([sys.executable, str(RUNNER), sid], cwd=str(ROOT))
    r = load_result(sid)
    print(
        f"######## DONE {sid} verdict={r.get('verdict')} match={r.get('evidenceMatch')} "
        f"A={r.get('assertionPassedCount')} S={r.get('screenshotCount')} ########",
        flush=True,
    )
    STATE.write_text(
        json.dumps({"last": sid, "result": {
            "verdict": r.get("verdict"),
            "match": r.get("evidenceMatch"),
            "assertions": r.get("assertionPassedCount"),
            "screenshots": r.get("screenshotCount"),
        }}, indent=2),
        encoding="utf-8",
    )
    return r


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    # Invalidate previous mis-attributed results by renaming
    archive = RESULTS / "_misattributed"
    archive.mkdir(parents=True, exist_ok=True)
    for name in [
        "room.hall.smoke.json",
        "room.kitchen.smoke.json",
        "room.maid-room.smoke.json",
        "room.study-room.smoke.json",
        "room.child-room.smoke.json",
        "room.wife-room.smoke.json",
        "room.bed-room.smoke.json",
    ]:
        src = RESULTS / name
        if src.exists():
            dst = archive / name
            if dst.exists():
                dst.unlink()
            src.rename(dst)

    order = [
        # Phase B
        "room.hall.smoke",
        "room.kitchen.smoke",
        "room.maid-room.smoke",
        "room.study-room.smoke",
        "room.child-room.smoke",
        "room.wife-room.smoke",
        "room.bed-room.smoke",
    ]
    smoke_ok: dict[str, bool] = {}
    for sid in order:
        r = run(sid)
        smoke_ok[sid] = r.get("verdict") == "Pass" and r.get("evidenceMatch") == "matched"
        # one retry on no-matching-evidence
        if not smoke_ok[sid] and r.get("evidenceMatch") == "no-matching-evidence":
            print(f"RETRY once {sid}", flush=True)
            r = run(sid)
            smoke_ok[sid] = r.get("verdict") == "Pass" and r.get("evidenceMatch") == "matched"

    # Phase C
    if smoke_ok.get("room.kitchen.smoke"):
        for sid in [
            "room.kitchen.happy-path",
            "room.kitchen.guard.wrong-item",
            "room.kitchen.guard.reentry",
        ]:
            run(sid)
    else:
        print("SKIP Phase C — kitchen smoke not PASS", flush=True)

    # Phase D
    for sid, need in [
        ("room.hall.happy-path", "room.hall.smoke"),
        ("room.maid-room.happy-path", "room.maid-room.smoke"),
        ("room.study-room.happy-path", "room.study-room.smoke"),
        ("room.child-room.happy-path", "room.child-room.smoke"),
        ("room.wife-room.happy-path", "room.wife-room.smoke"),
        ("room.bed-room.happy-path", "room.bed-room.smoke"),
    ]:
        if smoke_ok.get(need):
            run(sid)
        else:
            print(f"SKIP {sid} blocked by {need}", flush=True)

    print("CLEAN BATCH COMPLETE", flush=True)
    print(json.dumps(smoke_ok, indent=2), flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

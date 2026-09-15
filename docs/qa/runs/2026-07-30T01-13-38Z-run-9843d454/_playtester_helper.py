#!/usr/bin/env python3
"""QA playtester helper — invoke unity-cli without PowerShell quote mangling."""
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

UNITY = Path(os.environ["LOCALAPPDATA"]) / "unity-cli" / "unity-cli.exe"
PROJ = Path("disputatio").resolve()
EVIDENCE = Path("docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454").resolve()
OWNER = "qa-20260730-playtester"


def unity(*args: str, timeout: int = 120) -> subprocess.CompletedProcess[str]:
    cmd = [str(UNITY), "--project", str(PROJ), *args]
    return subprocess.run(
        cmd,
        capture_output=True,
        encoding="utf-8",
        errors="replace",
        timeout=timeout,
    )


def print_result(label: str, r: subprocess.CompletedProcess[str]) -> None:
    print(f"=== {label} code={r.returncode} ===")
    out = (r.stdout or "").strip()
    if out:
        print(out)
    err = (r.stderr or "").strip()
    if err and "Update available" not in err:
        print("STDERR:", err)


def parse_json_stdout(stdout: str) -> dict:
    text = stdout.split("Update available")[0].strip()
    return json.loads(text) if text else {}


def qa_status() -> dict:
    r = unity("qa_status")
    print_result("qa_status", r)
    return parse_json_stdout(r.stdout or "")


def copy_gateway_evidence(evid_path: str) -> str | None:
    if not evid_path or not os.path.isdir(evid_path):
        return None
    dst = EVIDENCE / "gateway-runs" / Path(evid_path).name
    if dst.exists():
        shutil.rmtree(dst)
    shutil.copytree(evid_path, dst)
    print(f"COPIED {evid_path} -> {dst}")
    return str(dst)


def recover_and_clear() -> None:
    r = unity("qa_recover", "--params", json.dumps({"owner_id": OWNER}))
    print_result("qa_recover", r)
    r = unity("console", "--clear")
    print_result("console --clear", r)


def run_scenario(scenario_id: str, timeout_ms: int = 180000) -> dict:
    recover_and_clear()
    params = json.dumps({"scenario_id": scenario_id, "timeout_ms": timeout_ms})
    print(f"START {scenario_id}")
    r = unity("qa_run", "--params", params, "--timeout", "200000", timeout=210)
    print_result(f"qa_run {scenario_id}", r)
    outcome: dict = {}
    try:
        outcome = parse_json_stdout(r.stdout or "")
    except json.JSONDecodeError:
        outcome = {"raw": r.stdout, "parseError": True}

    for _ in range(90):
        st = qa_status()
        if not st.get("isScenarioRunning"):
            break
        time.sleep(2)

    cerr = unity("console", "--type", "error", "--lines", "40")
    print_result("console errors", cerr)
    console_errors = []
    try:
        console_errors = json.loads(
            (cerr.stdout or "").split("Update available")[0].strip() or "[]"
        )
    except json.JSONDecodeError:
        console_errors = [{"raw": cerr.stdout}]

    evid = st.get("evidenceRunDirectoryPath", "") if "st" in dir() else ""
    st = qa_status()
    evid = st.get("evidenceRunDirectoryPath", "")
    copied = copy_gateway_evidence(evid)

    manifest = None
    report = None
    if evid and os.path.isdir(evid):
        mpath = Path(evid) / "manifest.json"
        rpath = Path(evid) / "report.md"
        if mpath.exists():
            manifest = json.loads(mpath.read_text(encoding="utf-8-sig"))
        if rpath.exists():
            report = rpath.read_text(encoding="utf-8-sig")

    return {
        "scenarioId": scenario_id,
        "cliOutcome": outcome,
        "consoleErrors": console_errors,
        "gatewayEvidencePath": evid,
        "copiedEvidencePath": copied,
        "manifest": manifest,
        "reportExcerpt": (report or "")[:800],
        "qaStatusAfter": st,
    }


def editor_state() -> None:
    code = (
        "var playing = UnityEditor.EditorApplication.isPlaying; "
        "var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; "
        "return playing + \"|\" + scene;"
    )
    r = unity("exec", code)
    print_result("editor_state", r)


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser()
    ap.add_argument("action", choices=["status", "recover", "run", "editor"])
    ap.add_argument("--scenario", default="")
    args = ap.parse_args()
    if args.action == "status":
        qa_status()
    elif args.action == "recover":
        recover_and_clear()
    elif args.action == "editor":
        editor_state()
    elif args.action == "run":
        if not args.scenario:
            print("scenario required", file=sys.stderr)
            return 2
        result = run_scenario(args.scenario)
        out = EVIDENCE / "scenario-results" / f"{args.scenario}.json"
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
        print("WROTE", out)
        print(json.dumps({"outcomeCode": result.get("cliOutcome", {}).get("outcomeCode"),
                          "message": result.get("cliOutcome", {}).get("outcomeMessage"),
                          "verdict": (result.get("manifest") or {}).get("Verdict")}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

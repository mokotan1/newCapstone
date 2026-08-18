#!/usr/bin/env python3
"""Reliable single-scenario runner with scenario-id evidence matching."""
from __future__ import annotations

import ctypes
import json
import os
import shutil
import subprocess
import sys
import time
from ctypes import wintypes
from pathlib import Path
from typing import Any

UNITY = Path(os.environ["LOCALAPPDATA"]) / "unity-cli" / "unity-cli.exe"
PROJ = Path("disputatio").resolve()
REPO = Path(".").resolve()
REPLAY = Path("docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/replay").resolve()
GATEWAY_DST = REPLAY / "gateway-runs"
OWNER = "qa-20260730-replay"
user32 = ctypes.windll.user32
VK_CONTROL = 0x11
VK_P = 0x50
KEYEVENTF_KEYUP = 0x0002

# Expected step id fragments per scenario for attribution
EXPECTED_STEPS: dict[str, list[str]] = {
    "kitchen.faucet-key": ["faucet", "before-faucet", "kitchen.faucet"],
    "room.hall.smoke": ["probe-route", "hall.nav.probe"],
    "room.kitchen.smoke": ["apply-before-faucet", "probe-readiness", "kitchen.faucet.probe"],
    "room.maid-room.smoke": ["probe-food", "maidroom.food.probe"],
    "room.study-room.smoke": ["apply-before-placement", "probe-mirror", "studyroom.mirror"],
    "room.child-room.smoke": ["probe-seals", "childroom.seals.probe"],
    "room.wife-room.smoke": ["probe-wallclock", "wiferoom.wallclock.probe"],
    "room.bed-room.smoke": ["probe-book", "bedroom.book.probe"],
    "room.kitchen.happy-path": ["kitchen", "faucet", "happy"],
    "room.kitchen.guard.wrong-item": ["wrong", "guard"],
    "room.kitchen.guard.reentry": ["reentry", "guard"],
    "room.hall.happy-path": ["hall", "happy"],
    "room.maid-room.happy-path": ["maid", "happy"],
    "room.study-room.happy-path": ["study", "happy", "mirror"],
    "room.child-room.happy-path": ["child", "happy"],
    "room.wife-room.happy-path": ["wife", "happy"],
    "room.bed-room.happy-path": ["bed", "happy"],
}


def unity(*args: str, timeout: int = 120) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [str(UNITY), "--ignore-version-mismatch", "--project", str(PROJ), *args],
        capture_output=True,
        encoding="utf-8",
        errors="replace",
        timeout=timeout,
        cwd=str(REPO),
    )


def strip(s: str) -> str:
    return (s or "").split("Update available")[0].strip()


def parse(s: str) -> Any:
    t = strip(s)
    if not t:
        return {}
    try:
        return json.loads(t)
    except json.JSONDecodeError:
        return {"raw": t}


def log(msg: str) -> None:
    print(msg, flush=True)


def ctrl_p_stop() -> None:
    hwnds: list[tuple[int, str]] = []

    @ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)
    def callback(hwnd: int, _lp: int) -> bool:
        if not user32.IsWindowVisible(hwnd):
            return True
        length = user32.GetWindowTextLengthW(hwnd)
        if length <= 0:
            return True
        buf = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buf, length + 1)
        title = buf.value
        if "Unity" in title:
            hwnds.append((hwnd, title))
        return True

    user32.EnumWindows(callback, 0)
    target = None
    for h, t in hwnds:
        tl = t.lower()
        if ("disputatio" in tl or "newcapstone" in tl) and "status" not in tl and "hub" not in tl:
            target = h
            log(f"Ctrl+P target: {t}")
            break
    if target is None:
        return
    user32.ShowWindow(target, 9)
    user32.SetForegroundWindow(target)
    time.sleep(0.3)
    user32.keybd_event(VK_CONTROL, 0, 0, 0)
    user32.keybd_event(VK_P, 0, 0, 0)
    time.sleep(0.05)
    user32.keybd_event(VK_P, 0, KEYEVENTF_KEYUP, 0)
    user32.keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0)
    time.sleep(3)


def wait_ready(max_s: int = 120) -> None:
    deadline = time.time() + max_s
    while time.time() < deadline:
        try:
            line = strip(unity("status", timeout=20).stdout).splitlines()[0]
            log(f"  status: {line}")
            if "playing" in line:
                try:
                    unity("editor", "stop", timeout=20)
                except Exception:
                    pass
                ctrl_p_stop()
                time.sleep(2)
                continue
            if line.startswith("Unity: ready"):
                st = parse(unity("qa_status", timeout=40).stdout)
                if isinstance(st, dict) and "isScenarioRunning" in st:
                    if st.get("isQaProfileActive") and not st.get("isScenarioRunning"):
                        recover()
                    return
        except Exception as e:
            log(f"  wait_ready err: {e}")
        time.sleep(3)


def recover() -> dict[str, Any]:
    try:
        r = unity(
            "qa_recover",
            "--params",
            json.dumps({"owner_id": OWNER}),
            timeout=90,
        )
        return parse(r.stdout or "")
    except Exception as e:
        return {"error": str(e)}


def safe_rmtree(path: Path) -> None:
    if not path.exists():
        return
    for _ in range(3):
        try:
            shutil.rmtree(path)
            return
        except Exception:
            time.sleep(1)
    # last resort: rename aside
    aside = path.with_name(path.name + f".old-{int(time.time())}")
    try:
        path.rename(aside)
    except Exception as e:
        log(f"rmtree failed: {e}")


def copy_evidence(evid: str) -> str | None:
    if not evid or not os.path.isdir(evid):
        return None
    GATEWAY_DST.mkdir(parents=True, exist_ok=True)
    dst = GATEWAY_DST / Path(evid).name
    safe_rmtree(dst)
    shutil.copytree(evid, dst)
    parent = Path("docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/gateway-runs") / Path(evid).name
    parent.parent.mkdir(parents=True, exist_ok=True)
    safe_rmtree(parent)
    try:
        shutil.copytree(evid, parent)
    except Exception as e:
        log(f"parent copy warn: {e}")
    rel = str(dst.relative_to(REPO)).replace("\\", "/")
    log(f"COPIED -> {rel}")
    return rel


def journal_blob(evid_dir: Path) -> str:
    j = evid_dir / "journal.jsonl"
    if not j.exists():
        return ""
    return j.read_text(encoding="utf-8-sig")


def evidence_matches(scenario_id: str, evid_dir: Path) -> bool:
    blob = journal_blob(evid_dir).lower()
    if not blob:
        return False
    # Strong match: scenario id appears in journal
    if scenario_id.lower() in blob:
        return True
    # Step fragment match — require at least one expected fragment AND reject known wrong classic ids
    wrong_markers = {
        "room.hall.smoke": ["click-faucet", "click-kitchen-entry", "click-food-tray"],
        "room.kitchen.smoke": ["click-kitchen-entry", "hall.kitchen-quest", "click-food-tray"],
        "room.maid-room.smoke": ["click-faucet", "click-kitchen-entry", "hall.kitchen-quest"],
    }
    for bad in wrong_markers.get(scenario_id, []):
        if bad in blob:
            return False
    frags = EXPECTED_STEPS.get(scenario_id, [scenario_id.split(".")[-1]])
    hits = sum(1 for f in frags if f.lower() in blob)
    # For smokes, also accept DeveloperQa step ids from the JSON if present in journal
    return hits >= 1 and "runbegan" in blob.replace(" ", "")


def list_candidate_runs(after_ts: float) -> list[Path]:
    root = Path("docs/qa/runs")
    out: list[Path] = []
    for p in root.iterdir():
        if not p.is_dir():
            continue
        if "9843d454" in p.name:
            continue
        if not p.name.startswith("20260730T") and not p.name.startswith("2026"):
            continue
        if p.stat().st_mtime < after_ts - 2:
            continue
        if (p / "manifest.json").exists() or (p / "journal.jsonl").exists():
            out.append(p)
    out.sort(key=lambda x: x.stat().st_mtime)
    return out


def read_manifest(evid: Path) -> dict[str, Any] | None:
    mpath = evid / "manifest.json"
    if not mpath.exists():
        return None
    return json.loads(mpath.read_text(encoding="utf-8-sig"))


def is_finalized(manifest: dict[str, Any] | None) -> bool:
    if not manifest:
        return False
    if manifest.get("Verdict") or manifest.get("verdict"):
        return True
    if manifest.get("EndedAtUtc"):
        return True
    status = str(manifest.get("Status") or "")
    return status.lower() in ("completed", "failed", "blocked", "passed", "pass")


def run_scenario(scenario_id: str, timeout_ms: int = 180000) -> dict[str, Any]:
    REPLAY.mkdir(parents=True, exist_ok=True)
    log(f"=== run {scenario_id} ===")
    wait_ready(120)
    log(f"recover: {recover()}")
    wait_ready(60)
    try:
        unity("console", "--clear", timeout=30)
    except Exception:
        pass

    started = time.time()
    known_before = {p.name for p in list_candidate_runs(0)}
    params = json.dumps(
        {"scenario_id": scenario_id, "timeout_ms": timeout_ms, "owner_id": OWNER}
    )
    cli_outcome: dict[str, Any] = {}
    try:
        log(f"qa_run fire {scenario_id}")
        # Longer send window — Play Mode bootstrap can stall health briefly
        r = unity("qa_run", "--params", params, "--timeout", "240", timeout=250)
        cli_outcome = parse(r.stdout or "")
        if not cli_outcome:
            cli_outcome = {
                "returncode": r.returncode,
                "stderr": strip(r.stderr or "")[:500],
                "stdout_raw": strip(r.stdout or "")[:500],
            }
        log(f"qa_run returned: {json.dumps(cli_outcome)[:700]}")
    except subprocess.TimeoutExpired:
        cli_outcome = {"error": "qa_run CLI timeout — polling"}
        log("qa_run CLI timed out; polling")
    except Exception as e:
        cli_outcome = {"error": str(e)}
        log(f"qa_run exception: {e}")

    deadline = time.time() + (timeout_ms / 1000.0) + 120
    matched: Path | None = None
    last_st: dict[str, Any] = {}

    while time.time() < deadline:
        try:
            last_st = parse(unity("qa_status", timeout=30).stdout)
            if not isinstance(last_st, dict):
                last_st = {}
        except Exception as e:
            last_st = {"error": str(e)}

        # Prefer status evidence path if matches
        evid_path = str(last_st.get("evidenceRunDirectoryPath") or "")
        if evid_path and os.path.isdir(evid_path):
            ep = Path(evid_path)
            if evidence_matches(scenario_id, ep):
                m = read_manifest(ep)
                if is_finalized(m) and not last_st.get("isScenarioRunning"):
                    matched = ep
                    log(f"  matched status evid {ep.name} verdict={(m or {}).get('Verdict')}")
                    break
                log(f"  status evid {ep.name} match but not final status={ (m or {}).get('Status')} running={last_st.get('isScenarioRunning')}")

        # Scan new run dirs
        for p in list_candidate_runs(started):
            if p.name in known_before and p.stat().st_mtime < started:
                continue
            if not evidence_matches(scenario_id, p):
                continue
            m = read_manifest(p)
            if is_finalized(m):
                matched = p
                log(f"  matched scanned evid {p.name} verdict={(m or {}).get('Verdict')}")
                break
            else:
                log(f"  candidate {p.name} matches but InProgress")
        if matched:
            break

        if last_st.get("isScenarioRunning"):
            log(f"  still running {last_st.get('activeScenarioId')}")
        time.sleep(4)

    # Force-stop if still playing
    try:
        line = strip(unity("status", timeout=20).stdout).splitlines()[0]
        if "playing" in line:
            log("Post-run playing — stop")
            try:
                unity("editor", "stop", timeout=30)
            except Exception:
                pass
            ctrl_p_stop()
    except Exception:
        ctrl_p_stop()

    # One more scan after stop
    if matched is None:
        time.sleep(2)
        for p in list_candidate_runs(started):
            if evidence_matches(scenario_id, p) and is_finalized(read_manifest(p)):
                matched = p
                break

    evid = str(matched) if matched else ""
    copied = copy_evidence(evid) if evid else None
    manifest = read_manifest(matched) if matched else None
    report = ""
    journal_tail = ""
    if matched:
        rpath = matched / "report.md"
        jpath = matched / "journal.jsonl"
        if rpath.exists():
            report = rpath.read_text(encoding="utf-8-sig")
        if jpath.exists():
            journal_tail = "\n".join(jpath.read_text(encoding="utf-8-sig").strip().splitlines()[-25:])

    try:
        errs = parse(unity("console", "--type", "error", "--lines", "40", timeout=30).stdout)
    except Exception:
        errs = []

    rec = recover()
    wait_ready(60)

    if matched is None:
        status_note = "no-matching-evidence"
        verdict = None
    else:
        status_note = "matched"
        verdict = (manifest or {}).get("Verdict")

    result = {
        "scenarioId": scenario_id,
        "cliOutcome": cli_outcome,
        "qaStatusFinal": last_st,
        "recoverAfter": rec,
        "consoleErrors": errs if isinstance(errs, list) else [errs],
        "gatewayEvidencePath": evid,
        "copiedEvidencePath": copied,
        "manifest": manifest,
        "reportExcerpt": report[:1500],
        "journalTail": journal_tail,
        "verdict": verdict,
        "verdictReason": (manifest or {}).get("VerdictReason"),
        "assertionPassedCount": (manifest or {}).get("AssertionPassedCount"),
        "screenshotCount": (manifest or {}).get("ScreenshotCount"),
        "evidenceMatch": status_note,
        "attributionNote": "Evidence accepted only if journal matches scenario id/steps",
    }
    out = REPLAY / "scenario-results" / f"{scenario_id}.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    log(f"WROTE {out}")
    log(
        json.dumps(
            {
                "verdict": verdict,
                "match": status_note,
                "assertions": result.get("assertionPassedCount"),
                "screenshots": result.get("screenshotCount"),
                "copied": copied,
                "reason": str(result.get("verdictReason") or "")[:200],
            },
            indent=2,
            ensure_ascii=False,
        )
    )
    return result


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sid = sys.argv[1] if len(sys.argv) > 1 else "room.hall.smoke"
    run_scenario(sid)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

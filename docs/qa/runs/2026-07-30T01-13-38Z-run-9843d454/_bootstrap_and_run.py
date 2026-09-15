#!/usr/bin/env python3
"""Bootstrap scenario scene into Play Mode, then qa_run."""
from __future__ import annotations

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

SCENE_HINTS = {
    "Kitchen": "Kitchen",
    "Hall_playerble": "Hall_playerble",
    "MaidRoom": "MaidRoom",
    "TutorRoom": "TutorRoom",
    "MainMenuScene": "MainMenuScene",
    "StudyRoom": "StudyRoom",
}


def unity(*args: str, timeout: int = 120) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [str(UNITY), "--project", str(PROJ), *args],
        capture_output=True,
        encoding="utf-8",
        errors="replace",
        timeout=timeout,
    )


def show(label: str, r: subprocess.CompletedProcess[str]) -> None:
    print(f"=== {label} code={r.returncode} ===")
    out = (r.stdout or "").strip()
    if out:
        print(out[:3000])


def parse_json(stdout: str):
    text = (stdout or "").split("Update available")[0].strip()
    return json.loads(text) if text else {}


def open_scene(scene_name: str) -> None:
    # C# finds the scene asset by name and opens it in the Editor.
    code = (
        "var guids = UnityEditor.AssetDatabase.FindAssets(\""
        + scene_name
        + " t:Scene\");"
        "string found = null;"
        "foreach (var g in guids) {"
        "  var p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);"
        "  if (System.IO.Path.GetFileNameWithoutExtension(p) == \""
        + scene_name
        + "\") { found = p; break; }"
        "}"
        "if (found == null) return \"NOT_FOUND\";"
        "UnityEditor.SceneManagement.EditorSceneManager.OpenScene("
        "found, UnityEditor.SceneManagement.OpenSceneMode.Single);"
        "return found;"
    )
    r = unity("exec", code, timeout=60)
    show(f"open_scene {scene_name}", r)


def editor_state() -> str:
    code = (
        "return UnityEditor.EditorApplication.isPlaying.ToString()"
        " + \"|\" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;"
    )
    r = unity("exec", code, timeout=30)
    show("editor_state", r)
    return (r.stdout or "").strip().split("\n")[0] if r.stdout else ""


def ensure_play(scene_name: str) -> None:
    # Stop play mode if already playing in wrong scene.
    st = editor_state()
    if "True|" in st and scene_name not in st:
        show("editor stop", unity("editor", "stop", timeout=60))
        time.sleep(2)
    open_scene(scene_name)
    st = editor_state()
    if "True|" not in st:
        show("editor play", unity("editor", "play", "--wait", timeout=120))
        time.sleep(2)
    editor_state()


def copy_evidence(evid: str) -> str | None:
    if not evid or not os.path.isdir(evid):
        return None
    dst = EVIDENCE / "gateway-runs" / Path(evid).name
    if dst.exists():
        shutil.rmtree(dst)
    shutil.copytree(evid, dst)
    print("COPIED", evid, "->", dst)
    return str(dst)


def run_scenario(scenario_id: str, scene_name: str, bootstrap: bool = True) -> dict:
    show("qa_recover", unity("qa_recover", "--params", json.dumps({"owner_id": OWNER})))
    show("console clear", unity("console", "--clear"))

    if bootstrap:
        ensure_play(scene_name)

    params = json.dumps({"scenario_id": scenario_id, "timeout_ms": 180000})
    print("START", scenario_id, "scene", scene_name, "bootstrap", bootstrap)
    r = unity("qa_run", "--params", params, "--timeout", "200000", timeout=210)
    show(f"qa_run {scenario_id}", r)
    try:
        outcome = parse_json(r.stdout or "")
    except json.JSONDecodeError:
        outcome = {"raw": r.stdout}

    st = {}
    for _ in range(90):
        sr = unity("qa_status")
        st = parse_json(sr.stdout or "")
        if not st.get("isScenarioRunning"):
            break
        time.sleep(2)

    cerr = unity("console", "--type", "error", "--lines", "40")
    show("console errors", cerr)
    try:
        console_errors = json.loads(
            (cerr.stdout or "").split("Update available")[0].strip() or "[]"
        )
    except json.JSONDecodeError:
        console_errors = []

    evid = st.get("evidenceRunDirectoryPath", "")
    copied = copy_evidence(evid)
    manifest = None
    if evid and Path(evid, "manifest.json").exists():
        manifest = json.loads(Path(evid, "manifest.json").read_text(encoding="utf-8"))

    result = {
        "scenarioId": scenario_id,
        "scene": scene_name,
        "bootstrapPlayMode": bootstrap,
        "cliOutcome": outcome,
        "consoleErrors": console_errors,
        "gatewayEvidencePath": evid,
        "copiedEvidencePath": copied,
        "manifest": manifest,
        "qaStatusAfter": st,
    }
    out = EVIDENCE / "scenario-results" / f"{scenario_id}.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    print("WROTE", out)
    return result


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if len(sys.argv) < 3:
        print("usage: bootstrap_and_run.py <scenario_id> <scene_name> [--no-bootstrap]")
        return 2
    sid = sys.argv[1]
    scene = sys.argv[2]
    bootstrap = "--no-bootstrap" not in sys.argv
    result = run_scenario(sid, scene, bootstrap=bootstrap)
    print(
        json.dumps(
            {
                "outcomeCode": (result.get("cliOutcome") or {}).get("outcomeCode"),
                "message": (result.get("cliOutcome") or {}).get("outcomeMessage"),
                "verdict": (result.get("manifest") or {}).get("Verdict"),
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

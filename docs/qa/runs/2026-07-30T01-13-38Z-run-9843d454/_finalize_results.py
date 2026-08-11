#!/usr/bin/env python3
"""Finalize playtester results + smoke summary + recover."""
from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path

UNITY = Path(os.environ["LOCALAPPDATA"]) / "unity-cli" / "unity-cli.exe"
PROJ = Path("disputatio").resolve()
EVIDENCE = Path("docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454").resolve()
OWNER = "qa-20260730-playtester"


def unity(*args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [str(UNITY), "--project", str(PROJ), *args],
        capture_output=True,
        encoding="utf-8",
        errors="replace",
        timeout=60,
    )


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    scenarios = [
        {
            "scenarioId": "hall.kitchen-quest",
            "substitutesFor": ["room.hall.smoke"],
            "status": "fail",
            "attempts": 2,
            "retries": 1,
            "outcomeCode": "Failed",
            "outcomeMessage": (
                "Step 'click-kitchen-entry' failed: CorridorEntranceController not found "
                "in the active scene. This adapter only works while Hall_playerble is the "
                "active Play Mode scene."
            ),
            "failureSignature": "missing-playmode-scene:Hall_playerble/CorridorEntranceController",
            "gatewayEvidence": [
                "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/gateway-runs/20260730T012105Z-run-ba7ae7b54d024b89b7702639166c80f4",
                "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/gateway-runs/20260730T012202Z-run-cdc5cef1ede0423086f1e91a0d304bdf",
            ],
            "screenshotsPresent": True,
            "assertionsPassed": 0,
            "consoleErrors": [],
            "note": (
                "Same signature twice; not an environmental flake. Gateway records Blocked "
                "(partial evidence). qa_run does not bootstrap Play Mode scene."
            ),
        },
        {
            "scenarioId": "kitchen.faucet-key",
            "substitutesFor": ["room.kitchen.smoke", "room.kitchen.happy-path"],
            "status": "fail",
            "attempts": 2,
            "retries": 1,
            "outcomeCode": "Failed",
            "outcomeMessage": (
                "Failed to apply preset 'before-faucet': KitchenPuzzleState not found in "
                "the active scene. This preset only works while the Kitchen scene is the "
                "active Play Mode scene."
            ),
            "failureSignature": "missing-playmode-scene:Kitchen/KitchenPuzzleState",
            "gatewayEvidence": [
                "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/gateway-runs/20260730T012226Z-run-b54a8f2e3f77432596bf6ce5bade1460",
                "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/gateway-runs/20260730T012457Z-run-47c5da7958554fa280583411bee08211",
            ],
            "screenshotsPresent": True,
            "assertionsPassed": 0,
            "consoleErrors": [],
            "note": "Same signature twice. Preset apply requires Kitchen Play Mode.",
        },
        {
            "scenarioId": "maidroom.food-effect",
            "substitutesFor": ["room.maid-room.smoke", "room.maid-room.happy-path"],
            "status": "fail",
            "attempts": 2,
            "retries": 1,
            "outcomeCode": "Failed",
            "outcomeMessage": (
                "Step 'click-food-tray' failed: MaidRoomPuzzleController not found in the "
                "active scene. This adapter only works while the MaidRoom scene is the "
                "active Play Mode scene."
            ),
            "failureSignature": "missing-playmode-scene:MaidRoom/MaidRoomPuzzleController",
            "gatewayEvidence": [
                "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/gateway-runs/20260730T012512Z-run-7983610111fe48f4b521087e487923d9",
                "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/gateway-runs/20260730T012524Z-run-48c5c5bfd63f48c5bd2bc6c1d3c28dcf",
            ],
            "screenshotsPresent": True,
            "assertionsPassed": 0,
            "consoleErrors": [],
            "note": "Same signature twice.",
        },
        {
            "scenarioId": "kitchen.cheshire-repeat",
            "substitutesFor": [],
            "status": "fail",
            "attempts": 2,
            "retries": 1,
            "outcomeCode": "Failed",
            "outcomeMessage": (
                "Failed to apply preset 'before-parret': KitchenPuzzleState not found in "
                "the active scene. This preset only works while the Kitchen scene is the "
                "active Play Mode scene."
            ),
            "failureSignature": "missing-playmode-scene:Kitchen/KitchenPuzzleState",
            "gatewayEvidence": [
                "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/gateway-runs/20260730T012536Z-run-7afa6b81c743410a9c70d4b50d4efcc1",
                "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/gateway-runs/20260730T012555Z-run-0e9ab14cb068414f80e3374d5c04f428",
            ],
            "screenshotsPresent": True,
            "assertionsPassed": 0,
            "consoleErrors": [],
            "note": "Same Play Mode bootstrap gap as kitchen.faucet-key.",
        },
        {
            "scenarioId": "mainmenu.new-game-reset",
            "substitutesFor": [],
            "status": "fail",
            "attempts": 2,
            "retries": 1,
            "outcomeCode": "Failed",
            "outcomeMessage": (
                "Step 'click-start' failed: MainMenu instance not found in the active "
                "scene. Requires MainMenuScene to be the active Play Mode scene when "
                "qa_run executes."
            ),
            "failureSignature": "missing-playmode-scene:MainMenuScene/MainMenu",
            "gatewayEvidence": [
                "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/gateway-runs/20260730T012606Z-run-49edf9cecc3a48bab83e23134a6ad53b",
                "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/gateway-runs/20260730T012620Z-run-71edbbee22d84b88bbcbe8bfede90bb1",
            ],
            "screenshotsPresent": True,
            "assertionsPassed": 0,
            "consoleErrors": [],
            "note": "Same Play Mode bootstrap gap.",
        },
        {
            "scenarioId": "tutorroom.cheshire-quiz",
            "substitutesFor": [],
            "status": "pass",
            "attempts": 1,
            "retries": 0,
            "outcomeCode": "Passed",
            "outcomeMessage": "All steps passed.",
            "failureSignature": None,
            "gatewayEvidence": [
                "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/gateway-runs/20260730T012633Z-run-3089a0ef04f0494082a30bef43c4b7bc",
            ],
            "screenshotsPresent": True,
            "screenshotFiles": [
                "screenshots/0006-capture-quiz-input-evidence.png",
                "screenshots/0008-auto-finalize-safety-net.png",
            ],
            "assertionsPassed": 3,
            "assertionsFailed": 0,
            "consoleErrors": [],
            "gatewayVerdict": "Pass",
            "note": (
                "Editor active scene was already TutorRoom. Gateway Pass with 3 "
                "assertions + 2 screenshots + console. Hand to qa-evidence-reviewer."
            ),
        },
    ]

    for room in ["study-room", "child-room", "wife-room", "bed-room"]:
        for kind in ["smoke", "happy-path"]:
            scenarios.append(
                {
                    "scenarioId": f"room.{room}.{kind}",
                    "substitutesFor": [],
                    "status": "blocked",
                    "attempts": 0,
                    "retries": 0,
                    "outcomeCode": None,
                    "outcomeMessage": (
                        "QA_INFRA_DEFECT: room.* packs are not loadable via qa_run "
                        "(schema mismatch). No legacy substitute available for this room."
                    ),
                    "failureSignature": "QA_INFRA_DEFECT:room-pack-schema-mismatch",
                    "gatewayEvidence": [],
                    "screenshotsPresent": False,
                    "assertionsPassed": 0,
                    "consoleErrors": [],
                    "classification": "QA_INFRA_DEFECT",
                }
            )

    findings = [
        {
            "id": "F1",
            "severity": "blocker",
            "classification": "QA_INFRA_DEFECT",
            "summary": (
                "qa_run does not open scenario.scene / enter Play Mode before adapter "
                "preset/interaction; 5/6 legacy scenarios fail with missing scene controller."
            ),
            "evidence": "Editor state before runs: False|TutorRoom (Edit Mode).",
        },
        {
            "id": "F2",
            "severity": "blocker",
            "classification": "QA_INFRA_DEFECT",
            "summary": (
                "room.* scenario packs schema-mismatch — not loadable via qa_run; "
                "study/child/wife/bed have no legacy substitutes."
            ),
            "evidence": "inventory-findings.json runtimeRunnableViaQaList=false",
        },
        {
            "id": "F3",
            "severity": "info",
            "classification": "OBSERVATION",
            "summary": (
                "tutorroom.cheshire-quiz Passed with assertions+screenshots while Editor "
                "scene was already TutorRoom; may be environment-dependent."
            ),
            "evidence": "gateway-runs/20260730T012633Z-run-3089a0ef04f0494082a30bef43c4b7bc",
        },
    ]

    # Final recover
    r = unity("qa_recover", "--params", json.dumps({"owner_id": OWNER}))
    recover_out = (r.stdout or "").strip()
    print("FINAL qa_recover:", recover_out[:500])
    st = unity("qa_status")
    status_text = (st.stdout or "").split("Update available")[0].strip()
    try:
        status_json = json.loads(status_text)
    except json.JSONDecodeError:
        status_json = {"raw": status_text}
    print("FINAL qa_status:", json.dumps(status_json, indent=2)[:800])

    payload = {
        "taskId": "qa-20260730-autorun",
        "ownerId": OWNER,
        "evidenceRoot": "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454",
        "status": "fail",
        "preflight": {
            "unityStatus": "ready",
            "qaRecover": "Success",
            "consoleCleared": True,
            "editorStateBeforeRuns": "False|TutorRoom",
        },
        "lease": {
            "ownerId": OWNER,
            "note": (
                "qa_recover used between runs; isQaProfileActive remained false after "
                "each completed run (runner finally restores profile)."
            ),
        },
        "profileRestore": {
            "confirmed": True,
            "method": "qa_recover after each scenario + final qa_recover",
            "finalQaRecoverStdout": recover_out[:1000],
            "isQaProfileActiveAfter": status_json.get("isQaProfileActive", False),
            "isScenarioRunningAfter": status_json.get("isScenarioRunning", False),
        },
        "scenarios": scenarios,
        "findings": findings,
        "gatewayRunsCopiedUnder": "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/gateway-runs/",
        "artifacts": {
            "playtesterResults": "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/playtester-results.json",
            "smokeSummary": "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/smoke-summary.md",
        },
    }

    out = EVIDENCE / "playtester-results.json"
    out.write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")
    print("WROTE", out)

    summary = """# Smoke Summary — 2026-07-30 autorun playtester

**Task:** `qa-20260730-autorun`  
**Evidence root:** `docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454`  
**Overall status:** **fail**

## Preflight

- Unity: ready (6000.0.36f1)
- `qa_recover` owner `qa-20260730-playtester`: Success
- Console cleared
- Editor before runs: Edit Mode, scene `TutorRoom`

## Legacy substitute runs (qa_run)

| Scenario | Role | Attempts | Status | Notes |
|---|---|---:|---|---|
| `hall.kitchen-quest` | room.hall.smoke | 2 | fail | Hall_playerble not in Play Mode |
| `kitchen.faucet-key` | kitchen smoke/happy | 2 | fail | Kitchen preset needs Play Mode |
| `maidroom.food-effect` | maid smoke/happy | 2 | fail | MaidRoom controller missing |
| `kitchen.cheshire-repeat` | extra valid | 2 | fail | same Kitchen Play Mode gap |
| `mainmenu.new-game-reset` | extra valid | 2 | fail | MainMenuScene not in Play Mode |
| `tutorroom.cheshire-quiz` | extra valid | 1 | pass* | 3 assertions + 2 screenshots; gateway Pass |

\\* Playtester recorded gateway Pass evidence; final certification is for `qa-evidence-reviewer`.

## Rooms without legacy substitutes

| Room | Status | Reason |
|---|---|---|
| study / child / wife / bed (smoke + happy) | **blocked** | `QA_INFRA_DEFECT` — room packs not loadable via `qa_run` |

## Systemic finding

`QaScenarioRunner` applies presets/interactions without opening `scenario.scene` or entering Play Mode. Adapters then fail with “X not found in the active scene… requires … Play Mode scene.” Classification: **QA_INFRA_DEFECT**.

## Profile / lease

Final `qa_recover` executed. Profile inactive; no scenario running.

## Evidence paths

Gateway run copies under `gateway-runs/`. Full per-scenario detail in `playtester-results.json`.
"""
    summary_path = EVIDENCE / "smoke-summary.md"
    summary_path.write_text(summary, encoding="utf-8")
    print("WROTE", summary_path)

    envelope = {
        "taskId": "qa-20260730-autorun",
        "evidenceRoot": "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454",
        "status": "fail",
        "findings": findings,
        "leaseOwnerId": OWNER,
        "profileRestoreConfirmed": True,
        "manifestPaths": [
            "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/playtester-results.json",
            "docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/smoke-summary.md",
        ],
        "scenarioSummary": {
            "pass": 1,
            "fail": 5,
            "blocked": 8,
        },
    }
    env_path = EVIDENCE / "playtester-envelope.json"
    env_path.write_text(json.dumps(envelope, indent=2, ensure_ascii=False), encoding="utf-8")
    print("ENVELOPE")
    print(json.dumps(envelope, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

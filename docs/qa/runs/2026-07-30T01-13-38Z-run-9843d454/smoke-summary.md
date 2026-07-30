# Smoke Summary — 2026-07-30 autorun playtester

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

\* Playtester recorded gateway Pass evidence; final certification is for `qa-evidence-reviewer`.

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

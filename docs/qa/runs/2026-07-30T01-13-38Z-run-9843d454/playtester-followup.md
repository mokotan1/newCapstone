# Follow-up from playtester (2026-07-30)

Source: [qa-playtester](d785b733-f2df-4714-845e-732c21fd30d0) → `playtester-results.json`

## Verdict of first runtime pass

| Metric | Count |
|---|---:|
| PASS (gateway) | 1 (`tutorroom.cheshire-quiz`) |
| FAIL | 5 (legacy substitutes) |
| BLOCKED | 8 (study/child/wife/bed smoke+happy) |

## Root cause (Phase 1)

Adapters require the declared scene **in Play Mode**. `qa_run` applied presets/interactions while the Editor stayed in **Edit Mode** on `TutorRoom`. Failure signature (repeated, not flake):

`missing-playmode-scene:<Scene>/<Controller>`

Classification: **QA_INFRA_DEFECT** (P0 progression blocker for autorun).

Separate blocker (already inventoried): room packs not listed/runnable via `qa_list`/`qa_run`.

## Actions taken after completion

1. Interrupted/resumed infra fix agent to add **Play Mode + scene bootstrap** before first step.
2. Started `qa-evidence-reviewer` on the single gateway Pass (`tutorroom.cheshire-quiz`).
3. Re-playtest deferred until infra compile + bootstrap land.

## Do not claim

- Do not promote the 5 fails to product defects.
- Do not certify `tutorroom` PASS until evidence-reviewer confirms (possible environment dependence).

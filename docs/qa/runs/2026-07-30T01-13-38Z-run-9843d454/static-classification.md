# Static classification (Task 2)

Run: `2026-07-30T01-13-38Z-run-9843d454`  
Plan: `docs/superpowers/plans/2026-07-30-qa-autorun-execution.md`

## Unit tests

| Check | Result |
|---|---|
| `python -m pytest scripts/qa/tests -q` | **55 passed** (1 pytest cache warning on Windows) |

## Coverage audit (`--report-only`)

| Field | Value | Classification |
|---|---|---|
| `ok` | `false` | Not a gameplay failure by itself |
| `missingManifests` | all `basement.*` (6) | `NOT_IMPLEMENTED` / coverage gap |
| `missingScenarioFiles` | `kitchen:guard-wrong-input.json` | `SPEC_MISMATCH` (packs use `guard-wrong-item.json`) |
| `undeclaredCapabilities` | none | — |
| `unmappedBuildScenes` | none | — |

## Gateway discovery (pre-fix)

| Observation | Classification |
|---|---|
| `qa_list` returns **0** `room.*` ids | `QA_INFRA_DEFECT` |
| Nested `Rooms/**` packs use DeveloperQa `{family,name,targetId}` while classic validator expects `{command,timeoutMs,scene}` | `QA_INFRA_DEFECT` |
| Invalid TextAssets listed by filename (`smoke`) not JSON `id` | `QA_INFRA_DEFECT` |
| Legacy runnable: `hall.kitchen-quest`, `kitchen.faucet-key`, `maidroom.food-effect`, `kitchen.cheshire-repeat`, `mainmenu.new-game-reset`, `tutorroom.cheshire-quiz` | usable smoke/happy substitutes |
| study / child / wife / bed lack classic `qa_run` substitutes | blocked until room packs load |

## Preflight (Task 1)

| Check | Result |
|---|---|
| Branch | `feature/tutorroom-quiz-input-qa-seam` @ `b836b519` |
| Unity | `ready` 6000.0.36f1 |
| Cursor CLI | present; `-p`, `--output-format`, `--workspace`, `--prompt` available |
| Dirty paths | protected (fonts, unrelated `.meta`, prior run artifacts) — not autorun-owned |
| Gameplay gate | OPEN |

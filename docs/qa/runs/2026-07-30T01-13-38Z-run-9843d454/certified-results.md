# Certified results (partial)

| Scenario | Playtester | Evidence reviewer | Certifiable? |
|---|---|---|---|
| `tutorroom.cheshire-quiz` | gateway Pass | **pass** ([review](evidence-review-tutorroom.md)) | Yes, **with caveat**: environment-dependent (Editor already on TutorRoom; no Play Mode bootstrap) |
| `hall.kitchen-quest` | fail | — | No — `QA_INFRA_DEFECT` Play Mode bootstrap |
| `kitchen.faucet-key` | fail | — | No — same |
| `maidroom.food-effect` | fail | — | No — same |
| `kitchen.cheshire-repeat` | fail | — | No — same |
| `mainmenu.new-game-reset` | fail | — | No — same |
| study/child/wife/bed room.* | blocked | — | No — room pack schema + bootstrap |

## Policy for daily report

- Count `tutorroom.cheshire-quiz` as **PASS (env-dependent)**, not as proof that autorun bootstraps scenes.
- Do not escalate the five fails to PRODUCT_DEFECT until bootstrap is fixed and re-run.
- Re-playtest gated on infra agent completing room bridge + Play Mode bootstrap.

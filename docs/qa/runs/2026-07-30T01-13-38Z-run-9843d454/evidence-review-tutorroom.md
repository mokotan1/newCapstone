# Evidence review — `tutorroom.cheshire-quiz`

**Task:** `qa-20260730-autorun`  
**Evidence root:** `docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454`  
**Gateway run:** `gateway-runs/20260730T012633Z-run-3089a0ef04f0494082a30bef43c4b7bc`  
**Reviewed status:** **pass**

## Verdict

Claimed gateway/playtester **Pass** is **supported**. Reproduction path, three assertion records, required screenshots on disk, and a clean console finalize are all present under the evidence root. No hard-reject conditions applied.

## Checklist

| Criterion | Result |
|---|---|
| Scenario ID + git revision | Pass — `tutorroom.cheshire-quiz`; head `b836b519…` in `baseline.json` |
| Reproduction path | Pass — `click-quiz-input` → `Success` / API interaction |
| State assertions recorded | Pass — 3/0 (`inputUnlocked`, `AiConnectionState=Idle`, `noNewConsoleError`) |
| Required screenshots | Pass — `0006-…png`, `0008-…png` present and event-referenced |
| Console delta | Pass — `ConsoleErrorCount=0`; no exception lines |
| Profile / lease | Pass — `qa_recover` / profile inactive noted in `playtester-results.json` |
| API vs RealInput | N/A — API-layer only |

## Environment dependency (noted, not overturned)

Preflight Editor state was **Edit Mode + TutorRoom** (`False|TutorRoom`). Other scenarios in this autorun failed with `missing-playmode-scene` because `qa_run` does not open `scenario.scene` / enter Play Mode. This Pass is **environment-dependent** on the Editor already being on TutorRoom; it does **not** certify self-bootstrapping from an arbitrary scene.

## Observations

- Screenshots are byte-identical; TutorRoom frame with UI is visible; quiz-input panel is not visually distinct beyond state assertions.
- No unredacted secrets in reviewed artifacts.

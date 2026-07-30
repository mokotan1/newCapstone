# QA Infra Fix Notes — 2026-07-30T01-13-38Z-run-9843d454

## Summary

Two P0 `QA_INFRA_DEFECT`s were fixed so room packs and classic scenarios become runnable via `qa_list` / `qa_run`:

1. **Room-pack schema bridge** — DeveloperQa-style JSON under `Resources/QA/Scenarios/Rooms/**` validates/lists/runs by JSON `id` (e.g. `room.kitchen.smoke`).
2. **Play Mode / scene bootstrap** — classic `QaScenarioRunner` (and DeveloperQa gateway path when `scene` is set) enters Play Mode and loads `scenario.scene` before presets/interactions; restores Play Mode in `finally` when it entered it.

Coverage audit naming: required guard file is now `guard-wrong-item.json` (alias `guard-wrong-input.json` still accepted).

## Algorithm

### A. Dual-schema detection (Option 1)

- `QaCommandGateway.LooksLikeDeveloperQaScenario`: treat as DeveloperQa when (`roomId` + `tier`) **or** first step has `family`/`name`.
- Skip non-scenario Resources names: `manifest`, `catalog`, `exclusions`.
- List/run: DeveloperQa → `DeveloperQaScenarioValidator` + `DeveloperQaService.scenario.run`; classic → existing validator/runner.
- `DeveloperQaScenarioValidator` accepts `roomId` when `scene` is blank.
- `DeveloperQaScenarioRunner.ResolveScenarioPath` recursively finds `Rooms/**/*.json` by JSON `id`; gateway can also pass `scenario_json`.

### B. Play Mode bootstrap (P0 follow-up)

- New `IQaPlayModeSceneBootstrap` (DIP) injected into `QaScenarioRunner` / `QaCommandGateway`.
- Before `ApplyPreset` / first interaction: `EnsureReadyAsync(scenario.Scene, timeout)`.
- Editor impl `EditorQaPlayModeSceneBootstrap`:
  1. Resolve scene path from Build Settings / AssetDatabase.
  2. If not playing: `OpenScene` → temporarily `DisableDomainReload` → `EditorApplication.isPlaying = true` → wait.
  3. If active scene ≠ declared: `LoadSceneInPlayMode` → wait (timeout → **BLOCKED**).
  4. `RestoreIfOwned` in runner/gateway `finally` stops Play Mode only if this run entered it; restores Enter Play Mode options.
- Outcome code `QaScenarioRunOutcomeCode.Blocked` for environment failures (message includes `BLOCKED:`).

## Files changed

| Area | Path |
|------|------|
| Gateway dual schema | `disputatio/Assets/mokotan/.../QA/UI/QaCommandGateway.cs` |
| UI asmdef | `.../QA/UI/Godlotto.QA.UI.asmdef` |
| DeveloperQa roomId | `.../QA/Developer/DeveloperQaScenarioDefinition.cs` |
| DeveloperQa validator | `.../QA/Developer/DeveloperQaScenarioValidator.cs` |
| Nested path resolve + `scenario_json` | `.../QA/Developer/DeveloperQaScenarioRunner.cs` |
| Runner bootstrap hook | `.../QA/Scenarios/QaScenarioRunner.cs` |
| Bootstrap contract | `.../QA/Scenes/IQaPlayModeSceneBootstrap.cs` |
| Editor bootstrap | `disputatio/Assets/Editor/QA/EditorQaPlayModeSceneBootstrap.cs` |
| Installer wiring | `disputatio/Assets/Editor/QA/QaUnityCliTools.cs` |
| Player gateway wiring | `.../DevMode/DeveloperModeController.cs` |
| PlayMode tests asmdef | `disputatio/Assets/Tests/PlayMode/PlayModeTests.asmdef` |
| Coverage audit | `scripts/qa/rooms/coverage_audit.py` |
| Tests | `RoomPackScenarioGatewayTests.cs`, `QaScenarioRunnerPlayModeBootstrapTests.cs`, `test_coverage_audit.py` |

## Verification commands / results

```text
python -m pytest scripts/qa/tests -q
→ 56 passed

python -m scripts.qa.rooms.coverage_audit --report-only
→ missingScenarioFiles: []  (no kitchen:guard-wrong-input.json)

.\scripts\unity-cli.cmd --project disputatio editor refresh --compile
→ Compilation complete

.\scripts\unity-cli.cmd --project disputatio console --type error,warning --lines 80
→ []

.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter RoomPackScenarioGatewayTests
→ 4/4 passed

.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter QaScenarioRunnerPlayModeBootstrapTests
→ 2/2 passed

.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter QaScenarioRunnerEvidenceCaptureTests
→ 3/3 passed

.\scripts\unity-cli.cmd --project disputatio qa_list
→ room.kitchen.smoke / room.hall.smoke listed (JSON ids, not TextAsset stems)
```

Long `qa_run` suite deferred to next qa-playtester. Optional single smoke not executed in this pass (bootstrap + list green; leave Play Mode mutations for playtester).
## Remaining blockers

- Empty-step / NOT_IMPLEMENTED room stubs (`hall.left`, `prison`, `utility-room`, …) still list as invalid — by design until packs gain steps.
- Transition JSONs are not classic/DeveloperQa scenarios — still invalid via classic validator.
- Full `qa_run` re-playtest left for next `qa-playtester` (do not batch-run here). Optional single smoke: `kitchen.faucet-key` after green compile+list.
- Player builds still need Play Mode scene already active unless a player-side bootstrap is added later (Editor bootstrap is Editor-only).

## Do not commit

Unrelated dirty paths (fonts, unrelated `.meta`, `.pytest_tmp`) left untouched. No commit per user request.

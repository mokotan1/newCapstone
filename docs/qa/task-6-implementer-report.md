# Task 6 Implementer Report — preset.before-placement + place-bookmark

**Status:** `DONE_WITH_CONCERNS`  
**Branch:** `feature/self-extending-qa-autorun`  
**Worktree:** `.worktrees/qa-autorun-dev-mode`  
**Date:** 2026-07-27

## What shipped

| File | Role |
|------|------|
| `StudyRoomDiaryMirrorPuzzleController.cs` | QA seam `TrySnapToConfiguredSolutionAndEvaluateForQa` — snap to configured pose then normal Evaluate → SuccessRouter (no ForceSolve, no player-path change) |
| `StudyRoomQaAdapter.cs` | Registers + handles `studyroom.mirror.preset.before-placement` and `studyroom.mirror.place-bookmark`; `ApplyPreset("before-placement")`; place uses real `FilterCardBookDropZone.OnDrop` |
| `StudyRoomQaAdapterTests.cs` | Seven capability IDs; before-placement EnvBlocked/Ok; place EnvBlocked without inventory/controller; place does not ForceSolve |
| `StudyRoomDiaryMirrorPuzzleControllerTests.cs` | Seam uses SuccessRouter; no-active-mirror returns false |
| `DeveloperQaPanelBridge.cs` | Already calls `RegisterCapabilities` (no change required) |

Capability IDs added: `preset.before-placement`, `place-bookmark`. Force-solve not used as PASS path.

## TDD / verification

1. **Tests first** — EditMode cases for register / before-placement / place EnvBlocked / no-ForceSolve before handlers.
2. **Unity EditMode** — blocked: `unity-cli --project disputatio status` → `no Unity instance found for project: disputatio`.
3. **Fallback** — `dotnet run --project scripts/CSharpSyntaxChecker/CSharpSyntaxChecker.csproj` on Interaction, SceneAdapters, Developer/UI EditMode dirs → exit 0.
4. **Commits** (not pushed):
   - `fix(studyroom): expose placement seam for QA without changing player path`
   - `feat(qa): add studyroom.mirror place and before-placement preset`

## Concerns / follow-up

- Unity EditMode filters **not executed** — reopen Unity on this worktree and run:
  ```powershell
  .\scripts\unity-cli.cmd --project disputatio editor refresh --compile
  .\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter StudyRoomQaAdapterTests
  .\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter StudyRoomDiaryMirrorPuzzleControllerTests
  ```
- Full happy-path place (inventory + drop zone + solved assert) needs PlayMode / StudyRoom scene — EditMode covers registration + safe EnvironmentBlocked / no-ForceSolve only.
- PlayMode `StudyRoomMirrorCapabilityPlayModeTests.cs` from the plan was not added (Unity unavailable).

## Commits

- Product seam SHA: `9a0b4e6f` — `fix(studyroom): expose placement seam for QA without changing player path`
- QA SHA: `3db05b4f` — `feat(qa): add studyroom.mirror place and before-placement preset`
- Push: not performed (per instructions)

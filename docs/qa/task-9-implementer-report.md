# Task 9 Implementer Report — StudyRoom scenario JSON + scenario.* commands

**Status:** `DONE_WITH_CONCERNS`  
**Branch:** `feature/self-extending-qa-autorun`  
**Worktree:** `.worktrees/qa-autorun-dev-mode`  
**Date:** 2026-07-27

## What shipped

| File | Role |
|------|------|
| `Resources/QA/Scenarios/studyroom-mirror-diary.json` | Design §10 StudyRoom diary-mirror steps (before-placement → grant → place → probe/capture → assert-solved → evidence → reset → repeat place → assert → evidence) |
| `DeveloperQaScenarioDefinition.cs` | DeveloperQa-command step schema (`family`/`name`/`targetId`) + status constants |
| `DeveloperQaScenarioValidator.cs` | Strict JSON validate (no nested `scenario.*`) |
| `DeveloperQaScenarioRunner.cs` | Session state machine: run / resume / cancel / status; file load by `scenario_id`/`scenario_path` |
| `DeveloperQaService.cs` | Wires `scenario.run\|resume\|cancel\|status`; keeps profile+evidence begin; `execute=false` defers steps |
| `DeveloperQaScenarioTests.cs` | Load/validate studyroom JSON; status after deferred run; cancel → `cancelled`; resume advances |

**Why not `QaScenarioRunner`:** existing schema only allows `interaction.pointer|drag|key` / `state.assert` / `evidence.*` — not StudyRoom capability IDs.

## TDD / verification

1. **Tests first** — `DeveloperQaScenarioTests` before runner/service wiring.
2. **Unity EditMode** — blocked: `unity-cli --project disputatio status` → `no Unity instance found for project: disputatio`.
3. **Fallback** — `dotnet run --project scripts/CSharpSyntaxChecker/...` on Developer + Developer EditMode tests → exit 0.
4. **Commit** — `test(qa): add studyroom-mirror-diary scenario`. Not pushed.

## Concerns / follow-up

- Unity EditMode filter **not executed** — reopen Unity on this worktree and run:
  ```powershell
  .\scripts\unity-cli.cmd --project disputatio editor refresh --compile
  .\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter DeveloperQaScenarioTests
  ```
- Full Play Mode execution of studyroom steps still needs StudyRoom + Developer Mode (`CanUse`); EditMode uses `execute=false` for status/cancel contracts.
- `scene.load` is not yet a live DeveloperQa command; scenario assumes StudyRoom is (or will be) active when steps execute.
- Hand-authored `.meta` GUIDs; Unity refresh may rewrite importer blocks.

## Commit

- Message: `test(qa): add studyroom-mirror-diary scenario`
- Scope: scenario JSON, DeveloperQa scenario runner/validator, service wiring, EditMode tests, architecture note, this report
- Push: not performed (per instructions)

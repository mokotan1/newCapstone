# Task 8 Implementer Report — CLI gateway + contract parity for DeveloperQaService

**Status:** `DONE_WITH_CONCERNS`  
**Branch:** `feature/self-extending-qa-autorun`  
**Worktree:** `.worktrees/qa-autorun-dev-mode`  
**Date:** 2026-07-27

## What shipped

| File | Role |
|------|------|
| `SceneAdapters/DeveloperQaServiceFactory.cs` | Shared factory: registers StudyRoom capabilities; optional profile + evidence |
| `SceneAdapters/DeveloperQaPanelBridge.cs` | Default service creation now goes through the factory (CLI parity wiring) |
| `Editor/QA/DeveloperQaCliBridge.cs` | Parses family/name/target/parameters → `DeveloperQaCommand`; `qa_dev_exec` CLI tool; `CreateProductionService` injects `EditorQaEvidenceRecorder` (`docs/qa/runs`); Editor installer Configure's panel+CLI |
| `DeveloperQaCliPanelParityTests.cs` | Same payloads CLI vs panel → equal `DeveloperQaResultCode`; unknown cap → equal `MissingCapabilityId`; production evidence not EnvironmentBlocked |

## TDD / verification

1. **Tests first** — `DeveloperQaCliPanelParityTests` before factory/CLI bridge.
2. **Unity EditMode** — blocked: `unity-cli --project disputatio status` → `no Unity instance found for project: disputatio`.
3. **Fallback** — `dotnet run --project scripts/CSharpSyntaxChecker/...` on SceneAdapters, Editor/QA, Developer EditMode tests → exit 0.
4. **Commit** — `feat(qa): expose DeveloperQaService to Unity CLI`. Not pushed.

## Concerns / follow-up

- Unity EditMode filter `DeveloperQaCliPanelParityTests` **not executed** — reopen Unity on this worktree and run:
  ```powershell
  .\scripts\unity-cli.cmd --project disputatio editor refresh --compile
  .\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter DeveloperQaCliPanelParityTests
  ```
- Hand-authored `.meta` GUIDs; Unity refresh may rewrite importer blocks.
- Grant/reset parity codes depend on StudyRoom runtime/dev-mode availability (same as Task 5); probe/missing-cap assertions are the stronger EditMode contracts.

## Commit

- Message: `feat(qa): expose DeveloperQaService to Unity CLI`
- Scope: factory, CLI bridge + installer, panel factory wiring, parity tests, this report
- Push: not performed (per instructions)

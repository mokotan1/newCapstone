# Wave 1 Task 2 Implementer Report — MainMenu start capabilities

**Status:** `DONE_WITH_CONCERNS`  
**Branch:** `feature/self-extending-qa-autorun`  
**Worktree:** `.worktrees/qa-autorun-dev-mode`  
**Date:** 2026-07-27

## What shipped

MainMenu start DeveloperQa capabilities on `MainMenuQaAdapter` (Kitchen-style thin wraps):

| Capability ID | Kind | Delegates to |
|---|---|---|
| `mainmenu.start.click` | Interaction | `TryClick(mainmenu.start-button)` |
| `mainmenu.start.probe` | Probe | `CaptureSnapshot` |
| `mainmenu.start.capture` | Probe | `CaptureSnapshot` |
| `mainmenu.start.assert-invoked` | Assertion | `CaptureSnapshot`; `mainMenuFound != True` → `AssertionFailed` |

Missing MainMenu: click → `EnvironmentBlocked` (not fake Ok).

**Tests:** `MainMenuQaCapabilityTests` (list ids, MissingCapability describe, click without scene → EnvironmentBlocked, assert when `mainMenuFound!=True` → AssertionFailed).

## TDD / verification

1. **RED** — Wrote `MainMenuQaCapabilityTests` first (plan Step 1 + assert rule).
2. **Unity EditMode** — blocked: `unity-cli --project disputatio status` → `no Unity instance found for project: disputatio`.
3. **Fallback** — `dotnet run --project scripts/CSharpSyntaxChecker/CSharpSyntaxChecker.csproj` on `SceneAdapters` + `EditMode/QA/Developer` → exit 0.
4. **Commit** — `feat(qa): add mainmenu.start DeveloperQa capabilities` (adapter + tests + `.meta` + this report). Not pushed. No `bin/`.

## Concerns / follow-up

- Unity EditMode filter `MainMenuQaCapabilityTests` **not executed** — reopen Unity on this worktree and run:
  ```powershell
  .\scripts\unity-cli.cmd --project disputatio editor refresh --compile
  .\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter MainMenuQaCapabilityTests
  ```
- Wave 1 Task 3+ still needs factory wiring + scenario JSON.

## Commit

- Message: `feat(qa): add mainmenu.start DeveloperQa capabilities`
- Scope: `MainMenuQaAdapter.cs`, `MainMenuQaCapabilityTests.cs` (+ `.meta`), `docs/qa/wave1-task2-implementer-report.md`
- Push: not performed (per instructions)

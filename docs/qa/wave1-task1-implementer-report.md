# Wave 1 Task 1 Implementer Report — Kitchen faucet capabilities

**Status:** `DONE_WITH_CONCERNS`  
**Branch:** `feature/self-extending-qa-autorun`  
**Worktree:** `.worktrees/qa-autorun-dev-mode`  
**Date:** 2026-07-27

## What shipped

Kitchen faucet DeveloperQa capabilities on `KitchenQaAdapter` (StudyRoom-style thin wraps):

| Capability ID | Kind | Delegates to |
|---|---|---|
| `kitchen.faucet.preset.before-faucet` | Preset | `ApplyPreset(before-faucet)` |
| `kitchen.faucet.reset` | Recovery | `ApplyPreset(before-faucet)` (`SetFaucetClicked(false)`) |
| `kitchen.faucet.click` | Interaction | `TryClick(kitchen.sink.faucet)` |
| `kitchen.faucet.probe` | Probe | `CaptureSnapshot` |
| `kitchen.faucet.capture` | Probe | `CaptureSnapshot` |
| `kitchen.faucet.assert-clicked` | Assertion | `CaptureSnapshot`; `faucetClicked != True` → `AssertionFailed` |

No ForceSolve. Missing Kitchen scene: click/preset → `EnvironmentBlocked` (not fake Ok).

**Tests:** `KitchenQaCapabilityTests` (list ids, MissingCapability describe, click without scene → EnvironmentBlocked).

## TDD / verification

1. **RED** — Wrote `KitchenQaCapabilityTests` first (plan Step 1).
2. **Unity EditMode** — blocked: `unity-cli --project disputatio status` → `no Unity instance found for project: disputatio`.
3. **Fallback** — `dotnet run --project scripts/CSharpSyntaxChecker/CSharpSyntaxChecker.csproj` on `SceneAdapters` + `EditMode/QA/Developer` → exit 0.
4. **Commit** — `feat(qa): add kitchen.faucet DeveloperQa capabilities` (adapter + tests + `.meta` only). Not pushed. No `bin/`.

## Concerns / follow-up

- Unity EditMode filter `KitchenQaCapabilityTests` **not executed** — reopen Unity on this worktree and run:
  ```powershell
  .\scripts\unity-cli.cmd --project disputatio editor refresh --compile
  .\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter KitchenQaCapabilityTests
  ```
- Wave 1 Task 2+ still needs MainMenu capabilities + factory wiring + scenario JSON.

## Commit

- SHA: `6a26b5af01bbb5e171c760687fa4715c1bf9ebe1`
- Message: `feat(qa): add kitchen.faucet DeveloperQa capabilities`
- Scope: `KitchenQaAdapter.cs`, `KitchenQaCapabilityTests.cs` (+ `.meta`)
- Push: not performed (per instructions)

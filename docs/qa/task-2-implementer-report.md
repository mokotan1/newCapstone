# Task 2 Implementer Report — Capability Registry and MissingCapability

**Status:** `DONE_WITH_CONCERNS`  
**Branch:** `feature/self-extending-qa-autorun`  
**Worktree:** `.worktrees/qa-autorun-dev-mode`  
**Date:** 2026-07-27

## What shipped

| File | Role |
|------|------|
| `DeveloperQaCapabilityRegistry.cs` | Register / List / TryGet; monotonic `Version` (`"0"` → `"1"` on first Register) |
| `DeveloperQaService.cs` | Parameterless ctor creates empty registry; optional inject; wires `capability.list` / `describe`, unknown `interaction.invoke` → `MissingCapability` |
| `DeveloperQaCapabilityRegistryTests.cs` | Register+Version, unknown invoke, describe known/unknown |

`MissingCapability` includes `MissingCapabilityId`, non-empty `CheckpointId` (`Guid` N-format), and `Data["current_capabilities"]` (comma-separated ids or empty). Describe known → Ok + `scene_id` / `input_schema` / schemas in `Data`.

## TDD / verification

1. **Tests first** — `DeveloperQaCapabilityRegistryTests` (4 cases) written before registry/service wiring.
2. **Unity EditMode** — blocked: `unity-cli --project disputatio status` → `no Unity instance found for project: disputatio`.
3. **Fallback** — `dotnet run --project scripts/CSharpSyntaxChecker/CSharpSyntaxChecker.csproj` on Developer production + test paths → exit 0.
4. **Commit** — `feat(qa): add capability registry and MissingCapability results`. Not pushed.

## Concerns / follow-up

- Unity EditMode filters `DeveloperQaCapabilityRegistryTests` and `DeveloperQaServiceTests` **not executed** — reopen Unity on this worktree and run:
  ```powershell
  .\scripts\unity-cli.cmd --project disputatio editor refresh --compile
  .\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter DeveloperQaCapabilityRegistryTests
  .\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter DeveloperQaServiceTests
  ```
- Known `interaction.invoke` targets without adapters still return `UnsupportedCommand` (adapter tasks).
- Hand-authored `.meta` GUIDs; Unity refresh may rewrite importer blocks.

## Commit

- Message: `feat(qa): add capability registry and MissingCapability results`
- Scope: registry + service + EditMode tests + this report
- Push: not performed (per instructions)

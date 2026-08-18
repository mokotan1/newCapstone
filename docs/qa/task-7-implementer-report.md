# Task 7 Implementer Report — Evidence capture under docs/qa/runs

**Status:** `DONE_WITH_CONCERNS`  
**Branch:** `feature/self-extending-qa-autorun`  
**Worktree:** `.worktrees/qa-autorun-dev-mode`  
**Date:** 2026-07-27

## What shipped

| File | Role |
|------|------|
| `DevelopmentQaEvidenceRecorder.cs` | BeginRun creates `patches/`, stub `console.log`/`report.md`/`manifest.json`; dual-writes `events.jsonl` + `journal.jsonl` |
| `DeveloperQaService.cs` | Injects `IQaEvidenceRecorder`; `evidence.capture` + `scenario.run` begin/append into run dirs (not capability dispatch) |
| `Godlotto.QA.Developer.asmdef` | References `Godlotto.QA.Evidence` |
| `DeveloperQaEvidenceTests.cs` | Temp-root layout + null-recorder + no-target evidence.capture |
| `QaEvidenceRecorderTests.cs` | Asserts new BeginRun layout artifacts |

Layout:

```
<runsRoot>/<UTC>-run-<id>/
  manifest.json  journal.jsonl  report.md  console.log
  screenshots/   patches/
```

## TDD / verification

1. **Tests first** — `DeveloperQaEvidenceTests` before service/recorder wiring.
2. **Unity EditMode** — blocked: `unity-cli --project disputatio status` → `no Unity instance found for project: disputatio`.
3. **Fallback** — `dotnet run --project scripts/CSharpSyntaxChecker/...` on Developer/Evidence/EditMode QA paths → exit 0.
4. **Commit** — `feat(qa): write DeveloperQa evidence into run directories`. Not pushed.

## Concerns / follow-up

- Unity EditMode filter **not executed** — reopen Unity on this worktree and run:
  ```powershell
  .\scripts\unity-cli.cmd --project disputatio editor refresh --compile
  .\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter DeveloperQaEvidenceTests
  .\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter QaEvidenceRecorderTests
  ```
- Default `DeveloperQaService()` still has null evidence recorder; Editor/CLI wiring (Task 8) should inject `DevelopmentQaEvidenceRecorder` rooted at `docs/qa/runs`.
- Provisional `manifest.json` is overwritten on `Finalize`; full verdict still evidence-based via existing recorder.

## Commit

- Message: `feat(qa): write DeveloperQa evidence into run directories`
- Push: not performed (per instructions)

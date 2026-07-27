# Self-Extending QA Autorun Developer Mode Design

**Date:** 2026-07-27
**Status:** Approved direction
**Scope:** Developer Mode extension over the existing Cursor Subagent QA Driver design
**Initial acceptance target:** StudyRoom diary mirror puzzle

## 1. Purpose

Build a self-extending Developer Mode for QA autoruns. During a run, the AI must be able to detect that a required QA capability is missing, add that capability, compile and test it, and resume the interrupted scenario. When the run instead exposes a product defect, the AI may create a reproduction test, apply a minimal product fix, and rerun the scenario.

The system supports the command-line gateway and the in-game developer panel as equal clients of one shared service. It records state assertions, Console output, screenshots, commits, retries, and a final evidence-backed verdict.

This design narrows the existing `2026-07-22-cursor-subagent-qa-driver-design.md` to the self-extending Developer Mode loop. The existing QA Driver remains the execution and evidence foundation.

## 2. Fixed Decisions

- The CLI and in-game panel expose the same commands through one common service.
- Developer capabilities compile only for `UNITY_EDITOR || DEVELOPMENT_BUILD`.
- QA runs use an isolated QA profile and never mutate the normal player save.
- The AI may add QA capabilities and may fix product bugs during the autorun.
- QA-infrastructure changes and product fixes use separate commits.
- Work occurs on a dedicated `codex/` branch in an isolated worktree.
- A failed patch attempt is reverted without discarding previously validated commits.
- The first complete vertical slice covers the StudyRoom diary mirror puzzle.
- A gameplay PASS requires state assertions, relevant Console checks, screenshots, and successful regression verification.

## 3. Architecture

```text
CLI Gateway ───────────────┐
                          ├──> DeveloperQaService ──> existing QA Driver services
In-Game Developer Panel ──┘           |
                                      +── capability registry
                                      +── StudyRoom adapter
                                      +── state probes and assertions
                                      +── evidence recorder

QA Autorun Orchestrator
  |
  +── run scenario
  +── classify failure
  |     +── missing QA capability
  |     +── product defect
  |     +── environment block
  |     `── invalid scenario
  +── prepare bounded change request
  +── patch in isolated worktree
  +── compile and run focused tests
  +── commit validated change
  `── resume from last valid checkpoint
```

`DeveloperQaService` is the only supported mutation boundary. The CLI gateway and panel contain presentation and transport logic only. Scene-specific code registers capabilities through adapters instead of adding scene branches to the shared service.

The autorun orchestrator is external to the Unity runtime. It owns the repair state machine, Git operations, retry budget, and run journal. Unity owns gameplay mutation, state capture, screenshots, and Console deltas.

## 4. Common Developer QA Contract

The common service accepts typed commands and returns typed results:

```csharp
public interface IDeveloperQaService
{
    Task<DeveloperQaResult> ExecuteAsync(
        DeveloperQaCommand command,
        CancellationToken cancellationToken);

    DeveloperQaSnapshot CaptureSnapshot();
    IReadOnlyCollection<DeveloperQaCapability> ListCapabilities();
}
```

Required initial command families:

- `capability.list` and `capability.describe`
- `preset.apply`
- `scene.load` and `scene.waitReady`
- `interaction.invoke`
- `state.capture` and `state.assert`
- `evidence.capture`
- `scenario.run`, `scenario.resume`, `scenario.cancel`, and `scenario.status`

Every command uses stable scene, capability, target, preset, and assertion IDs. Screen coordinates, hierarchy paths, arbitrary reflection, and arbitrary C# execution are not stable public commands.

The CLI and panel contract tests must prove that the same command payload produces equivalent result codes and state changes through both gateways.

## 5. Capability Registry and Self-Extension

A capability describes one bounded operation or observation:

```csharp
public sealed class DeveloperQaCapability
{
    public string Id;
    public string SceneId;
    public DeveloperQaCapabilityKind Kind;
    public string InputSchema;
    public string OutputSchema;
}
```

Capability kinds are `Preset`, `Interaction`, `Probe`, `Assertion`, and `Recovery`. Each scene adapter declares its capabilities and implements only that scene's integration with existing gameplay controllers.

When a scenario requests an unknown capability, the runner returns `MissingCapability` before mutating gameplay. The result includes the missing stable ID, scene, requested input schema, current capabilities, checkpoint ID, and relevant state snapshot. The orchestrator converts this into a bounded change request.

A generated capability must:

1. live in QA/Developer Mode code unless a minimal product seam is essential;
2. reuse existing gameplay controllers and success routes rather than duplicate rules;
3. be reachable from both CLI and panel through `DeveloperQaService`;
4. include an EditMode or PlayMode test;
5. compile outside the active scene;
6. remain unavailable in release builds;
7. update capability discovery so a repeated request no longer reports `MissingCapability`.

## 6. Failure Classification

The orchestrator classifies each failure using structured evidence:

| Classification | Evidence | Allowed action |
|---|---|---|
| `MissingQaCapability` | Unknown capability, missing probe, missing preset, or unsupported scene adapter | Add the smallest QA capability and its test |
| `ProductDefect` | Capability executed, but expected product state or input behavior failed | Add a reproduction test, apply a minimal product fix, run focused and regression tests |
| `EnvironmentBlocked` | Unity unavailable, compile service unavailable, backend dependency unavailable, or corrupted external fixture | Record `BLOCKED`; do not patch product code |
| `InvalidScenario` | Unknown schema, contradictory assertion, invalid target, or impossible precondition | Fix the scenario only; do not patch product code |

The AI cannot classify an assertion failure as `MissingQaCapability` merely to bypass a product defect. Missing capability is valid only when the requested operation or observation cannot be executed or captured.

## 7. Autonomous Repair State Machine

The autorun uses explicit states:

```text
PREFLIGHT
  -> RUNNING
  -> CLASSIFYING
  -> PATCHING_QA | PATCHING_PRODUCT | BLOCKED
  -> COMPILING
  -> FOCUSED_TEST
  -> REGRESSION_TEST
  -> COMMITTING
  -> RESUMING
  -> RUNNING
  -> PASS | FAIL | BLOCKED
```

Before patching, the orchestrator records a checkpoint containing the scenario step, Git commit, active scene, QA profile ID, snapshot, Console cursor, and capability registry version.

Only one classification and one bounded patch are handled at a time. After a successful patch:

1. Unity compilation must succeed.
2. The new reproduction or capability test must pass.
3. affected existing tests must pass.
4. the original failing scenario step must be replayed.
5. the run resumes from the last valid checkpoint or, if state restoration is unsafe, from the scenario preset.

The same normalized failure signature may receive at most three patch attempts. A fourth occurrence ends the scenario as `BLOCKED` and preserves all evidence.

## 8. Git Isolation and Commit Policy

The orchestrator starts in an isolated worktree on a `codex/qa-autorun-<run-id>` branch. It refuses to begin autonomous patching if the worktree contains unowned changes.

Commit types are separated:

- `test(qa): reproduce <failure-id>` for a product reproduction test;
- `feat(qa): add <capability-id>` for a Developer Mode capability;
- `fix(<area>): resolve <failure-id>` for a product fix;
- `test(qa): add <scenario-id>` for scenario changes.

A product fix commit cannot contain QA capability implementation, and a QA capability commit cannot change product behavior. When a minimal product seam is required for observability or control, it is committed separately and must not change normal runtime behavior.

Before each patch the orchestrator records the exact base commit and owned path set. On failure it restores only those patch-owned changes to the recorded base. It never uses `git reset --hard`, never rewrites validated commits, and never touches unrelated worktrees.

## 9. Safety and Anti-Gaming Rules

The AI must not:

- delete, skip, weaken, or invert an existing assertion to obtain PASS;
- reduce timeouts without evidence or increase them repeatedly to hide a hang;
- mark a scenario PASS from unit tests alone;
- force expected state directly when validating the real user interaction path;
- suppress relevant Console exceptions or warnings;
- modify release-build symbols to expose Developer Mode;
- change normal save data or use a real player profile;
- combine multiple unrelated product fixes in one repair attempt;
- continue after three occurrences of the same normalized failure;
- publish, merge, push, or open a pull request without a separate explicit instruction.

The final report lists every generated capability, product modification, test, commit, retry, rollback, and remaining uncertainty.

## 10. StudyRoom Vertical Slice

The first scenario validates the diary mirror puzzle using existing `StudyRoomPuzzleDevTool`, `DeveloperModeController`, and `InGameDeveloperOverlay` behavior as the starting seam.

Required capabilities:

- `studyroom.mirror.preset.before-placement`
- `studyroom.mirror.grant-bookmark`
- `studyroom.mirror.place-bookmark`
- `studyroom.mirror.reset`
- `studyroom.mirror.probe`
- `studyroom.mirror.assert-solved`
- `studyroom.mirror.capture`

The scenario:

1. starts an isolated QA session;
2. loads StudyRoom and applies `before-placement`;
3. grants `BookmarkMirror`;
4. performs the real placement interaction;
5. captures placement and Fungus state;
6. asserts `DiarySolved`, key progression, target state, and input-gate release;
7. captures screenshot and Console delta;
8. resets the preset and repeats the critical interaction through the API boundary;
9. writes the verdict and restores the prior profile.

If the real placement operation or required state probe is unavailable, the run must produce `MissingQaCapability`, create the smallest missing capability, test it, commit it, and resume. If the capability works but the puzzle state is wrong, the run follows the product-defect path.

## 11. Evidence and Reporting

Each run writes an immutable directory under:

```text
docs/qa/runs/<UTC timestamp>-run-<id>/
  manifest.json
  journal.jsonl
  report.md
  console.log
  screenshots/
  patches/
```

`manifest.json` records the base commit, branch, Unity version, scenario version, capability registry version, run mode, and final commit list. `journal.jsonl` records state transitions and tool results. `patches/` records bounded change requests and validation summaries, not secret prompts or credentials.

Verdicts are `PASS`, `FAIL`, `BLOCKED`, or `NOT_RUN`. PASS requires the original scenario path, all required assertions, no new relevant Console exception, required screenshots, focused tests, and affected regression tests.

## 12. Testing Strategy

- EditMode tests for command validation, capability discovery, classification rules, retry counting, checkpoint serialization, and report aggregation.
- Contract tests proving CLI and panel parity.
- PlayMode tests for StudyRoom preset application, real placement, API interaction, state probes, screenshot checkpoints, cancellation, and profile cleanup.
- Orchestrator tests using fixture repositories for isolated branch creation, owned-path rollback, commit separation, three-attempt blocking, and resume behavior.
- Release-configuration compilation test proving Developer Mode types and entry points are unavailable.
- End-to-end vertical-slice test that intentionally starts with one missing StudyRoom capability, generates it through the repair loop, resumes, and finishes with a supported evidence verdict.

## 13. Non-Goals

- Automatically covering every game scene in the first implementation.
- Allowing multiple agents to mutate one Unity Editor concurrently.
- Replacing Unity Test Framework with gameplay scenarios.
- Treating a QA hook as proof that normal user input works.
- Automatically pushing, merging, or publishing autonomous fixes.
- General-purpose arbitrary code execution inside the game runtime.

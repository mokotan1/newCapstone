# Cursor Subagent QA Driver Design

**Date:** 2026-07-22
**Status:** Approved direction
**Scope:** Entire `disputatio` game, Editor and Development Build only

## 1. Purpose

Build a reusable Unity QA Driver that lets Cursor coordinate specialized QA subagents while preserving a human-operated developer panel. The system must support the whole game, isolate QA data from real saves, run stable API-level regression checks and real pointer/keyboard interaction checks, and produce reviewable local evidence.

The initial acceptance batch is the six gameplay defects dated 2026-07-15 or later, but the architecture must allow every scene and interaction to be added without growing one central manager.

## 2. Decisions

- Support both Cursor/CLI automation and a human QA panel.
- Compile runtime QA features only under `UNITY_EDITOR || DEVELOPMENT_BUILD`.
- Use a dedicated QA save profile; never mutate the player's normal profile.
- Use a hybrid interaction model: deterministic API interaction first, real Unity EventSystem input second.
- Store results locally as Markdown, JSON, screenshots, and Console logs.
- Support the whole game through incremental scene adapters.
- Use Cursor custom subagents for specialized analysis and reporting, but serialize all commands that mutate the running Unity Editor.

## 3. Architectural Approach

Use scene-specific C# adapters with a thin JSON scenario layer.

```text
Cursor parent agent (QA coordinator)
  |-- qa-inventory subagent -------- read-only scenario and coverage discovery
  |-- qa-scenario subagent --------- read-only scenario authoring/review
  |-- qa-playtest subagent --------- requests exclusive Unity execution lease
  |-- qa-evidence subagent --------- read-only evidence review and verdict
  |
  `-- QaRunCoordinator (single writer / execution queue)
          |
          +-- QaCommandGateway <---- Unity CLI custom tool
          |                    <---- Human developer panel
          |
          +-- QaDriverCore
          |     +-- QaProfileService
          |     +-- QaSceneRegistry
          |     +-- QaInputDriver
          |     +-- QaStateProbe
          |     `-- QaEvidenceRecorder
          |
          `-- IQaSceneAdapter
                +-- HallQaAdapter
                +-- KitchenQaAdapter
                +-- MaidRoomQaAdapter
                +-- TutorRoomQaAdapter
                `-- one adapter per supported scene
```

The Cursor agents may analyze files and existing evidence in parallel. Only `qa-playtest` may hold the Unity execution lease, and only one lease may exist. A subagent must never enter Play Mode, load a scene, change a QA profile, or send input without the lease.

## 4. Runtime Components

### 4.1 `QaDriverCore`

Owns one run at a time. It validates commands, delegates to services, applies timeouts, handles cancellation, and emits structured events. It contains no scene-specific gameplay logic.

Public contract:

```csharp
public interface IQaDriver
{
    Task<QaCommandResult> ExecuteAsync(QaCommand command, CancellationToken token);
    QaDriverSnapshot CaptureSnapshot();
}
```

### 4.2 `QaCommandGateway`

Provides one command contract for the Unity CLI tool and the human panel. Commands use stable IDs, never screen coordinates or hierarchy paths as their primary identity.

Required commands:

- `session.begin`, `session.end`, `session.abort`
- `profile.reset`, `profile.applyPreset`
- `scene.load`, `scene.waitReady`
- `interaction.api`, `interaction.pointer`, `interaction.drag`, `interaction.key`
- `state.read`, `state.assert`
- `evidence.capture`, `console.read`
- `scenario.run`, `scenario.cancel`, `scenario.status`

### 4.3 `QaProfileService`

Routes all save reads and writes through a QA namespace or QA slot. Beginning a session captures the previously active profile and selects QA. Ending or aborting restores the previous selection. Recovery on the next startup detects an unclosed QA session and restores the normal profile before loading gameplay.

The service must preserve settings such as BGM, SFX, fullscreen, resolution, and language while resetting gameplay progress.

### 4.4 `QaSceneRegistry` and `IQaSceneAdapter`

The registry resolves the current scene to one adapter. An unsupported scene returns a typed `UnsupportedScene` result instead of falling back to `GameObject.Find` guesses.

```csharp
public interface IQaSceneAdapter
{
    string SceneId { get; }
    IReadOnlyCollection<string> PresetIds { get; }
    IReadOnlyCollection<string> InteractionIds { get; }
    Task<QaCommandResult> PreparePresetAsync(string presetId, CancellationToken token);
    Task<QaCommandResult> InteractAsync(string interactionId, QaInteractionMode mode,
        CancellationToken token);
    QaSceneSnapshot CaptureState();
}
```

Each adapter calls existing domain controllers, Fungus blocks, inventory services, and UI components. It must not duplicate gameplay rules.

### 4.5 Stable target identity

Interactive targets receive `QaTargetId` components or are registered explicitly by an adapter. IDs use lowercase dotted names such as `kitchen.sink.faucet`, `maidroom.food`, and `tutorroom.cheshire`.

Hierarchy paths and coordinates may be recorded as diagnostics but cannot be the authoritative locator.

### 4.6 `QaInputDriver`

Supports two modes:

1. `Api`: invokes the same controller boundary used by normal gameplay.
2. `RealInput`: computes the target position and sends pointer, drag, or keyboard events through Unity's active input/EventSystem path.

Every critical scenario runs API mode first. The final interaction pass repeats user-visible steps in RealInput mode. API pass plus RealInput failure is classified as an input/UI-layer defect.

### 4.7 `QaStateProbe`

Produces JSON-serializable snapshots containing:

- active scene and load readiness;
- QA profile and session ID;
- inventory item IDs and quantities;
- quest ID, step, and HUD visibility;
- allow-listed Fungus variables;
- current Flowchart block where available;
- `InteractionInputGate` state and lock owner;
- relevant panels, targets, active state, interactability, sorting order;
- AI request state and last recoverable error;
- new Console errors and warnings since the previous checkpoint.

Sensitive values, chat tokens, and unrestricted player text are never captured.

### 4.8 `QaEvidenceRecorder`

Writes one immutable run directory:

```text
docs/qa/runs/2026-07-22T143000Z-run-<id>/
  report.md
  manifest.json
  events.jsonl
  console.log
  screenshots/
```

`manifest.json` records git commit, Unity version, scene list, scenario versions, run mode, and result counts. `report.md` links evidence and labels each result `PASS`, `FAIL`, `BLOCKED`, or `NOT_RUN`.

## 5. Scenario Format

JSON scenarios compose only commands and IDs exposed by adapters.

```json
{
  "schemaVersion": 1,
  "id": "kitchen.faucet-key",
  "scene": "Kitchen",
  "preset": "before-faucet",
  "steps": [
    { "command": "interaction.drag", "target": "inventory.bottle", "destination": "kitchen.sink.dropzone" },
    { "command": "interaction.pointer", "target": "kitchen.sink.faucet" },
    { "command": "state.assert", "condition": "target.enabled", "target": "kitchen.maid-key", "timeoutMs": 10000 },
    { "command": "interaction.pointer", "target": "kitchen.maid-key" },
    { "command": "state.assert", "condition": "inventory.contains", "value": "maid-room-key" }
  ]
}
```

The runner rejects unknown schema versions, scene IDs, commands, targets, presets, and assertion types before entering Play Mode.

## 6. Cursor Subagent Model

Project-defined agents live in `.cursor/agents/`:

- `qa-coordinator.md`: owns the run plan, delegates analysis, requests the execution lease, and publishes the consolidated verdict.
- `qa-inventory.md`: maps QA sheet items to scenes, adapters, presets, and existing tests. Read-only.
- `qa-scenario-author.md`: creates or reviews scenario JSON and expected assertions. It cannot run Unity.
- `qa-playtester.md`: sole agent authorized to drive Unity through the QA command gateway. It must acquire and release the lease.
- `qa-evidence-reviewer.md`: checks artifacts against acceptance criteria and rejects unsupported PASS claims. Read-only.

Cursor supports independent subagents and project custom agents. Because Task-tool availability may vary by Cursor version/model, the workflow performs a preflight. If custom delegation is unavailable, the parent runs the same agent prompt through `cursor-agent -p --output-format json` sequentially and preserves the same input/output envelopes.

Subagents exchange bounded JSON packets, not conversational summaries:

```json
{
  "taskId": "qa-152",
  "scenarioIds": ["mainmenu.new-game-reset"],
  "evidenceRoot": "docs/qa/runs/<run-id>",
  "status": "ready|running|pass|fail|blocked",
  "findings": []
}
```

## 7. Execution Flow

1. Parent checks git revision, Unity readiness, compilation, Console baseline, Task/subagent availability, and QA Driver version.
2. Inventory and scenario agents may work in parallel because they are read-only.
3. Coordinator validates all scenarios before runtime mutation.
4. Playtester obtains the exclusive Unity lease and begins an isolated QA profile.
5. For each scenario, the runner resets the preset, executes API mode, captures state, then resets and repeats user-visible actions in RealInput mode.
6. Recorder captures screenshots and Console deltas at checkpoints and on any failure.
7. Playtester ends the QA profile and releases the lease in `finally` behavior.
8. Evidence reviewer independently verifies every verdict.
9. Coordinator writes the consolidated local report. Google Sheets is not modified.

## 8. Error Handling and Recovery

- Compilation or domain reload: wait for readiness; do not retry commands against a stale adapter.
- Scene load timeout: capture current scene, loading state, Console, and screenshot; mark `BLOCKED`.
- Missing target or adapter: fail validation before interaction.
- Input gate stuck: record lock owner and elapsed time; attempt no synthetic unlock; mark `FAIL`.
- AI/backend unavailable: distinguish product failure from environment block using connection and timeout evidence.
- Unity connection loss: close the run as interrupted. On reconnect, restore the normal profile before starting a new run.
- Developer overlay failure: the QA core remains headless-operable; panel failure cannot block CLI cleanup.
- Subagent crash: lease expires by heartbeat timeout, but profile recovery runs before any subsequent scenario.

No automatic fix is performed during a QA run. Diagnosis and implementation occur in a separate task after the evidence is finalized.

## 9. Testing Strategy

- EditMode unit tests for command validation, lease behavior, profile isolation, registry resolution, scenario parsing, assertions, and report aggregation.
- Scene serialization tests for each `QaTargetId` and adapter reference.
- PlayMode component tests for EventSystem click, drag, keyboard input, condition-based waiting, cancellation, and cleanup.
- Contract tests proving panel and CLI send identical commands and receive equivalent results.
- Recovery tests for domain reload, scene timeout, aborted run, missing target, and stale lease.
- End-to-end acceptance for every supported scene, beginning with the six defects from 2026-07-15 onward.

A PASS requires the original reproduction path, expected state assertions, no new relevant Console exception, and required screenshots. Automated unit tests alone cannot produce a gameplay PASS.

## 10. Rollout

1. Stabilize developer overlay and add the headless QA core.
2. Add profile isolation, evidence, lease, and Cursor agent definitions.
3. Implement Hall/MainMenu/Kitchen/MaidRoom/TutorRoom adapters and the six initial scenarios.
4. Add remaining first-floor scenes.
5. Add second-floor and basement scenes.
6. Enforce adapter/scenario coverage in CI for all Build Settings scenes.

## 11. Non-Goals

- Shipping QA controls in release builds.
- Replacing Unity Test Framework.
- Letting multiple agents control one Unity Editor concurrently.
- Using unrestricted arbitrary C# execution as the stable QA API.
- Automatically editing production code or the Google QA sheet during playtest runs.

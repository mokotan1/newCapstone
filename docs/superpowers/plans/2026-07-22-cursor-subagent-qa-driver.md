# Cursor Subagent QA Driver Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an Editor/Development-only Unity QA Driver that Cursor QA subagents and a human developer panel can use to execute isolated, evidence-backed whole-game playtests.

**Architecture:** A headless `QaDriverCore` accepts typed commands from both a Unity CLI custom tool and the developer panel. Scene-specific adapters expose stable targets and presets, a single-writer lease serializes Unity mutation, and Cursor subagents parallelize read-only planning/evidence review while one playtester owns runtime execution.

**Tech Stack:** Unity 6000.0.36f1, C#, Unity Test Framework/NUnit, Unity UI/EventSystem/Input, Fungus, repo-local `unity-cli`, JSON/JSONL, Cursor custom subagents.

## Global Constraints

- Runtime QA features compile only when `UNITY_EDITOR || DEVELOPMENT_BUILD` is true.
- QA runs use a dedicated QA profile and never mutate the normal player save.
- BGM, SFX, fullscreen, resolution, and language settings survive QA profile resets.
- Only one agent/process may mutate the Unity Editor or active QA session at a time.
- API-level and RealInput-level outcomes are recorded separately.
- A gameplay PASS requires the original reproduction path, state assertions, screenshots, and no new relevant Console exception.
- Results are local under `docs/qa/runs/`; this plan does not write to Google Sheets.
- Existing user changes outside files listed by a task must be preserved.
- No bug is fixed during a QA run; fixes use separate diagnosis and implementation tasks.

---

## File Structure

### Runtime core

- `disputatio/Assets/mokotan/mokotan/script/QA/Core/`: commands, results, driver, run state, cancellation.
- `disputatio/Assets/mokotan/mokotan/script/QA/Profile/`: QA profile routing and crash recovery.
- `disputatio/Assets/mokotan/mokotan/script/QA/Scenes/`: registry, adapter interface, scene adapters.
- `disputatio/Assets/mokotan/mokotan/script/QA/Input/`: API and RealInput dispatch.
- `disputatio/Assets/mokotan/mokotan/script/QA/Evidence/`: snapshots and evidence records.
- `disputatio/Assets/mokotan/mokotan/script/QA/UI/`: human developer panel.
- `disputatio/Assets/mokotan/mokotan/script/QA/Scenarios/`: scenario schema and runner.

### Editor and orchestration

- `disputatio/Assets/Editor/QA/`: Unity CLI bridge, editor readiness, screenshot/Console bridge.
- `.cursor/agents/`: coordinator, inventory, scenario author, playtester, evidence reviewer.
- `.cursor/rules/qa-subagent-orchestration.mdc`: delegation and exclusive-lease rules.
- `scripts/qa/`: Cursor Task preflight/fallback and run wrapper.
- `disputatio/Assets/Resources/QA/Scenarios/`: versioned scenario JSON.
- `docs/qa/runs/`: generated evidence, with large/generated artifacts ignored as defined in Task 6.

---

### Task 1: Stabilize the developer overlay lifecycle

**Files:**
- Modify: `disputatio/Assets/mokotan/mokotan/script/UI/Debug/DeveloperModeGuiStyles.cs`
- Modify: `disputatio/Assets/mokotan/mokotan/script/UI/Debug/InGameDeveloperOverlay.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/UI/DeveloperModeGuiStylesTests.cs`
- Test: `disputatio/Assets/Tests/PlayMode/DeveloperOverlayPlayModeTests.cs`

**Interfaces:**
- Produces: `DeveloperModeGuiStyles.MarkDirty()` and OnGUI-only style materialization.
- Guarantees: adding/enabling `InGameDeveloperOverlay` outside `OnGUI` throws no GUI exception.

- [ ] **Step 1: Write a failing PlayMode lifecycle test**

```csharp
[UnityTest]
public IEnumerator AddingOverlay_OutsideOnGui_DoesNotLogGuiException()
{
    LogAssert.NoUnexpectedReceived();
    var host = new GameObject("DeveloperOverlayTestHost");
    host.AddComponent<InGameDeveloperOverlay>();
    yield return null;
    Object.Destroy(host);
}
```

- [ ] **Step 2: Run the focused test and confirm the current exception**

Run:

```powershell
.\scripts\unity-cli.cmd --project disputatio test --mode PlayMode --filter DeveloperOverlayPlayModeTests
```

Expected: FAIL with `You can only call GUI functions from inside OnGUI`.

- [ ] **Step 3: Move GUI style construction behind the OnGUI boundary**

Make `OnEnable` load scalar preferences and call `guiStyles.MarkDirty()` only. In `OnGUI`, after the visibility guard, call:

```csharp
guiStyles.EnsureBuilt(DeveloperModeGuiTypography.FontSize);
```

`DeveloperModeGuiStyles` must not access `GUI.skin` from its constructor, `OnEnable`, `Start`, or `Update` callers.

- [ ] **Step 4: Verify PlayMode start, screenshot, and stop three times**

Run the PlayMode test, then execute three `editor play --wait` / `screenshot --view game` / `editor stop` cycles. Expected: every command exits 0 and Console contains no GUI lifecycle exception.

- [ ] **Step 5: Commit the isolated stabilization**

```powershell
git add disputatio/Assets/mokotan/mokotan/script/UI/Debug disputatio/Assets/Editor/Tests/EditMode/UI disputatio/Assets/Tests/PlayMode
git commit -m "fix: stabilize developer overlay for automated QA"
```

---

### Task 2: Define typed QA commands and single-run driver core

**Files:**
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Core/QaCommand.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Core/QaCommandResult.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Core/QaRunState.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Core/IQaDriver.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Core/QaDriverCore.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/QA/QaDriverCoreTests.cs`

**Interfaces:**
- Produces: `Task<QaCommandResult> IQaDriver.ExecuteAsync(QaCommand, CancellationToken)`.
- Produces: immutable `QaRunId`, command sequence numbers, typed result codes.
- Consumes later: `IQaProfileService`, `IQaSceneRegistry`, `IQaInputDriver`, `IQaEvidenceRecorder`.

- [ ] **Step 1: Add failing tests for validation and concurrent-run rejection**

Test that blank command IDs return `InvalidCommand`, unknown command types return `UnsupportedCommand`, and a second `session.begin` returns `RunAlreadyActive` without changing the first run.

```csharp
QaCommandResult second = await driver.ExecuteAsync(
    QaCommand.BeginSession("second"), CancellationToken.None);
Assert.AreEqual(QaResultCode.RunAlreadyActive, second.Code);
```

- [ ] **Step 2: Run `QaDriverCoreTests` and confirm compilation failure**

Expected: missing QA core types.

- [ ] **Step 3: Implement minimal command/result/run-state types**

Use explicit enums for command and result codes. Do not accept arbitrary C# source or reflection member names in `QaCommand`.

- [ ] **Step 4: Implement serialized command execution**

Guard execution with one async-compatible gate and assign monotonically increasing sequence numbers. Cancellation returns `Cancelled`; exceptions become `InternalError` with a sanitized message and are also sent to evidence recording.

- [ ] **Step 5: Run tests and commit**

Expected: all `QaDriverCoreTests` pass.

```powershell
git add disputatio/Assets/mokotan/mokotan/script/QA/Core disputatio/Assets/Editor/Tests/EditMode/QA
git commit -m "feat: add typed QA driver core"
```

---

### Task 3: Add exclusive Unity execution lease

**Files:**
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Core/QaExecutionLease.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Core/QaLeaseService.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/QA/QaLeaseServiceTests.cs`

**Interfaces:**
- Produces: `TryAcquire(ownerId, runId, ttl)`, `Heartbeat(leaseId)`, `Release(leaseId)`.
- Guarantees: one active writer; stale lease expiration does not silently continue a prior run.

- [ ] **Step 1: Write failing tests for ownership, heartbeat, and expiry**

Cover different owner rejection, same-owner idempotent heartbeat, invalid release, and expiry requiring an explicit recovery result.

- [ ] **Step 2: Implement an in-process lease with persisted recovery marker**

Persist only `runId`, `ownerId`, `leaseId`, and last heartbeat under the QA profile. Never persist secrets or command payloads.

- [ ] **Step 3: Connect `QaDriverCore` mutation commands to lease validation**

Read-only `state.read` and completed-run evidence reads do not require a lease. Scene, profile, input, and scenario commands do.

- [ ] **Step 4: Run tests and commit**

```powershell
git add disputatio/Assets/mokotan/mokotan/script/QA/Core disputatio/Assets/Editor/Tests/EditMode/QA
git commit -m "feat: serialize Unity QA execution with leases"
```

---

### Task 4: Implement isolated QA profiles and crash recovery

**Files:**
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Profile/IQaProfileService.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Profile/QaProfileService.cs`
- Modify: `disputatio/Assets/mokotan/mokotan/script/UI/PlayDataPrefsCleaner.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/QA/QaProfileServiceTests.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/PlayDataPrefsCleanerTests.cs`

**Interfaces:**
- Produces: `BeginQaProfile(runId)`, `ResetGameplay()`, `RestorePreviousProfile()`, `RecoverInterruptedSession()`.
- Guarantees: normal gameplay keys remain byte-for-byte unchanged during QA.

- [ ] **Step 1: Inventory all save boundaries**

Record PlayerPrefs gameplay keys, settings keys, Fungus global variables, inventory persistence, `DontDestroyOnLoad` state, and any file-backed saves in the test fixture. Turn each category into an assertion.

- [ ] **Step 2: Write a failing isolation test**

Seed normal progress and settings, begin QA, mutate QA progress, end QA, then assert normal progress and settings equal their original values.

- [ ] **Step 3: Implement namespaced QA storage and runtime-state reset**

Wrap profile selection behind `IQaProfileService`. Extend the existing cleaner only through explicit key classification; do not delete unknown settings keys.

- [ ] **Step 4: Add interrupted-session recovery test**

Simulate a persisted active QA marker, recreate the service, call recovery, and assert the normal profile is selected before scene loading.

- [ ] **Step 5: Run tests and commit**

```powershell
git add disputatio/Assets/mokotan/mokotan/script/QA/Profile disputatio/Assets/mokotan/mokotan/script/UI/PlayDataPrefsCleaner.cs disputatio/Assets/Editor/Tests/EditMode
git commit -m "feat: isolate automated QA save data"
```

---

### Task 5: Add scene adapter registry and stable target IDs

**Files:**
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Scenes/IQaSceneAdapter.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Scenes/QaSceneRegistry.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Scenes/QaTargetId.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Scenes/QaSceneSnapshot.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/QA/QaSceneRegistryTests.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/QA/QaTargetIdTests.cs`

**Interfaces:**
- Produces: the `IQaSceneAdapter` contract from the design.
- Produces: `TryResolveScene(sceneName, out adapter)` and `TryResolveTarget(targetId, out target)`.

- [ ] **Step 1: Write failing duplicate-ID and unsupported-scene tests**

Duplicate active IDs must fail registry validation with both hierarchy diagnostics. Unsupported scenes return `UnsupportedScene`; no best-effort name search occurs.

- [ ] **Step 2: Implement focused registry and target components**

Normalize IDs to lowercase dotted strings and reject whitespace, hierarchy separators, and duplicates.

- [ ] **Step 3: Add Build Settings coverage audit**

Create an EditMode test that lists every enabled gameplay scene without a registered adapter. During rollout it reports explicit missing scenes; after Task 13 it becomes a hard failure.

- [ ] **Step 4: Run tests and commit**

```powershell
git add disputatio/Assets/mokotan/mokotan/script/QA/Scenes disputatio/Assets/Editor/Tests/EditMode/QA
git commit -m "feat: add QA scene adapter registry"
```

---

### Task 6: Build evidence capture and immutable run reports

**Files:**
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Evidence/QaRunManifest.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Evidence/QaEvidenceEvent.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Evidence/IQaEvidenceRecorder.cs`
- Create: `disputatio/Assets/Editor/QA/EditorQaEvidenceRecorder.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Evidence/DevelopmentQaEvidenceRecorder.cs`
- Modify: `.gitignore`
- Test: `disputatio/Assets/Editor/Tests/EditMode/QA/QaEvidenceRecorderTests.cs`

**Interfaces:**
- Produces: append-only `events.jsonl`, final `manifest.json`, `report.md`, screenshots, and Console log.
- Consumes: command results and `QaDriverSnapshot`.

- [ ] **Step 1: Write failing path-safety and append-only tests**

Reject run IDs containing separators and verify a second event appends rather than overwrites. Redact configured token/header fields.

- [ ] **Step 2: Implement the exact run layout**

Use `docs/qa/runs/<UTC timestamp>-run-<id>/`. Keep `report.md` and `manifest.json` trackable; ignore generated screenshot binaries and raw JSONL/Console files if their volume is unsuitable for Git, while retaining `.gitkeep` and documented paths.

- [ ] **Step 3: Add verdict aggregation**

Only explicit evidence-backed success becomes `PASS`; missing screenshot/assertion becomes `BLOCKED` or `NOT_RUN`. Never infer PASS from no exception.

- [ ] **Step 4: Run tests and commit**

```powershell
git add .gitignore disputatio/Assets/mokotan/mokotan/script/QA/Evidence disputatio/Assets/Editor/QA disputatio/Assets/Editor/Tests/EditMode/QA
git commit -m "feat: record immutable QA evidence"
```

---

### Task 7: Implement API and RealInput drivers

**Files:**
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Input/QaInteractionMode.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Input/IQaInputDriver.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Input/QaApiInputDriver.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Input/QaEventSystemInputDriver.cs`
- Test: `disputatio/Assets/Tests/PlayMode/QA/QaInputDriverPlayModeTests.cs`

**Interfaces:**
- Produces: `ClickAsync`, `DragAsync`, and `KeyAsync` for stable target IDs.
- Guarantees: RealInput goes through active Unity input/EventSystem paths; API mode uses adapter/controller boundaries.

- [ ] **Step 1: Create PlayMode fixtures for click, drag, disabled target, and covered target**

Use a test Canvas and EventSystem. Assert that a covered or non-interactable target does not report successful RealInput.

- [ ] **Step 2: Implement condition-based completion**

Wait for event receipt, target state change, or timeout. Do not use fixed sleeps as success criteria.

- [ ] **Step 3: Classify API-pass/RealInput-fail**

Return `InputLayerFailure` with target raycast results, sorting order, interactability, and input-gate snapshot.

- [ ] **Step 4: Run PlayMode tests and commit**

```powershell
git add disputatio/Assets/mokotan/mokotan/script/QA/Input disputatio/Assets/Tests/PlayMode/QA
git commit -m "feat: add hybrid QA input drivers"
```

---

### Task 8: Add state probes and condition-based assertions

**Files:**
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Evidence/QaDriverSnapshot.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Evidence/QaStateProbe.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Scenarios/QaAssertion.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Scenarios/QaConditionWaiter.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/QA/QaStateProbeTests.cs`
- Test: `disputatio/Assets/Tests/PlayMode/QA/QaConditionWaiterTests.cs`

**Interfaces:**
- Produces: allow-listed, JSON-serializable snapshots and typed assertions.
- Produces: timeout diagnostics with last observed value and elapsed time.

- [ ] **Step 1: Write tests for inventory, quest, Fungus, panel, input gate, and AI state**

Assert sensitive tokens and unrestricted prompt/response text are absent from serialization.

- [ ] **Step 2: Implement typed assertion evaluators**

Initial assertion types: equality, boolean, inventory contains, target active/interactable, quest current/completed, input unlocked, Flowchart idle, and no-new-console-error.

- [ ] **Step 3: Implement polling with cancellation and deadline**

Use realtime deadlines and frame yields. Report `TimedOut` with a final snapshot; never force-unlock gameplay state.

- [ ] **Step 4: Run tests and commit**

```powershell
git add disputatio/Assets/mokotan/mokotan/script/QA/Evidence disputatio/Assets/mokotan/mokotan/script/QA/Scenarios disputatio/Assets/Editor/Tests/EditMode/QA disputatio/Assets/Tests/PlayMode/QA
git commit -m "feat: expose QA state assertions"
```

---

### Task 9: Add validated JSON scenarios and runner

**Files:**
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Scenarios/QaScenarioDefinition.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Scenarios/QaScenarioValidator.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Scenarios/QaScenarioRunner.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/QA/QaScenarioValidatorTests.cs`
- Test: `disputatio/Assets/Tests/PlayMode/QA/QaScenarioRunnerTests.cs`

**Interfaces:**
- Produces: schema version 1 parser/validator and sequential runner.
- Consumes: driver, registry, profile, input, assertions, evidence.

- [ ] **Step 1: Write failing validation tests**

Reject unknown schema, command, scene, preset, target, assertion, duplicate step ID, and non-positive timeout before Play Mode mutation.

- [ ] **Step 2: Implement strict schema version 1**

Deserialize into typed DTOs. Do not execute arbitrary method names, C# expressions, or reflection paths from JSON.

- [ ] **Step 3: Implement runner cleanup boundary**

Begin QA profile and lease, execute steps, capture failure evidence, then restore profile and release lease in `finally` behavior for success, failure, cancellation, and exception.

- [ ] **Step 4: Test cancellation and recovery**

Cancel mid-wait and assert profile restoration, lease release, final manifest status `interrupted`, and captured final snapshot.

- [ ] **Step 5: Run tests and commit**

```powershell
git add disputatio/Assets/mokotan/mokotan/script/QA/Scenarios disputatio/Assets/Editor/Tests/EditMode/QA disputatio/Assets/Tests/PlayMode/QA
git commit -m "feat: run validated QA scenarios"
```

---

### Task 10: Expose the same gateway to Unity CLI and the human panel

**Files:**
- Create: `disputatio/Assets/Editor/QA/QaUnityCliTools.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/UI/QaDeveloperPanel.cs`
- Modify: `disputatio/Assets/mokotan/mokotan/script/DevMode/DeveloperModeController.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/QA/QaCommandGatewayContractTests.cs`
- Test: `disputatio/Assets/Tests/PlayMode/QA/QaDeveloperPanelTests.cs`

**Interfaces:**
- Produces Unity CLI tools: `qa_status`, `qa_list`, `qa_run`, `qa_cancel`, `qa_capture`, `qa_recover`.
- Produces a panel that submits the exact same `QaCommand` DTOs.

- [ ] **Step 1: Write contract tests proving CLI and panel command equivalence**

Given the same scenario ID and options, both adapters must serialize identical command type, arguments, timeout, and correlation ID fields.

- [ ] **Step 2: Implement CLI tools as thin gateway calls**

Each tool returns structured JSON and refuses mutation without a valid lease/session. Avoid using general `exec` as the documented QA interface.

- [ ] **Step 3: Implement the panel without blocking headless operation**

Panel sections: readiness, profile, scene/preset, scenario list, step controls, current state, evidence path, cancel/recover. A panel rendering exception must not own or dispose the core.

- [ ] **Step 4: Run tests, manually open the panel, and commit**

```powershell
git add disputatio/Assets/Editor/QA disputatio/Assets/mokotan/mokotan/script/QA/UI disputatio/Assets/mokotan/mokotan/script/DevMode/DeveloperModeController.cs disputatio/Assets/Editor/Tests/EditMode/QA disputatio/Assets/Tests/PlayMode/QA
git commit -m "feat: expose QA driver to CLI and developer panel"
```

---

### Task 11: Define Cursor QA subagents and orchestration fallback

**Files:**
- Create: `.cursor/agents/qa-coordinator.md`
- Create: `.cursor/agents/qa-inventory.md`
- Create: `.cursor/agents/qa-scenario-author.md`
- Create: `.cursor/agents/qa-playtester.md`
- Create: `.cursor/agents/qa-evidence-reviewer.md`
- Create: `.cursor/rules/qa-subagent-orchestration.mdc`
- Create: `scripts/qa/invoke-qa-agent.ps1`
- Create: `scripts/qa/run-cursor-qa.ps1`
- Test: `tools/tests/test_cursor_qa_agent_contracts.py`

**Interfaces:**
- Produces bounded JSON handoff packets and role-specific tool restrictions in prompts.
- Produces Task preflight with sequential `cursor-agent -p --output-format json` fallback.

- [ ] **Step 1: Write contract tests for agent definitions**

Assert every file has `name` and `description` frontmatter, references the common evidence root, emits the required JSON envelope, and only `qa-playtester` claims Unity mutation authority.

- [ ] **Step 2: Write each role prompt**

The coordinator delegates and aggregates; inventory and evidence roles are read-only; scenario author writes scenario files only when explicitly authorized; playtester must acquire/release the QA lease and must not edit production code during a run.

- [ ] **Step 3: Implement preflight and fallback wrapper**

The wrapper checks Cursor CLI availability and whether the selected Cursor session exposes custom Task delegation. If unavailable, invoke the same role prompt sequentially with:

```powershell
cursor-agent -p --output-format json --workspace $Workspace --prompt $Prompt
```

Keep normal command approvals enabled. The wrapper must run `cursor-agent --help` during preflight and fail with an actionable message if the installed CLI does not support an option used by the wrapper. Capture Cursor version in the run manifest.

- [ ] **Step 4: Enforce Unity serialization in the coordinator rule**

Parallelize repository search, scenario review, and evidence review only. Route all runtime mutation to one `qa-playtester` job and reject a second lease owner.

- [ ] **Step 5: Run contract tests and commit**

```powershell
pytest tools/tests/test_cursor_qa_agent_contracts.py -q
git add .cursor/agents .cursor/rules/qa-subagent-orchestration.mdc scripts/qa tools/tests/test_cursor_qa_agent_contracts.py
git commit -m "feat: orchestrate Cursor QA subagents"
```

---

### Task 12: Implement initial adapters and six post-July-15 scenarios

**Files:**
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Scenes/MainMenuQaAdapter.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Scenes/HallQaAdapter.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Scenes/KitchenQaAdapter.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Scenes/MaidRoomQaAdapter.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Scenes/TutorRoomQaAdapter.cs`
- Create: six JSON files under `disputatio/Assets/Resources/QA/Scenarios/2026-07/`
- Modify: target scenes under `disputatio/Assets/Scenes/Mokotan/` only where stable `QaTargetId` serialization is required
- Test: `disputatio/Assets/Editor/Tests/EditMode/QA/InitialSceneAdapterSerializationTests.cs`
- Test: `disputatio/Assets/Tests/PlayMode/QA/July15RegressionScenarioTests.cs`

**Interfaces:**
- Produces scenarios: `kitchen.faucet-key`, `mainmenu.new-game-reset`, `hall.kitchen-quest`, `tutorroom.cheshire-quiz`, `kitchen.cheshire-repeat`, `maidroom.food-effect`.

- [ ] **Step 1: Write serialization tests before editing scenes**

For every required target, assert one stable ID, expected component/controller, valid canvas/raycast state, and adapter registration.

- [ ] **Step 2: Implement adapters against existing domain controllers**

Adapters expose presets and state but do not duplicate quest, inventory, Fungus, or chatbot rules. Where an existing controller boundary is missing, stop and create a separate reviewed implementation task rather than using private reflection.

- [ ] **Step 3: Author six strict scenarios**

Each scenario contains setup, API pass, reset, RealInput pass, state assertions, screenshot checkpoints, and Console-delta assertion. AI scenarios distinguish service unavailability from interaction lock failures.

- [ ] **Step 4: Run each scenario independently**

Expected: current product behavior may produce FAIL; the runner itself must finish, restore the profile, release the lease, and generate complete evidence. Do not alter scenario expectations to make failures pass.

- [ ] **Step 5: Commit adapters, scenarios, tests, and scene wiring**

```powershell
git add disputatio/Assets/mokotan/mokotan/script/QA/Scenes disputatio/Assets/Resources/QA/Scenarios disputatio/Assets/Scenes/Mokotan disputatio/Assets/Editor/Tests/EditMode/QA disputatio/Assets/Tests/PlayMode/QA
git commit -m "test: add initial whole-flow QA scenarios"
```

---

### Task 13: Expand adapter coverage to the whole game

**Files:**
- Create/modify: adapters under `disputatio/Assets/mokotan/mokotan/script/QA/Scenes/`
- Create: scenario files grouped by floor/area under `disputatio/Assets/Resources/QA/Scenarios/`
- Modify: gameplay scenes only for stable target IDs
- Test: `disputatio/Assets/Editor/Tests/EditMode/QA/BuildSceneQaCoverageTests.cs`
- Test: scene-group PlayMode fixtures under `disputatio/Assets/Tests/PlayMode/QA/`

**Interfaces:**
- Produces one adapter or explicit non-gameplay exemption for every enabled Build Settings scene.

- [ ] **Step 1: Generate and review the enabled-scene inventory**

Classify scenes as menu, cutscene, corridor/hub, room/puzzle, transition, or non-gameplay exemption. Store the reviewed inventory as a test fixture.

- [ ] **Step 2: Implement one independently reviewable area at a time**

Order: remaining first floor, second floor, basement, opening/cutscenes, transitions. Each area gets adapter serialization tests and at least one API/RealInput smoke scenario before moving on.

- [ ] **Step 3: Convert the coverage audit to a hard gate**

Fail if an enabled gameplay scene has no adapter, duplicate scene ownership, unresolved target ID, or no smoke scenario.

- [ ] **Step 4: Run the full QA coverage suites and commit each area separately**

Expected: zero infrastructure failures; gameplay defects remain explicit scenario FAIL results rather than test harness crashes.

---

### Task 14: Perform end-to-end Cursor subagent acceptance

**Files:**
- Create: `docs/qa/cursor-subagent-qa-driver-operations.md`
- Create: one run directory under `docs/qa/runs/`
- Modify: `README.md` with a short QA entrypoint link

**Interfaces:**
- Verifies the complete coordinator-to-subagent-to-Unity-to-evidence workflow.

- [ ] **Step 1: Start from a clean Unity and QA state**

Confirm compilation complete, Console baseline captured, no active QA lease, normal profile selected, and current git commit recorded.

- [ ] **Step 2: Run the six initial scenarios through Cursor orchestration**

Use custom subagents when Task is available; otherwise use the documented Cursor CLI fallback. Confirm analysis work may overlap but Unity mutations are serialized.

- [ ] **Step 3: Kill one run during a condition wait and verify recovery**

Restart/reconnect, run `qa_recover`, and confirm normal profile restoration, expired lease handling, interrupted manifest, and ability to begin a new run.

- [ ] **Step 4: Review evidence independently**

The evidence reviewer must reject a seeded report that says PASS without a screenshot or assertion record, then accept a complete evidence set.

- [ ] **Step 5: Run final automated verification**

Run all QA EditMode tests, QA PlayMode tests, Cursor agent contract tests, scene coverage audit, compilation, and Console error scan. Record exact commands and counts in the operations document.

- [ ] **Step 6: Commit documentation and representative report**

```powershell
git add README.md docs/qa/cursor-subagent-qa-driver-operations.md docs/qa/runs
git commit -m "docs: document Cursor-driven Unity QA workflow"
```

---

## Plan Self-Review Checklist

- Every design component maps to at least one implementation task.
- Cursor subagents can parallelize only read-only work; Unity runtime mutation is single-writer.
- Custom Task unavailability has a Cursor CLI fallback.
- Normal saves are isolated and restored after success, failure, cancellation, connection loss, and restart.
- Panel and CLI share typed commands.
- API and RealInput paths are independently reported.
- Whole-game coverage is enforced only after incremental adapters exist.
- No step claims a gameplay defect is fixed merely because QA infrastructure passes.

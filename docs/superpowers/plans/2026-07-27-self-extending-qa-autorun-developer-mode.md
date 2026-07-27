# Self-Extending QA Autorun Developer Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Layer a typed `DeveloperQaService` + capability registry + StudyRoom diary-mirror vertical slice + external self-extending autorun orchestrator on top of the existing `Godlotto.QA` driver (`codex/qa-driver`).

**Architecture:** CLI and in-game panel remain thin clients. `IDeveloperQaService` is the sole mutation boundary for Developer Mode QA commands; it delegates Unity mutation to existing `IQaDriver` / scene adapters / DevMode tools. Scene-specific capabilities register through adapters. An external Python orchestrator in `scripts/qa/` owns failure classification, bounded patching, Git isolation, and resume.

**Tech Stack:** Unity 6000.0.36f1, C# (`UNITY_EDITOR || DEVELOPMENT_BUILD`), NUnit EditMode/PlayMode, existing `Godlotto.QA.*`, Python 3 + pytest for orchestrator, repo-local `unity-cli`.

**Base branch:** Implement on a branch forked from `codex/qa-driver` (QA Driver foundation already present). Do not start from bare `develop`.

---

## Global Constraints

- Runtime Developer/QA features compile only when `UNITY_EDITOR || DEVELOPMENT_BUILD`.
- QA runs use an isolated QA profile and never mutate the normal player save.
- CLI and panel produce equivalent result codes for the same command payload.
- Stable IDs only — no screen coordinates, hierarchy paths, arbitrary reflection, or arbitrary C# execution as public commands.
- QA-infrastructure commits and product-fix commits stay separate (design §8).
- Work in an isolated worktree; never `git reset --hard`; never push/PR without explicit user instruction.
- Same normalized failure signature: at most 3 patch attempts, then `BLOCKED`.
- PASS requires original scenario path, assertions, Console checks, screenshots, focused + affected regression tests.

---

## File Structure

### New / extended Unity runtime (on top of existing QA tree)

```
disputatio/Assets/mokotan/mokotan/script/QA/
  Developer/
    IDeveloperQaService.cs
    DeveloperQaService.cs
    DeveloperQaCommand.cs
    DeveloperQaResult.cs
    DeveloperQaSnapshot.cs
    DeveloperQaCapability.cs
    DeveloperQaCapabilityKind.cs
    DeveloperQaResultCode.cs
    DeveloperQaCapabilityRegistry.cs
  SceneAdapters/
    StudyRoomQaAdapter.cs          # NEW — diary mirror capabilities
  UI/
    DeveloperQaPanelBridge.cs      # thin adapter over InGameDeveloperOverlay / QaDeveloperPanel
```

### Editor / tests / scenarios / orchestrator

```
disputatio/Assets/Editor/QA/DeveloperQaCliBridge.cs
disputatio/Assets/Editor/Tests/EditMode/QA/Developer/
disputatio/Assets/Tests/PlayMode/QA/Developer/
disputatio/Assets/Resources/QA/Scenarios/studyroom-mirror-diary.json
scripts/qa/autorun/
  __init__.py
  classify.py
  checkpoint.py
  orchestrator.py
  git_isolation.py
  report.py
scripts/qa/tests/
docs/qa/runs/   # generated evidence (gitignored binaries as already planned)
```

### Existing seams to reuse (do not reimplement)

- `Godlotto.QA.Core.IQaDriver` / `QaDriverCore`
- `QaProfileService`, evidence recorder, scenario runner
- `StudyRoomPuzzleDevTool`, `DeveloperModeController`, `InGameDeveloperOverlay`
- `StudyRoomDiaryMirrorPuzzleController`, `StudyRoomMirrorPuzzleSuccessRouter`, `FilterCardBookDropZone`

---

### Task 1: Typed DeveloperQa contract

**Files:**
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Developer/DeveloperQaCapabilityKind.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Developer/DeveloperQaResultCode.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Developer/DeveloperQaCapability.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Developer/DeveloperQaCommand.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Developer/DeveloperQaResult.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Developer/DeveloperQaSnapshot.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Developer/IDeveloperQaService.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Developer/DeveloperQaService.cs`
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Developer/Godlotto.QA.Developer.asmdef` (reference Core + UnityEngine; editor/dev define constraints via existing Core pattern)
- Test: `disputatio/Assets/Editor/Tests/EditMode/QA/Developer/DeveloperQaServiceTests.cs`

**Interfaces:**
- `Task<DeveloperQaResult> ExecuteAsync(DeveloperQaCommand, CancellationToken)`
- `DeveloperQaSnapshot CaptureSnapshot()`
- `IReadOnlyCollection<DeveloperQaCapability> ListCapabilities()`
- Initial command families as string `Family` + `Name`: `capability.list`, `capability.describe`, `state.capture`, `state.assert`, `evidence.capture`, `preset.apply`, `scene.load`, `scene.waitReady`, `interaction.invoke`, `scenario.run|resume|cancel|status`

- [ ] **Step 1: Write failing EditMode tests**

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;
using NUnit.Framework;

public class DeveloperQaServiceTests
{
    [Test]
    public async Task ExecuteAsync_BlankCommandId_ReturnsInvalidCommand()
    {
        var service = new DeveloperQaService();
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("", "capability", "list"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.InvalidCommand, result.Code);
    }

    [Test]
    public async Task ExecuteAsync_UnknownFamily_ReturnsUnsupportedCommand()
    {
        var service = new DeveloperQaService();
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "not-a-family", "x"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.UnsupportedCommand, result.Code);
    }

    [Test]
    public void ListCapabilities_InitiallyEmpty_UntilAdaptersRegister()
    {
        var service = new DeveloperQaService();
        Assert.AreEqual(0, service.ListCapabilities().Count);
    }

    [Test]
    public void CaptureSnapshot_ReturnsNonNullWithEmptyCapabilityVersion()
    {
        var service = new DeveloperQaService();
        DeveloperQaSnapshot snap = service.CaptureSnapshot();
        Assert.IsNotNull(snap);
        Assert.IsFalse(string.IsNullOrEmpty(snap.CapturedAtUtc));
    }
}
#endif
```

- [ ] **Step 2: Run test — expect compile failure (missing types)**

```powershell
.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter DeveloperQaServiceTests
```

Expected: FAIL — types/namespace missing.

- [ ] **Step 3: Implement minimal types + service validation only**

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Godlotto.QA.Developer
{
    public enum DeveloperQaCapabilityKind { Preset, Interaction, Probe, Assertion, Recovery }

    public enum DeveloperQaResultCode
    {
        Ok,
        InvalidCommand,
        UnsupportedCommand,
        MissingCapability,
        AssertionFailed,
        Cancelled,
        InternalError,
        EnvironmentBlocked
    }

    public sealed class DeveloperQaCapability
    {
        public string Id { get; }
        public string SceneId { get; }
        public DeveloperQaCapabilityKind Kind { get; }
        public string InputSchema { get; }
        public string OutputSchema { get; }

        public DeveloperQaCapability(
            string id,
            string sceneId,
            DeveloperQaCapabilityKind kind,
            string inputSchema,
            string outputSchema)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            SceneId = sceneId ?? string.Empty;
            Kind = kind;
            InputSchema = inputSchema ?? "{}";
            OutputSchema = outputSchema ?? "{}";
        }
    }

    public sealed class DeveloperQaCommand
    {
        public string Id { get; }
        public string Family { get; }
        public string Name { get; }
        public string TargetId { get; }
        public IReadOnlyDictionary<string, string> Parameters { get; }

        private DeveloperQaCommand(
            string id,
            string family,
            string name,
            string targetId,
            IReadOnlyDictionary<string, string> parameters)
        {
            Id = id;
            Family = family;
            Name = name;
            TargetId = targetId;
            Parameters = parameters ?? new Dictionary<string, string>();
        }

        public static DeveloperQaCommand Create(
            string id,
            string family,
            string name,
            string targetId = null,
            IReadOnlyDictionary<string, string> parameters = null)
        {
            return new DeveloperQaCommand(id, family, name, targetId, parameters);
        }
    }

    public sealed class DeveloperQaResult
    {
        public DeveloperQaResultCode Code { get; }
        public string Message { get; }
        public string MissingCapabilityId { get; }
        public string CheckpointId { get; }
        public IReadOnlyDictionary<string, string> Data { get; }

        public DeveloperQaResult(
            DeveloperQaResultCode code,
            string message = null,
            string missingCapabilityId = null,
            string checkpointId = null,
            IReadOnlyDictionary<string, string> data = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            MissingCapabilityId = missingCapabilityId;
            CheckpointId = checkpointId;
            Data = data ?? new Dictionary<string, string>();
        }
    }

    public sealed class DeveloperQaSnapshot
    {
        public string CapturedAtUtc { get; }
        public string ActiveSceneId { get; }
        public string QaProfileId { get; }
        public string CapabilityRegistryVersion { get; }
        public IReadOnlyDictionary<string, string> State { get; }

        public DeveloperQaSnapshot(
            string capturedAtUtc,
            string activeSceneId,
            string qaProfileId,
            string capabilityRegistryVersion,
            IReadOnlyDictionary<string, string> state)
        {
            CapturedAtUtc = capturedAtUtc;
            ActiveSceneId = activeSceneId ?? string.Empty;
            QaProfileId = qaProfileId ?? string.Empty;
            CapabilityRegistryVersion = capabilityRegistryVersion ?? "0";
            State = state ?? new Dictionary<string, string>();
        }
    }

    public interface IDeveloperQaService
    {
        Task<DeveloperQaResult> ExecuteAsync(
            DeveloperQaCommand command,
            CancellationToken cancellationToken);

        DeveloperQaSnapshot CaptureSnapshot();
        IReadOnlyCollection<DeveloperQaCapability> ListCapabilities();
    }

    public sealed class DeveloperQaService : IDeveloperQaService
    {
        private static readonly HashSet<string> KnownFamilies = new HashSet<string>(StringComparer.Ordinal)
        {
            "capability", "preset", "scene", "interaction", "state", "evidence", "scenario"
        };

        public Task<DeveloperQaResult> ExecuteAsync(
            DeveloperQaCommand command,
            CancellationToken cancellationToken)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.Id))
            {
                return Task.FromResult(new DeveloperQaResult(
                    DeveloperQaResultCode.InvalidCommand,
                    "Command id is required."));
            }

            if (string.IsNullOrWhiteSpace(command.Family) ||
                !KnownFamilies.Contains(command.Family))
            {
                return Task.FromResult(new DeveloperQaResult(
                    DeveloperQaResultCode.UnsupportedCommand,
                    $"Unknown family '{command.Family}'."));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new DeveloperQaResult(DeveloperQaResultCode.Cancelled));
            }

            // Task 2+ fills real handlers; capability.list returns Ok with empty list for now.
            if (command.Family == "capability" && command.Name == "list")
            {
                return Task.FromResult(new DeveloperQaResult(DeveloperQaResultCode.Ok, "empty"));
            }

            return Task.FromResult(new DeveloperQaResult(
                DeveloperQaResultCode.UnsupportedCommand,
                $"{command.Family}.{command.Name} not implemented yet."));
        }

        public DeveloperQaSnapshot CaptureSnapshot()
        {
            return new DeveloperQaSnapshot(
                DateTime.UtcNow.ToString("o"),
                string.Empty,
                string.Empty,
                "0",
                new Dictionary<string, string>());
        }

        public IReadOnlyCollection<DeveloperQaCapability> ListCapabilities()
        {
            return Array.Empty<DeveloperQaCapability>();
        }
    }
}
#endif
```

Add `.asmdef` mirroring sibling QA folders (`Godlotto.QA.Developer`, reference `Godlotto.QA.Core` if needed; include `.meta` via Unity refresh).

- [ ] **Step 4: Re-run EditMode filter — expect PASS**

```powershell
.\scripts\unity-cli.cmd --project disputatio editor refresh --compile
.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter DeveloperQaServiceTests
```

- [ ] **Step 5: Commit**

```powershell
git add disputatio/Assets/mokotan/mokotan/script/QA/Developer disputatio/Assets/Editor/Tests/EditMode/QA/Developer
git commit -m "feat(qa): add DeveloperQaService typed contract"
```

---

### Task 2: Capability registry and MissingCapability

**Files:**
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/Developer/DeveloperQaCapabilityRegistry.cs`
- Modify: `DeveloperQaService.cs` — inject/register registry; `capability.describe`; unknown capability → `MissingCapability`
- Test: `disputatio/Assets/Editor/Tests/EditMode/QA/Developer/DeveloperQaCapabilityRegistryTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
[Test]
public void Register_ThenList_ReturnsCapability()
{
    var registry = new DeveloperQaCapabilityRegistry();
    registry.Register(new DeveloperQaCapability(
        "studyroom.mirror.probe",
        "StudyRoom",
        DeveloperQaCapabilityKind.Probe,
        "{}",
        "{hasBookmarkMirror:bool}"));
    Assert.AreEqual(1, registry.List().Count);
    Assert.AreEqual("1", registry.Version);
}

[Test]
public async Task ExecuteAsync_UnknownCapabilityInvoke_ReturnsMissingCapability()
{
    var service = new DeveloperQaService(new DeveloperQaCapabilityRegistry());
    DeveloperQaResult result = await service.ExecuteAsync(
        DeveloperQaCommand.Create(
            "c1",
            "interaction",
            "invoke",
            "studyroom.mirror.place-bookmark"),
        CancellationToken.None);
    Assert.AreEqual(DeveloperQaResultCode.MissingCapability, result.Code);
    Assert.AreEqual("studyroom.mirror.place-bookmark", result.MissingCapabilityId);
    Assert.IsFalse(string.IsNullOrEmpty(result.CheckpointId));
}
```

- [ ] **Step 2: Run — expect FAIL**

```powershell
.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter DeveloperQaCapabilityRegistryTests
```

- [ ] **Step 3: Implement registry + MissingCapability path**

`MissingCapability` result must include: missing stable ID, scene (if known), requested input schema hint, current capability ids (in `Data`), checkpoint ID, and optionally snapshot fields in `Data`.

Bump `Version` as a monotonic string integer on each successful `Register`.

- [ ] **Step 4: PASS + commit**

```powershell
git commit -m "feat(qa): add capability registry and MissingCapability results"
```

---

### Task 3: QA profile session boundary on DeveloperQaService

**Files:**
- Modify: `DeveloperQaService.cs` to accept `IQaProfileService` (existing)
- Test: `DeveloperQaProfileSessionTests.cs`

- [ ] **Step 1: Failing test — `scenario.run` / session begin uses QA profile and abort restores**

Use existing `IQaProfileService` fake/stub: begin marks QA slot active; abort clears without writing normal PlayerPrefs progress keys used by production saves.

- [ ] **Step 2: Run FAIL → implement thin delegation to `IQaProfileService` → PASS**

- [ ] **Step 3: Commit** `feat(qa): isolate DeveloperQa sessions on QA profile`

---

### Task 4: StudyRoom adapter — grant, reset, probe, assert-solved, capture

**Files:**
- Create: `disputatio/Assets/mokotan/mokotan/script/QA/SceneAdapters/StudyRoomQaAdapter.cs`
- Modify: `QaSceneAdapterRegistration.cs` to register StudyRoom
- Modify: `DeveloperQaService` to dispatch registered capability handlers
- Test: `StudyRoomQaAdapterTests.cs` (EditMode; mock/flowchart fixtures like `StudyRoomPuzzleDevToolTests`)

**Required capability IDs (subset):**
- `studyroom.mirror.grant-bookmark`
- `studyroom.mirror.reset`
- `studyroom.mirror.probe`
- `studyroom.mirror.assert-solved`
- `studyroom.mirror.capture`

Map onto `StudyRoomPuzzleDevTool` + `CaptureDebugInfo`. Do **not** treat force-solve as the PASS path for placement.

- [ ] **Step 1: Failing tests for registration + grant/reset/probe codes**

- [ ] **Step 2: Implement adapter handlers; register capabilities into `DeveloperQaCapabilityRegistry`**

- [ ] **Step 3: PASS + commit** `feat(qa): add StudyRoom mirror capability adapter`

---

### Task 5: Thin panel bridge

**Files:**
- Create: `DeveloperQaPanelBridge.cs`
- Modify: `InGameDeveloperOverlay.cs` StudyRoom QA buttons to call bridge (same payloads as CLI will use)
- Test: `DeveloperQaPanelBridgeTests.cs` — same command payload → same `DeveloperQaResultCode` as direct service call

- [ ] **Step 1–4:** TDD parity for grant + reset buttons
- [ ] **Step 5: Commit** `feat(qa): route StudyRoom developer panel through DeveloperQaService`

---

### Task 6: preset.before-placement + place-bookmark (real path)

**Files:**
- Extend `StudyRoomQaAdapter.cs`
- Optionally minimal product seam on `StudyRoomDiaryMirrorPuzzleController` / drop zone **only if required for observability**, committed separately as `fix`/`feat` product seam with no normal-runtime behavior change
- PlayMode: `StudyRoomMirrorCapabilityPlayModeTests.cs`

**Capabilities:**
- `studyroom.mirror.preset.before-placement`
- `studyroom.mirror.place-bookmark`

Placement must exercise the real BookmarkMirror drop/success route (reuse `FilterCardBookDropZone` / controller notify APIs). Force-solve is Recovery only, not scenario PASS.

- [ ] **Step 1: EditMode/PlayMode failing tests for preset + place**
- [ ] **Step 2: Implement; verify probe/assert after place**
- [ ] **Step 3: Commit QA capability** `feat(qa): add studyroom.mirror place and before-placement preset`
- [ ] **Step 4: If product seam required, separate commit** `fix(studyroom): expose placement seam for QA without changing player path`

---

### Task 7: Evidence capture under docs/qa/runs

**Files:**
- Bridge `evidence.capture` in `DeveloperQaService` to existing `IQaEvidenceRecorder` / `DevelopmentQaEvidenceRecorder`
- Ensure run directory layout:

```
docs/qa/runs/<UTC timestamp>-run-<id>/
  manifest.json
  journal.jsonl
  report.md
  console.log
  screenshots/
  patches/
```

- Test: `DeveloperQaEvidenceTests.cs`

- [ ] TDD → implement → commit `feat(qa): write DeveloperQa evidence into run directories`

---

### Task 8: CLI gateway + contract parity

**Files:**
- Create: `disputatio/Assets/Editor/QA/DeveloperQaCliBridge.cs`
- Test: `DeveloperQaCliPanelParityTests.cs`

Same JSON/command payload through CLI bridge and panel bridge → equal `Code` and state keys.

- [ ] TDD → implement → commit `feat(qa): expose DeveloperQaService to Unity CLI`

---

### Task 9: StudyRoom scenario JSON + scenario.* commands

**Files:**
- Create: `disputatio/Assets/Resources/QA/Scenarios/studyroom-mirror-diary.json`
- Wire `scenario.run|resume|cancel|status` on `DeveloperQaService` to existing `QaScenarioRunner` where possible
- Test: EditMode validator + PlayMode smoke for scenario load

Scenario steps (design §10):
1. isolated QA session
2. load StudyRoom + `before-placement`
3. grant BookmarkMirror
4. real place
5. capture placement/Fungus state
6. assert DiarySolved / key / gate
7. screenshot + Console delta
8. reset + repeat critical interaction via API
9. verdict + restore profile

- [ ] TDD → implement → commit `test(qa): add studyroom-mirror-diary scenario`

---

### Task 10: External autorun orchestrator skeleton (Python)

**Files:**
- `scripts/qa/autorun/classify.py`
- `scripts/qa/autorun/checkpoint.py`
- `scripts/qa/autorun/git_isolation.py`
- `scripts/qa/autorun/orchestrator.py`
- `scripts/qa/autorun/report.py`
- Tests under `scripts/qa/tests/`

**States:** PREFLIGHT → RUNNING → CLASSIFYING → PATCHING_QA|PATCHING_PRODUCT|BLOCKED → COMPILING → FOCUSED_TEST → REGRESSION_TEST → COMMITTING → RESUMING → PASS|FAIL|BLOCKED

**Classification table** from design §6. Max 3 attempts per normalized failure signature.

- [ ] **Step 1: pytest for classify MissingQaCapability vs ProductDefect vs EnvironmentBlocked vs InvalidScenario**

```python
def test_unknown_capability_is_missing_qa_capability():
    evidence = {"result_code": "MissingCapability", "missing_capability_id": "studyroom.mirror.place-bookmark"}
    assert classify(evidence) == "MissingQaCapability"
```

- [ ] **Step 2: pytest for owned-path rollback without `reset --hard`**

- [ ] **Step 3: pytest for third retry then BLOCKED**

- [ ] **Step 4: Implement modules; run `pytest scripts/qa/tests -q`**

- [ ] **Step 5: Commit** `feat(qa): add self-extending autorun orchestrator skeleton`

---

### Task 11: Release-configuration compile gate

**Files:**
- Test/editor script or asmdef define constraints proving `Godlotto.QA.Developer` types are unavailable / stripped outside editor/dev
- Follow existing `AssemblyInfo` / `#if UNITY_EDITOR || DEVELOPMENT_BUILD` pattern used by `Godlotto.QA.Core`

- [ ] Add EditMode or editor test documenting the gate
- [ ] Commit `test(qa): prove DeveloperQa unavailable outside editor/dev builds`

---

### Task 12: E2E vertical slice — missing capability repair loop

**Files:**
- Orchestrator integration test with fixture repo OR documented dry-run script
- Unity side: scenario that requests a capability intentionally unregistered in a test harness, expects `MissingCapability`, then after registry hot-register (simulating patch) resume succeeds

- [ ] Failing E2E test first
- [ ] Minimal loop: classify → apply fixture capability patch in temp worktree → focused test → resume → PASS evidence
- [ ] Commit `test(qa): e2e self-extend StudyRoom missing capability`

---

## Spec coverage checklist

| Design section | Tasks |
|---|---|
| §4 Common contract | 1, 5, 8 |
| §5 Capability registry / self-extension | 2, 4, 6, 12 |
| §6 Failure classification | 10 |
| §7 Repair state machine | 10, 12 |
| §8 Git isolation / commits | 10 (+ per-task commit messages) |
| §9 Anti-gaming | enforced in orchestrator + reviews |
| §10 StudyRoom slice | 4, 5, 6, 9, 12 |
| §11 Evidence | 7 |
| §12 Testing strategy | all tasks |
| Release gate | 11 |

---

## Execution notes for subagents

- Worktree path (controller sets): `.worktrees/qa-autorun-dev-mode` on branch `feature/self-extending-qa-autorun` from `codex/qa-driver`.
- Follow TDD: RED → verify fail → GREEN → verify pass → commit.
- After C# changes: `unity-cli` compile + filtered EditMode tests (see `.cursor/rules/unity-verification-postflight.mdc`).
- Do not push or open PR unless the user explicitly asks.
- Do not commit unrelated dirty assets (fonts, DOTween, fixture JSON).

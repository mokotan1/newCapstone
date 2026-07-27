# Multi-Room QA Autorun Wave 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Register Kitchen faucet and MainMenu start DeveloperQa capabilities (thin wraps of existing adapters), wire them through `DeveloperQaServiceFactory`, and add capability-style autorun scenarios plus EditMode tests.

**Architecture:** Mirror `StudyRoomQaAdapter.RegisterCapabilities`: each room adapter registers `{room}.{feature}.*` handlers that delegate to existing `ApplyPreset` / `TryClick` / `CaptureSnapshot`. Factory calls every `RegisterCapabilities`. No new puzzle rules; no force-solve PASS path.

**Tech Stack:** Unity 6000 C# (`UNITY_EDITOR || DEVELOPMENT_BUILD`), NUnit EditMode, existing `Godlotto.QA.*`, JSON scenarios under `Resources/QA/Scenarios/`.

**Spec:** `docs/superpowers/specs/2026-07-27-multi-room-qa-autorun-design.md`  
**Worktree:** `.worktrees/qa-autorun-dev-mode` on `feature/self-extending-qa-autorun`  
**Out of scope for this plan:** Wave 2 (Maid/Hall), Wave 3 (Child/Wife/Bed), TutorRoom.

---

## File Structure

| File | Role |
|------|------|
| `SceneAdapters/KitchenQaAdapter.cs` | Add capability constants + `RegisterCapabilities` + handlers |
| `SceneAdapters/MainMenuQaAdapter.cs` | Same for start-button slice |
| `SceneAdapters/DeveloperQaServiceFactory.cs` | Call Kitchen + MainMenu register |
| `Resources/QA/Scenarios/kitchen-faucet-autorun.json` | Capability invoke scenario |
| `Resources/QA/Scenarios/mainmenu-start-autorun.json` | Capability invoke scenario |
| `Editor/Tests/EditMode/QA/Developer/KitchenQaCapabilityTests.cs` | EditMode TDD |
| `Editor/Tests/EditMode/QA/Developer/MainMenuQaCapabilityTests.cs` | EditMode TDD |
| `Editor/Tests/EditMode/QA/Developer/DeveloperQaServiceFactoryMultiRoomTests.cs` | Factory lists kitchen + mainmenu ids |

---

### Task 1: Kitchen faucet capability registration

**Files:**
- Modify: `disputatio/Assets/mokotan/mokotan/script/QA/SceneAdapters/KitchenQaAdapter.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/QA/Developer/KitchenQaCapabilityTests.cs`

**Capability IDs (exact):**
- `kitchen.faucet.preset.before-faucet`
- `kitchen.faucet.click`
- `kitchen.faucet.probe`
- `kitchen.faucet.assert-clicked`
- `kitchen.faucet.capture`
- `kitchen.faucet.reset` (maps to `SetFaucetClicked(false)` via existing preset path)

- [ ] **Step 1: Write failing EditMode tests**

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;

public class KitchenQaCapabilityTests
{
    private static readonly string[] ExpectedIds =
    {
        "kitchen.faucet.preset.before-faucet",
        "kitchen.faucet.click",
        "kitchen.faucet.probe",
        "kitchen.faucet.assert-clicked",
        "kitchen.faucet.capture",
        "kitchen.faucet.reset"
    };

    [Test]
    public void RegisterCapabilities_ListsAllFaucetIds()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        KitchenQaAdapter.RegisterCapabilities(registry);
        var ids = registry.List().Select(c => c.Id).ToArray();
        CollectionAssert.IsSubsetOf(ExpectedIds, ids);
    }

    [Test]
    public async Task Describe_UnknownKitchenCap_ReturnsMissingCapability()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        KitchenQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "capability", "describe", "kitchen.faucet.missing"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.MissingCapability, result.Code);
    }

    [Test]
    public async Task Invoke_Click_WithoutKitchenScene_ReturnsEnvironmentBlocked()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        KitchenQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "interaction", "invoke", "kitchen.faucet.click"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
    }
}
#endif
```

- [ ] **Step 2: Run EditMode filter (or syntax check if no Unity)**

```powershell
.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter KitchenQaCapabilityTests
```

Expected: FAIL — `RegisterCapabilities` missing on `KitchenQaAdapter`.

- [ ] **Step 3: Implement `RegisterCapabilities` on `KitchenQaAdapter`**

Add constants and handlers that delegate to existing methods:

```csharp
public const string FaucetPresetCapabilityId = "kitchen.faucet.preset.before-faucet";
public const string FaucetClickCapabilityId = "kitchen.faucet.click";
public const string FaucetProbeCapabilityId = "kitchen.faucet.probe";
public const string FaucetAssertClickedCapabilityId = "kitchen.faucet.assert-clicked";
public const string FaucetCaptureCapabilityId = "kitchen.faucet.capture";
public const string FaucetResetCapabilityId = "kitchen.faucet.reset";

public static void RegisterCapabilities(DeveloperQaCapabilityRegistry registry)
{
    if (registry == null) throw new ArgumentNullException(nameof(registry));
    string sceneId = SceneNames.Kitchen;
    var adapter = new KitchenQaAdapter();

    registry.Register(new DeveloperQaCapability(FaucetPresetCapabilityId, sceneId, DeveloperQaCapabilityKind.Preset, "{}", "{faucetClicked:bool}"),
        _ => MapPreset(adapter.ApplyPreset(BeforeFaucetPresetId)));
    registry.Register(new DeveloperQaCapability(FaucetResetCapabilityId, sceneId, DeveloperQaCapabilityKind.Recovery, "{}", "{faucetClicked:bool}"),
        _ => MapPreset(adapter.ApplyPreset(BeforeFaucetPresetId)));
    registry.Register(new DeveloperQaCapability(FaucetClickCapabilityId, sceneId, DeveloperQaCapabilityKind.Interaction, "{}", "{clicked:bool}"),
        _ => MapClick(adapter, FaucetTargetId));
    registry.Register(new DeveloperQaCapability(FaucetProbeCapabilityId, sceneId, DeveloperQaCapabilityKind.Probe, "{}", "{faucetClicked:bool}"),
        _ => MapSnapshot(adapter, assertClicked: false));
    registry.Register(new DeveloperQaCapability(FaucetCaptureCapabilityId, sceneId, DeveloperQaCapabilityKind.Probe, "{}", "{faucetClicked:bool}"),
        _ => MapSnapshot(adapter, assertClicked: false));
    registry.Register(new DeveloperQaCapability(FaucetAssertClickedCapabilityId, sceneId, DeveloperQaCapabilityKind.Assertion, "{}", "{faucetClicked:bool}"),
        _ => MapSnapshot(adapter, assertClicked: true));
}

// MapPreset: Success -> Ok; Failed/Unknown -> EnvironmentBlocked
// MapClick: TryClick true -> Ok; false -> EnvironmentBlocked with error message
// MapSnapshot: copy CaptureSnapshot values into Data; if assertClicked and faucetClicked!=true -> AssertionFailed
```

Add `using Godlotto.QA.Developer;`. Keep existing `IQaSceneAdapter` behavior unchanged.

- [ ] **Step 4: Re-run tests — expect PASS (or syntax OK + DONE_WITH_CONCERNS)**

- [ ] **Step 5: Commit**

```powershell
git add disputatio/Assets/mokotan/mokotan/script/QA/SceneAdapters/KitchenQaAdapter.cs disputatio/Assets/Editor/Tests/EditMode/QA/Developer/KitchenQaCapabilityTests.cs*
git commit -m "feat(qa): add kitchen.faucet DeveloperQa capabilities"
```

---

### Task 2: MainMenu start capability registration

**Files:**
- Modify: `disputatio/Assets/mokotan/mokotan/script/QA/SceneAdapters/MainMenuQaAdapter.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/QA/Developer/MainMenuQaCapabilityTests.cs`

**Capability IDs:**
- `mainmenu.start.click`
- `mainmenu.start.probe`
- `mainmenu.start.assert-invoked`
- `mainmenu.start.capture`

Note: `assert-invoked` in EditMode without scene should `EnvironmentBlocked` or, if snapshot shows `mainMenuFound=false`, `AssertionFailed` when asserting found — prefer: click without MainMenu → `EnvironmentBlocked`; assert when `mainMenuFound!=true` → `AssertionFailed`.

- [ ] **Step 1: Failing tests** (same pattern as Kitchen — list ids, MissingCapability, click without scene → EnvironmentBlocked)

- [ ] **Step 2: Run — expect missing `RegisterCapabilities`**

- [ ] **Step 3: Implement `MainMenuQaAdapter.RegisterCapabilities`**

```csharp
public const string StartClickCapabilityId = "mainmenu.start.click";
public const string StartProbeCapabilityId = "mainmenu.start.probe";
public const string StartAssertInvokedCapabilityId = "mainmenu.start.assert-invoked";
public const string StartCaptureCapabilityId = "mainmenu.start.capture";

public static void RegisterCapabilities(DeveloperQaCapabilityRegistry registry)
{
    // register four caps; click delegates to TryClick(StartButtonTargetId)
    // probe/capture map CaptureSnapshot
    // assert-invoked: AssertionFailed unless mainMenuFound == "True" AFTER a successful click is not required in EditMode;
    //   assert that Data contains mainMenuFound key; if value is not True -> AssertionFailed
}
```

- [ ] **Step 4: PASS**

- [ ] **Step 5: Commit** `feat(qa): add mainmenu.start DeveloperQa capabilities`

---

### Task 3: Factory multi-room registration

**Files:**
- Modify: `disputatio/Assets/mokotan/mokotan/script/QA/SceneAdapters/DeveloperQaServiceFactory.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/QA/Developer/DeveloperQaServiceFactoryMultiRoomTests.cs`

- [ ] **Step 1: Failing test**

```csharp
[Test]
public void Create_RegistersStudyRoomKitchenAndMainMenuCapabilities()
{
    IDeveloperQaService service = DeveloperQaServiceFactory.Create();
    var ids = service.ListCapabilities().Select(c => c.Id).ToArray();
    Assert.That(ids, Does.Contain("studyroom.mirror.probe"));
    Assert.That(ids, Does.Contain("kitchen.faucet.click"));
    Assert.That(ids, Does.Contain("mainmenu.start.click"));
}
```

- [ ] **Step 2: Run — expect kitchen/mainmenu ids missing**

- [ ] **Step 3: Update factory**

```csharp
StudyRoomQaAdapter.RegisterCapabilities(registry);
KitchenQaAdapter.RegisterCapabilities(registry);
MainMenuQaAdapter.RegisterCapabilities(registry);
```

Update factory XML doc to say multi-room, not StudyRoom-only.

- [ ] **Step 4: PASS**

- [ ] **Step 5: Commit** `feat(qa): register Kitchen and MainMenu capabilities in factory`

---

### Task 4: Kitchen and MainMenu autorun scenario JSON

**Files:**
- Create: `disputatio/Assets/Resources/QA/Scenarios/kitchen-faucet-autorun.json`
- Create: `disputatio/Assets/Resources/QA/Scenarios/mainmenu-start-autorun.json`
- Modify or add tests in `DeveloperQaScenarioTests.cs` (or new `MultiRoomAutorunScenarioTests.cs`) to load/validate both

- [ ] **Step 1: Failing test — Resources load by id**

```csharp
[Test]
public void KitchenFaucetAutorun_Json_LoadsAndHasClickStep()
{
    TextAsset json = Resources.Load<TextAsset>("QA/Scenarios/kitchen-faucet-autorun");
    Assert.IsNotNull(json);
    Assert.IsTrue(json.text.Contains("kitchen.faucet.click"));
}
```

- [ ] **Step 2: Run — missing asset**

- [ ] **Step 3: Write JSON** (studyroom-mirror-diary shape)

`kitchen-faucet-autorun.json`:

```json
{
  "schemaVersion": 1,
  "id": "kitchen-faucet-autorun",
  "scene": "Kitchen",
  "steps": [
    { "id": "preset", "family": "preset", "name": "apply", "targetId": "kitchen.faucet.preset.before-faucet" },
    { "id": "click", "family": "interaction", "name": "invoke", "targetId": "kitchen.faucet.click" },
    { "id": "probe", "family": "interaction", "name": "invoke", "targetId": "kitchen.faucet.probe" },
    { "id": "assert", "family": "interaction", "name": "invoke", "targetId": "kitchen.faucet.assert-clicked" },
    { "id": "capture", "family": "interaction", "name": "invoke", "targetId": "kitchen.faucet.capture" },
    { "id": "evidence", "family": "evidence", "name": "capture" }
  ]
}
```

`mainmenu-start-autorun.json`:

```json
{
  "schemaVersion": 1,
  "id": "mainmenu-start-autorun",
  "scene": "MainMenuScene",
  "steps": [
    { "id": "click", "family": "interaction", "name": "invoke", "targetId": "mainmenu.start.click" },
    { "id": "probe", "family": "interaction", "name": "invoke", "targetId": "mainmenu.start.probe" },
    { "id": "assert", "family": "interaction", "name": "invoke", "targetId": "mainmenu.start.assert-invoked" },
    { "id": "capture", "family": "interaction", "name": "invoke", "targetId": "mainmenu.start.capture" },
    { "id": "evidence", "family": "evidence", "name": "capture" }
  ]
}
```

Unity `.meta` for TextAssets after refresh if generated.

- [ ] **Step 4: PASS load tests**

- [ ] **Step 5: Commit** `test(qa): add kitchen and mainmenu capability autorun scenarios`

---

### Task 5: CLI/panel parity smoke for new ids

**Files:**
- Modify: `disputatio/Assets/Editor/Tests/EditMode/QA/Developer/DeveloperQaCliPanelParityTests.cs` (or add cases)

- [ ] **Step 1: Add failing parity cases** for `kitchen.faucet.probe` and `mainmenu.start.probe` describe/list — CLI bridge vs panel bridge same `ResultCode`

- [ ] **Step 2–4:** Implement if bridge already uses factory (should already pass once Task 3 done); fix only if not

- [ ] **Step 5: Commit** `test(qa): extend CLI panel parity to kitchen and mainmenu`

---

### Task 6: Wave 1 gate checklist + pytest regression

- [ ] **Step 1: Run** `python -m pytest scripts/qa/tests -q` — expect all green

- [ ] **Step 2: Write** `docs/qa/wave-1-completion.md` listing factory ids, scenario paths, Unity EditMode status

- [ ] **Step 3: Commit** `docs(qa): record multi-room Wave 1 completion status`

---

## Spec coverage (Wave 1)

| Spec item | Task |
|-----------|------|
| Kitchen faucet capability set | 1, 4 |
| MainMenu start capability set | 2, 4 |
| Factory registers all | 3 |
| CLI/panel parity | 5 |
| EditMode registration / MissingCapability / EnvironmentBlocked | 1, 2 |
| Scenarios under Resources/QA/Scenarios | 4 |
| No force-solve | Handlers only use ApplyPreset/TryClick/CaptureSnapshot |
| Wave 2/3 | Deferred — separate plan |

## Execution notes

- Work only in `.worktrees/qa-autorun-dev-mode`
- TDD: RED → GREEN → commit per task
- Do not push unless asked
- Do not commit `scripts/CSharpSyntaxChecker/bin/**`
- If Unity missing: CSharpSyntaxChecker + DONE_WITH_CONCERNS

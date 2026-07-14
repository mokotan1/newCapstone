# Hall, Kitchen, Maid Room, and Study Regression Fixes Implementation Plan

> **Status (2026-07-14):** Tasks 1–8 code/scene/test changes are present in the working tree; Task 9 (EditMode run + manual playtest) remains open. Local EditMode was blocked (`unity-cli: no Unity instance found for project: disputatio`). QA checklist: `docs/qa/2026-07-14-regression-playtest.md`. Plan step checkboxes below are left unchecked until each step is verified in-session.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve the eight reported gameplay regressions, add automated guards for state and Unity-scene wiring, and complete a focused end-to-end playtest pass.

**Architecture:** Keep the existing `RoomInteractionController`, Fungus Flowchart, and quest HUD architecture. Move only failure-prone state decisions into small testable C# policies, keep scene-only behavior in scene YAML with serialization tests, and treat PlayerPrefs, Fungus global variables, and `DontDestroyOnLoad` objects as separate reset layers.

**Tech Stack:** Unity 6000.0.36f1, C#, Unity Test Framework/NUnit, Fungus, Unity UI/TMP, UnityWebRequest.

## Global Constraints

- Preserve BGM, SFX, fullscreen, and resolution preferences when starting a new game.
- Do not change unrelated user modifications in fonts, image metadata, or generated test binaries.
- Use the existing Elastic IP endpoint through `ServerConfig`; do not add another scene-local endpoint source.
- Every scene reference change must have an EditMode YAML/serialization regression test.
- Every gameplay fix must pass a clean-start path and a close/reopen or revisit path where applicable.

---

## Current Evidence and Root-Cause Classification

| Issue | Current evidence | Classification |
|---|---|---|
| Cheshire timeout freezes interaction | `ChatHttpClient` uses 60/120 second timeouts, but request state is cleared only after response handling, not by an outer `finally`. Endpoint values are duplicated in config, binder, prefab, and scenes. The current endpoint is reachable: TCP/HTTP connect succeeded and GET `/chat` returned 405, confirming a live POST-only route. | Client recovery and configuration-drift risk remain. |
| Cleared quest remains top-right | `TutorialQuestProgressAdapter.GetNextQuestId` returns a next quest only after `LightTheManor`. `QuestTrackerHudController.HandleQuestCleared` has no final-quest dismissal branch. | Confirmed root cause. |
| New game retains previous progress | `MainMenu.OnStartButton` already clears PlayerPrefs, but `InGameSettingsPanel` explicitly preserves Fungus `GlobalVariables` and `Variablemanager` while returning to the menu. Those are in-memory gameplay state outside PlayerPrefs. | Partial fix; runtime state survives. |
| Kitchen sink immediately says no more business | Kitchen `Sink` Flowchart command `290854250` branches on `FaucetClicked` (`290854235`) before the final dialogue `290854224`. It should branch on `HaveMaidKey` (`290853994`). The obsolete key Button route to `filled_bottle` was already removed in commit `1bc74719`. | Confirmed Flowchart condition bug; click interception already fixed. |
| Maid food effect remains | The current `food` block already has enabled Set Active(false) command `285511247` targeting stripped `FoodItemEffect` object `1934550032`. There is no regression test locking this assignment. | Current scene appears fixed; test coverage missing. |
| `PuzzleBook_SelectYes` Set Active error | Commands `285511175` and `285511291` are disabled; opening is already handled by `MaidRoomPuzzleController` block outcome. | Redundant broken Flowchart commands should be removed. |
| Cookbook page 4/orange screen | `BookPanelController.pages` contains only pages 1-3 and orphan page 4 is deactivated in `Awake`, but the shared `RightClickArea` stays active at the last page. | Boundary UI state is not represented; add an explicit last-page navigation state. |
| Study mirror disappears after reopen | `FilterCardBookDropZone.RestoreDiaryMirrorDropPanel` explicitly hides and deactivates the mirror on `OnEnable`, even after a successful drop. | Confirmed lifecycle bug. |
| BookCase2 exposes prison button first | `BlueMidButton` is initially inactive. The `Start` false branch only sets it interactable, while the true branch restores the post-click state. `PrisonButton` starts active. The Blue button sprite is currently assigned. | Confirmed initial-state Flowchart bug; asset assignment is present. |

## Recommended Approach

Use minimal state-boundary fixes with automated guards. A scene-only approach would be faster but would leave timeout recovery and new-game runtime state unprotected; a broad quest/inventory/Flowchart rewrite would be too risky for this regression batch.

---

### Task 1: Cheshire timeout recovery and single-source endpoint

**Files:**
- Create: `disputatio/Assets/mokotan/mokotan/script/AI/ChatRequestRecoveryPolicy.cs`
- Modify: `disputatio/Assets/mokotan/mokotan/script/AI/ChatHttpClient.cs`
- Modify: `disputatio/Assets/mokotan/mokotan/script/AI/ParretPanelChatbotBinder.cs`
- Modify: `disputatio/Assets/godlotto/KTH/Parret_Panel.prefab`
- Modify: chatbot-bearing scene overrides under `disputatio/Assets/Scenes/Mokotan/`
- Modify: `disputatio/Assets/Resources/Scenario/cheshire_ui_strings.csv`
- Test: `disputatio/Assets/Editor/Tests/EditMode/AI/ChatRequestRecoveryPolicyTests.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/AI/ChatHttpClientTests.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/Config/ServerConfigTests.cs`

**Interfaces:**
- Produces: `ChatRequestRecoveryPolicy.ShouldRetry(UnityWebRequest.Result result, long responseCode, int attempt)`.
- Produces: one automatic retry for connection errors/timeouts and 408/429/5xx responses.
- Guarantees: `IChatHttpCallbacks.IsRequestInProgress` and `InteractionInputGate` are released on success, timeout, parse failure, response-handler exception, coroutine disposal, and object destruction.

- [ ] **Step 1: Add failing retry-policy tests**

```csharp
[TestCase(UnityWebRequest.Result.ConnectionError, 0, 0, true)]
[TestCase(UnityWebRequest.Result.ProtocolError, 503, 0, true)]
[TestCase(UnityWebRequest.Result.ProtocolError, 400, 0, false)]
[TestCase(UnityWebRequest.Result.ConnectionError, 0, 1, false)]
public void ShouldRetry_OnlyRetriesOneTransientFailure(
    UnityWebRequest.Result result, long code, int attempt, bool expected)
{
    Assert.AreEqual(expected, ChatRequestRecoveryPolicy.ShouldRetry(result, code, attempt));
}
```

- [ ] **Step 2: Run the new test and confirm it fails because the policy does not exist**

Run Unity EditMode with test filter `ChatRequestRecoveryPolicyTests`.

Expected: FAIL at compilation or test discovery because `ChatRequestRecoveryPolicy` is missing.

- [ ] **Step 3: Implement one-retry classification**

```csharp
public static bool ShouldRetry(UnityWebRequest.Result result, long responseCode, int attempt)
{
    if (attempt >= 1) return false;
    if (result == UnityWebRequest.Result.ConnectionError) return true;
    return responseCode == 408 || responseCode == 429 || responseCode >= 500;
}
```

- [ ] **Step 4: Refactor both chat coroutines around an outer cleanup boundary**

Use one request factory per attempt, wait 0.5 seconds realtime before the single retry, show the localized reconnect message, and place this guarantee around the entire operation:

```csharp
_host.IsRequestInProgress = true;
try
{
    yield return SendWithOneRetry(...);
    yield return _host.StartHostCoroutine(
        _host.HandleChatbotResponse(responseText, functionCalls));
}
finally
{
    _host.OnChatHttpWaitFinished();
    _host.IsRequestInProgress = false;
}
```

Call `OnChatHttpWaitStarted/Finished` exactly once for the logical request, not once per retry.

- [ ] **Step 5: Remove duplicated default endpoint overrides**

Set `ParretPanelChatbotBinder.localServerUrlOverride` to an empty string and clear equivalent serialized prefab/scene overrides. `BaseChatbot` must resolve `ServerConfig.GetOrCreate().ChatUrl`, whose expected default remains `http://54.156.51.119:8000/chat`.

- [ ] **Step 6: Add transport cleanup tests**

Extend `ChatHttpClientTests` with a fake request-execution seam so a timeout followed by success asserts one retry, while two failures assert a localized error and `IsRequestInProgress == false`.

- [ ] **Step 7: Verify live connectivity without consuming a gameplay turn**

Run:

```powershell
curl.exe -sS -o NUL -w "http_code=%{http_code} connect=%{time_connect}s total=%{time_total}s`n" --connect-timeout 5 --max-time 10 http://54.156.51.119:8000/chat
```

Expected: network connect succeeds and `/chat` returns 405 for GET. Then perform one POST smoke test only in the development environment with a non-progress-changing prompt.

- [ ] **Step 8: Commit the isolated transport change**

```powershell
git add disputatio/Assets/mokotan/mokotan/script/AI disputatio/Assets/godlotto/Script/Config disputatio/Assets/Editor/Tests/EditMode/AI disputatio/Assets/Editor/Tests/EditMode/Config disputatio/Assets/godlotto/KTH/Parret_Panel.prefab disputatio/Assets/Scenes/Mokotan disputatio/Assets/Resources/Scenario/cheshire_ui_strings.csv
git commit -m "fix: recover Cheshire chat after transient timeout"
```

### Task 2: Quest completion transition and final HUD dismissal

**Files:**
- Modify: `disputatio/Assets/godlotto/Script/Quest/QuestTrackerHudController.cs`
- Modify: `disputatio/Assets/godlotto/Script/Quest/QuestTrackerHudView.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/UI/QuestTrackerHudTests.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/UI/TutorialQuestProgressAdapterTests.cs`

**Interfaces:**
- Consumes: `TutorialQuestProgressAdapter.GetNextQuestId(string clearedQuestId)`.
- Produces: a single `HandleQuestCleared` path used by both `AdvanceStep` and `TryCompleteTutorialStep`.
- Guarantees: next quest crossfades when configured; otherwise the completed HUD fades out and becomes inactive.

- [ ] **Step 1: Add a failing final-quest HUD test**

Create a Unity coroutine test that presents `BottleKey`, completes all three steps, calls the clear handler with zero delay, and asserts the HUD root becomes inactive. Keep the existing first-quest crossfade test to prove `LightTheManor` still advances to `BottleKey`.

- [ ] **Step 2: Consolidate clear handling**

Resolve the next quest inside `HandleQuestCleared` instead of only inside `TryCompleteTutorialStep`:

```csharp
string nextQuestId = TutorialQuestProgressAdapter.GetNextQuestId(trackerState.CurrentQuestId);
if (!string.IsNullOrWhiteSpace(nextQuestId))
    QueueCrossfadeToQuest(nextQuestId);
else
    StartFinalDismiss(QuestTrackerStylePalette.CrossfadeDelayAfterClearSeconds);
```

- [ ] **Step 3: Implement final dismissal**

Reuse the existing unscaled fade duration. After alpha reaches zero, deactivate `hudView.gameObject`; do not clear completed quest state until a new game reset.

- [ ] **Step 4: Run quest tests**

Expected: first tutorial quest crossfades to the second; final tutorial quest shows completion briefly and disappears; no active step remains after completion.

- [ ] **Step 5: Commit**

```powershell
git add disputatio/Assets/godlotto/Script/Quest disputatio/Assets/Editor/Tests/EditMode/UI/QuestTrackerHudTests.cs disputatio/Assets/Editor/Tests/EditMode/UI/TutorialQuestProgressAdapterTests.cs
git commit -m "fix: dismiss quest HUD after final tutorial mission"
```

### Task 3: Complete new-game reset across persisted and in-memory state

**Files:**
- Modify: `disputatio/Assets/godlotto/Script/InGameSettingsPanel.cs`
- Modify: `disputatio/Assets/godlotto/Script/MainMenu.cs`
- Modify: `disputatio/Assets/godlotto/Script/PlayDataPrefsCleaner.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/UI/InGameSettingsPanelCleanupPolicyTests.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/PlayDataPrefsCleanerTests.cs`
- Create: `disputatio/Assets/Editor/Tests/EditMode/UI/MainMenuNewGameResetTests.cs`

**Interfaces:**
- Consumes: `Fungus.SaveManagerSignals.DoSaveReset()`.
- Guarantees: return-to-menu destroys gameplay global-variable roots, while `GlobalSettingManager` survives; Start clears PlayerPrefs and broadcasts runtime reset before the `StartButton` Flowchart loads the opening scene.

- [ ] **Step 1: Change the cleanup-policy test to expose the stale-runtime-state bug**

```csharp
Assert.IsTrue(InGameSettingsPanel.ShouldPreserveDontDestroyRoot(globalSettingsObject, settingsPanelObject));
Assert.IsFalse(InGameSettingsPanel.ShouldPreserveDontDestroyRoot(globalVariablesObject, settingsPanelObject));
Assert.IsFalse(InGameSettingsPanel.ShouldPreserveDontDestroyRoot(variableManagerObject, settingsPanelObject));
```

Expected before implementation: FAIL because both gameplay roots are currently preserved.

- [ ] **Step 2: Narrow the preservation policy**

Keep only the current settings object and `GlobalSettingManager`. Remove the `GlobalVariables` component and `Variablemanager` name exceptions so a return to the main menu cannot carry Fungus gameplay flags into a new run.

- [ ] **Step 3: Broadcast the standard Fungus reset from the new-game path**

After `PlayDataPrefsCleaner.ClearProgressPreserveAudioVideoSettings()` and before the menu Flowchart continues, call:

```csharp
Fungus.SaveManagerSignals.DoSaveReset();
InventorySlot.ClearDragState();
```

This clears still-live reset subscribers and transient drag UI even when the player reaches the menu through a nonstandard path.

- [ ] **Step 4: Add menu wiring and settings-preservation tests**

Verify `MainMenuScene.unity` Start button invokes `MainMenu.OnStartButton` before `Flowchart.ExecuteBlock("StartButton")`. Verify junk progress keys and `LastBookPage_*` are removed while BGM, SFX, fullscreen, and resolution remain.

- [ ] **Step 5: Run reset tests and a two-run PlayMode smoke test**

Run 1: obtain any item and advance one tutorial step, return to main menu, click Start. Run 2 must begin with empty inventory, initial quest, closed overlays, initial Fungus flags, and preserved audio/display settings.

- [ ] **Step 6: Commit**

```powershell
git add disputatio/Assets/godlotto/Script/InGameSettingsPanel.cs disputatio/Assets/godlotto/Script/MainMenu.cs disputatio/Assets/godlotto/Script/PlayDataPrefsCleaner.cs disputatio/Assets/Editor/Tests/EditMode
git commit -m "fix: reset runtime gameplay state on new game"
```

### Task 4: Kitchen key pickup and finished-dialogue condition

**Files:**
- Modify: `disputatio/Assets/Scenes/Mokotan/First Floor/1foorLeft/Kitchen.unity`
- Test: `disputatio/Assets/Editor/Tests/EditMode/UI/KitchenSinkCompletionDialogueTests.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/UI/KitchenAddKeyFlowTests.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/UI/FaucetKeyReleaseControllerTests.cs`

**Interfaces:**
- Consumes: Fungus `HaveMaidKey` variable file ID `290853994`.
- Guarantees: `FaucetClicked` can reveal/fill the bottle without entering the final sink dialogue; final dialogue is reachable only after the key pickup sets `HaveMaidKey` true.

- [ ] **Step 1: Add a YAML regression test for the Sink block**

Parse the `Sink` Flowchart block and assert the `더이상 볼 일은 없는 것 같다.` Say command is inside an If branch that references `HaveMaidKey`, not `FaucetClicked`.

- [ ] **Step 2: Run the test and confirm the current failure**

Expected: FAIL because command `290854250` references variable `290854235` (`FaucetClicked`).

- [ ] **Step 3: Correct the Flowchart condition in Unity**

In `Kitchen` > Flowchart > `Sink`, change the final-dialogue If variable from `FaucetClicked` to `HaveMaidKey`. Preserve the earlier bottle/faucet branches and the existing `addKey` block.

- [ ] **Step 4: Preserve the already-correct key click path**

Keep `MaidRoomKey` without a `Button` and without a `filled_bottle` route. Pickup must continue through `ItemPickup`, which sets `HaveMaidKey` only on actual player click.

- [ ] **Step 5: Run Kitchen tests**

Expected: key is visible/clickable above the overlay, `addKey` does not mark it acquired, and the final dialogue is gated only by `HaveMaidKey`.

- [ ] **Step 6: Commit**

```powershell
git add 'disputatio/Assets/Scenes/Mokotan/First Floor/1foorLeft/Kitchen.unity' disputatio/Assets/Editor/Tests/EditMode/UI/KitchenSinkCompletionDialogueTests.cs disputatio/Assets/Editor/Tests/EditMode/UI/KitchenAddKeyFlowTests.cs disputatio/Assets/Editor/Tests/EditMode/UI/FaucetKeyReleaseControllerTests.cs
git commit -m "fix: gate kitchen sink completion on key pickup"
```

### Task 5: Maid Room item-effect references and obsolete PuzzleBook commands

**Files:**
- Modify: `disputatio/Assets/Scenes/Mokotan/First Floor/1floorRight/MaidRoom.unity`
- Create: `disputatio/Assets/Editor/Tests/EditMode/UI/MaidRoomSceneFlowTests.cs`
- Modify: `disputatio/Assets/Editor/Tests/EditMode/UI/MaidRoomPuzzleControllerTests.cs`

**Interfaces:**
- Consumes: `FoodItemEffect` stripped GameObject file ID `1934550032`.
- Consumes: `MaidRoomPuzzleController` block outcome for `PuzzleBook_SelectYes`.
- Guarantees: acquiring food deactivates both the pickup object and its effect; opening the puzzle book has one owner.

- [ ] **Step 1: Add scene-flow tests**

Assert the `food` block contains an enabled Set Active(false) command targeting `1934550032`. Assert `PuzzleBook_SelectYes` does not depend on disabled Set Active commands.

- [ ] **Step 2: Verify the food reference in the Inspector**

Open `MaidRoom`, select Flowchart block `food`, and confirm Set Active command `285511247` points to the visible `FoodItemEffect` instance. Reassign it through the object picker if Unity reports a missing reference so the YAML keeps `gameObjectVal: {fileID: 1934550032}`.

- [ ] **Step 3: Remove redundant disabled commands from `PuzzleBook_SelectYes`**

Remove command references `285511175` and `285511291` from the block. Keep `MaidRoomPuzzleController.blockOutcomes` entry that opens puzzle panel `1407025573`; it is already covered by `OnBlockEnd_CookBookSelectYes_OpensPanelAndHidesDiary`-style controller tests.

- [ ] **Step 4: Run Maid Room tests**

Expected: food acquisition deactivates the item and effect; puzzle book Yes opens exactly one panel without missing-reference console errors.

- [ ] **Step 5: Commit**

```powershell
git add 'disputatio/Assets/Scenes/Mokotan/First Floor/1floorRight/MaidRoom.unity' disputatio/Assets/Editor/Tests/EditMode/UI/MaidRoomSceneFlowTests.cs disputatio/Assets/Editor/Tests/EditMode/UI/MaidRoomPuzzleControllerTests.cs
git commit -m "fix: stabilize Maid Room pickup and puzzle book flow"
```

### Task 6: Cookbook last-page navigation boundary

**Files:**
- Modify: `disputatio/Assets/godlotto/Script/BookPanelController.cs`
- Modify: `disputatio/Assets/Scenes/Mokotan/First Floor/1floorRight/MaidRoom.unity`
- Create: `disputatio/Assets/Editor/Tests/EditMode/UI/BookPanelNavigationBoundsTests.cs`
- Modify: `disputatio/Assets/Editor/Tests/EditMode/UI/BookPanelControllerAutoMapTests.cs`

**Interfaces:**
- Produces: serialized `GameObject nextPageClickArea` and `GameObject previousPageClickArea` bindings.
- Guarantees: the right click area is active/interactable only when `CurrentPageIndex < PageCount - 1`; the fourth orphan page is never part of `pages`.

- [ ] **Step 1: Add failing boundary tests**

At page index 0, previous is disabled and next is enabled. At page index 2 with three pages, next is disabled. Reopening with a stale saved index of 3 clamps both display and `currentPageIndex` to 2.

- [ ] **Step 2: Clamp the stored index on enable**

```csharp
currentPageIndex = Mathf.Clamp(PlayerPrefs.GetInt(PREF_KEY, 0), 0, PageCount - 1);
ShowPage(currentPageIndex);
RefreshNavigationAreas();
```

- [ ] **Step 3: Refresh navigation after every page change**

Call `RefreshNavigationAreas()` after immediate display and at the end of an animated turn. Disable the right area on the last page instead of allowing a visible no-op click target.

- [ ] **Step 4: Bind the Maid Room click areas**

Assign `RightClickArea` file ID `1164327270` to `nextPageClickArea`. Keep `CookBookPage4` excluded from `pages`; verify `DeactivateOrphanPageChildren` leaves it inactive.

- [ ] **Step 5: Run book tests and inspect page 3**

Expected: page 3 remains visible, the right half does not respond, no orange page/background appears, and left navigation still returns to page 2.

- [ ] **Step 6: Commit**

```powershell
git add disputatio/Assets/godlotto/Script/BookPanelController.cs 'disputatio/Assets/Scenes/Mokotan/First Floor/1floorRight/MaidRoom.unity' disputatio/Assets/Editor/Tests/EditMode/UI/BookPanelNavigationBoundsTests.cs disputatio/Assets/Editor/Tests/EditMode/UI/BookPanelControllerAutoMapTests.cs
git commit -m "fix: disable cookbook navigation at final page"
```

### Task 7: Study mirror restoration after panel reopen

**Files:**
- Modify: `disputatio/Assets/godlotto/Script/DropZone/FilterCardBookDropZone.cs`
- Modify: `disputatio/Assets/Editor/Tests/EditMode/UI/FilterCardBookDropZoneTests.cs`
- Verify: `disputatio/Assets/Scenes/Mokotan/First Floor/1floorRight/StudyRoom.unity`

**Interfaces:**
- Produces: `bool hasPlacedMirror` runtime state for the current room session.
- Guarantees: a placed unsolved mirror card is restored on panel reopen; an unused panel still starts without the mirror.

- [ ] **Step 1: Add failing reopen tests**

Drop `BookmarkMirror`, disable and re-enable `CardStackPanel`, invoke `OnEnable`, and assert `FilterCardImage.activeSelf == true`. Add a control test proving it remains false before the first drop.

- [ ] **Step 2: Record placement state on successful drop**

Set `hasPlacedMirror = true` immediately after the mirror image is activated and the drop is accepted.

- [ ] **Step 3: Restore instead of unconditionally hiding**

In `RestoreDiaryMirrorDropPanel`, activate and reset the mirror when `hasPlacedMirror` is true; hide it only when false. Rebind drag/rotation components and notify `diaryMirrorPuzzleController` so the puzzle remains interactive after reopen.

- [ ] **Step 4: Verify scene binding**

Confirm the active Study Room drop-zone component points `filterCardObject` to `FilterCardImage` and the inactive legacy duplicate component remains disabled.

- [ ] **Step 5: Run tests and manual close/reopen sequence**

Expected: mirror is visible and movable after every close/reopen until the puzzle resolves; inventory consumption does not prevent restoring the already-placed visual.

- [ ] **Step 6: Commit**

```powershell
git add disputatio/Assets/godlotto/Script/DropZone/FilterCardBookDropZone.cs disputatio/Assets/Editor/Tests/EditMode/UI/FilterCardBookDropZoneTests.cs
git commit -m "fix: restore Study mirror after panel reopen"
```

### Task 8: BookCase2 clean-entry state

**Files:**
- Modify: `disputatio/Assets/Scenes/Mokotan/First Floor/1floorRight/BookCase2.unity`
- Modify: `disputatio/Assets/Editor/Tests/EditMode/UI/BookCase2StateRestoreTests.cs`

**Interfaces:**
- Consumes: `ButtonClicked` variable file ID `687597805`.
- Guarantees: false branch shows/enables `BlueMidButton` and hides `PrisonButton`; true branch hides Blue and shows/enables Prison.

- [ ] **Step 1: Extend the scene test for the fresh branch**

Before the Else command, assert the command list contains Set Active(true) for Blue file ID `2143532138`, Set Interactable(true) for Blue, and Set Active(false) for Prison file ID `823372011`.

- [ ] **Step 2: Run the test and confirm failure**

Expected: FAIL because the current false branch only sets Blue interactable while Blue starts inactive and Prison starts active.

- [ ] **Step 3: Fix the Start Flowchart branch**

For `ButtonClicked == false`, execute in order: Blue active true, Blue interactable true, Prison active false. Preserve the existing Else branch that restores Prison after Blue has already been clicked.

- [ ] **Step 4: Verify the image asset**

Confirm Blue’s Image retains sprite GUID `55fb7eae75f3b8d488ba7d796b3fad3a`; no new image import is needed unless the sprite is visually incorrect.

- [ ] **Step 5: Run BookCase2 tests and both entry paths**

Expected clean entry: Blue visible/clickable, Prison hidden. Expected revisit after click: Blue hidden, Prison visible/clickable.

- [ ] **Step 6: Commit**

```powershell
git add 'disputatio/Assets/Scenes/Mokotan/First Floor/1floorRight/BookCase2.unity' disputatio/Assets/Editor/Tests/EditMode/UI/BookCase2StateRestoreTests.cs
git commit -m "fix: restore BookCase2 initial button state"
```

### Task 9: Automated regression suite and actual-play acceptance pass

**Files:**
- Create: `docs/qa/2026-07-14-regression-playtest.md`
- Test results: `disputatio/TestResults/2026-07-14-editmode.xml`
- Test log: `disputatio/TestResults/2026-07-14-editmode.log`

**Interfaces:**
- Consumes: all fixes from Tasks 1-8.
- Produces: a signed checklist with build, test, console, and gameplay evidence.

- [ ] **Step 1: Resolve the local Unity test-runner prerequisite**

The baseline run did not reach tests because Windows failed to launch `Unity.ILPP.Trigger.exe` with error 1455 (insufficient page-file/commit memory). Increase available virtual memory or close memory-heavy processes, delete only Unity-generated `Library/Bee` artifacts if Unity recommends it, then rerun from a clean editor state.

- [ ] **Step 2: Run focused EditMode tests**

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.36f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\Users\user\Documents\GitHub\newCapstone\disputatio' -runTests -testPlatform EditMode -testFilter 'ChatRequestRecoveryPolicyTests;ChatHttpClientTests;QuestTrackerHudTests;PlayDataPrefsCleanerTests;InGameSettingsPanelCleanupPolicyTests;KitchenSinkCompletionDialogueTests;KitchenAddKeyFlowTests;MaidRoomSceneFlowTests;BookPanelNavigationBoundsTests;FilterCardBookDropZoneTests;BookCase2StateRestoreTests' -testResults 'C:\Users\user\Documents\GitHub\newCapstone\disputatio\TestResults\2026-07-14-editmode.xml' -logFile 'C:\Users\user\Documents\GitHub\newCapstone\disputatio\TestResults\2026-07-14-editmode.log' -quit
```

Expected: Unity exit code 0, zero failed tests, and no compilation errors.

- [ ] **Step 3: Run the existing PlayMode smoke suite**

Run `ClientPlayModeSmokeTests` and any newly added two-run new-game reset test. Expected: zero failures.

- [ ] **Step 4: Execute the manual gameplay matrix**

| Route | Acceptance criteria |
|---|---|
| Hall / Cheshire normal | One question returns an answer; movement and scene interaction unlock after response. |
| Hall / Cheshire timeout | Simulated first timeout shows reconnect state, retries once, then either answers or shows a recoverable error; a second send is possible. |
| Hall / quests | First quest crossfades to the next; final tutorial quest disappears after completion delay. |
| Main menu / new game | Progress one run, return to main, Start again; inventory, Fungus flags, overlays, quest state, and book page reset; audio/video settings persist. |
| Kitchen | Fill bottle, key appears, final dialogue does not appear before pickup, key remains clickable, pickup sets `HaveMaidKey`, later sink click shows final dialogue. |
| Maid Room food | Acquire food; pickup and `FoodItemEffect` disappear with no MissingReference/NullReference console entry. |
| Maid puzzle book | Yes opens exactly one panel; no disabled-command warning. Page 3 has no active right-page navigation and no orange page appears. |
| Study mirror | Drop mirror, close panel, reopen; mirror is visible and draggable, then the puzzle can still complete. |
| BookCase2 | Clean entry shows Blue only; clicking Blue reveals Prison; revisiting restores Prison only. |

- [ ] **Step 5: Inspect console and capture evidence**

Record screenshots for the eight acceptance points and export Console errors/warnings. Acceptance requires no new `MissingReferenceException`, `NullReferenceException`, stuck `InteractionInputGate`, or Fungus missing-variable warning.

- [ ] **Step 6: Perform code review**

Review specifically for duplicated endpoint strings, state cleanup outside `finally`, scene references to file ID 0, disabled Flowchart commands still referenced by a block, stale PlayerPrefs page indices, and new-game reset code that deletes settings.

- [ ] **Step 7: Commit QA evidence**

```powershell
git add docs/qa/2026-07-14-regression-playtest.md
git commit -m "test: document July regression playtest"
```

## Self-Review Result

- All eight reported symptoms map to an implementation task and an actual-play acceptance case.
- The plan distinguishes confirmed causes from fixes already present but unprotected by tests.
- Endpoint recovery, quest completion, reset, Kitchen, Maid Room, Study, and BookCase2 changes are independently reviewable.
- No unrelated refactor or new package is required.

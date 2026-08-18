# July 14 Hall/Kitchen/Maid/Study regression playtest

Focused acceptance pass for the eight gameplay regressions covered by
`docs/superpowers/plans/2026-07-14-hall-kitchen-maid-study-regression-fixes.md`
(Tasks 1–8). Do not mark items pass until evidence exists.

---

## Status

| Track | Status | Notes |
|---|---|---|
| Automated EditMode suite | **PENDING** | Not run at documentation time. |
| Manual playtest matrix | **PENDING** | Not executed at documentation time. |
| PlayMode smoke (`ClientPlayModeSmokeTests`) | **PENDING** | Run after EditMode when Unity is available. |

**Local blocker (implementation time):** Unity EditMode automated run was blocked with:

```text
unity-cli: no Unity instance found for project: disputatio
```

Re-run the focused EditMode filters below once a Unity Editor instance is open for `disputatio` (or batchmode Unity is available). Expected artifacts:

- `disputatio/TestResults/2026-07-14-editmode.xml`
- `disputatio/TestResults/2026-07-14-editmode.log`

### EditMode filters to run when Unity is open

Exact `-testFilter` from the plan (Task 9 Step 2):

```text
ChatRequestRecoveryPolicyTests;ChatHttpClientTests;QuestTrackerHudTests;PlayDataPrefsCleanerTests;InGameSettingsPanelCleanupPolicyTests;KitchenSinkCompletionDialogueTests;KitchenAddKeyFlowTests;MaidRoomSceneFlowTests;BookPanelNavigationBoundsTests;FilterCardBookDropZoneTests;BookCase2StateRestoreTests
```

Example batchmode command (from plan):

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.36f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\Users\user\Documents\GitHub\newCapstone\disputatio' -runTests -testPlatform EditMode -testFilter 'ChatRequestRecoveryPolicyTests;ChatHttpClientTests;QuestTrackerHudTests;PlayDataPrefsCleanerTests;InGameSettingsPanelCleanupPolicyTests;KitchenSinkCompletionDialogueTests;KitchenAddKeyFlowTests;MaidRoomSceneFlowTests;BookPanelNavigationBoundsTests;FilterCardBookDropZoneTests;BookCase2StateRestoreTests' -testResults 'C:\Users\user\Documents\GitHub\newCapstone\disputatio\TestResults\2026-07-14-editmode.xml' -logFile 'C:\Users\user\Documents\GitHub\newCapstone\disputatio\TestResults\2026-07-14-editmode.log' -quit
```

Expected when unblocked: Unity exit code 0, zero failed tests, no compilation errors.

---

## Manual playtest checklist

Mark each row only after a real play session. Values: `PENDING` | `PASS` | `FAIL` (+ notes).

| Route | Acceptance criteria | Result |
|---|---|---|
| Hall / Cheshire normal | One question returns an answer; movement and scene interaction unlock after response. | PENDING |
| Hall / Cheshire timeout | Simulated first timeout shows reconnect state, retries once, then either answers or shows a recoverable error; a second send is possible. | PENDING |
| Hall / quests | First quest crossfades to the next; final tutorial quest disappears after completion delay. | PENDING |
| Main menu / new game | Progress one run, return to main, Start again; inventory, Fungus flags, overlays, quest state, and book page reset; audio/video settings persist. | PENDING |
| Kitchen | Fill bottle, key appears, final dialogue does not appear before pickup, key remains clickable, pickup sets `HaveMaidKey`, later sink click shows final dialogue. | PENDING |
| Maid Room food | Acquire food; pickup and `FoodItemEffect` disappear with no MissingReference/NullReference console entry. | PENDING |
| Maid puzzle book / page 3 | Yes opens exactly one panel; no disabled-command warning. Page 3 has no active right-page navigation and no orange page appears. | PENDING |
| Study mirror | Drop mirror, close panel, reopen; mirror is visible and draggable, then the puzzle can still complete. | PENDING |
| BookCase2 | Clean entry shows Blue only; clicking Blue reveals Prison; revisiting restores Prison only. | PENDING |

### Console / evidence (Step 5)

Acceptance requires no new:

- `MissingReferenceException`
- `NullReferenceException`
- stuck `InteractionInputGate`
- Fungus missing-variable warning

Record screenshots for the acceptance points and export Console errors/warnings when the manual pass is run.

| Evidence item | Captured? |
|---|---|
| Screenshots for acceptance routes | PENDING |
| Console export (errors/warnings) | PENDING |

---

## Sign-off

| Role | Name | Date | Verdict |
|---|---|---|---|
| Playtester | | | PENDING |
| Reviewer | | | PENDING |

**Overall:** PENDING — automated EditMode and manual playtest not completed; no pass results claimed.

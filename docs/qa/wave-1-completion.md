# Multi-Room QA Autorun — Wave 1 Completion

**Status:** `DONE_WITH_CONCERNS`  
**Branch:** `feature/self-extending-qa-autorun`  
**Worktree:** `.worktrees/qa-autorun-dev-mode`  
**Date:** 2026-07-27

## Factory capability IDs

`DeveloperQaServiceFactory.Create()` registers:

### StudyRoom (`StudyRoomQaAdapter`)
- `studyroom.mirror.preset.before-placement`
- `studyroom.mirror.grant-bookmark`
- `studyroom.mirror.place-bookmark`
- `studyroom.mirror.probe`
- `studyroom.mirror.capture`
- `studyroom.mirror.assert-solved`
- `studyroom.mirror.reset`

### Kitchen (`KitchenQaAdapter`)
- `kitchen.faucet.preset.before-faucet`
- `kitchen.faucet.click`
- `kitchen.faucet.probe`
- `kitchen.faucet.assert-clicked`
- `kitchen.faucet.capture`
- `kitchen.faucet.reset`

### MainMenu (`MainMenuQaAdapter`)
- `mainmenu.start.click`
- `mainmenu.start.probe`
- `mainmenu.start.assert-invoked`
- `mainmenu.start.capture`

## Scenario paths

| Scenario ID | Resources path | Asset path |
|---|---|---|
| `kitchen-faucet-autorun` | `QA/Scenarios/kitchen-faucet-autorun` | `disputatio/Assets/Resources/QA/Scenarios/kitchen-faucet-autorun.json` |
| `mainmenu-start-autorun` | `QA/Scenarios/mainmenu-start-autorun` | `disputatio/Assets/Resources/QA/Scenarios/mainmenu-start-autorun.json` |
| `studyroom-mirror-diary` (pre-existing) | `QA/Scenarios/studyroom-mirror-diary` | `disputatio/Assets/Resources/QA/Scenarios/studyroom-mirror-diary.json` |

## Verification

| Check | Result |
|---|---|
| `python -m pytest scripts/qa/tests -q` | **21 passed** (0.95s) |
| C# syntax (`CSharpSyntaxChecker` on SceneAdapters + EditMode/QA/Developer) | **exit 0** |
| Unity EditMode (`unity-cli --project disputatio test --mode EditMode`) | **Not run** — `no Unity instance found for project: disputatio` |

## EditMode tests added (Wave 1)

- `KitchenQaCapabilityTests`
- `MainMenuQaCapabilityTests`
- `DeveloperQaServiceFactoryMultiRoomTests`
- `MultiRoomAutorunScenarioTests`
- `DeveloperQaCliPanelParityTests` extended (kitchen/mainmenu probe describe + invoke)

## Concerns / follow-up

Reopen Unity on this worktree and run:

```powershell
.\scripts\unity-cli.cmd --project disputatio editor refresh --compile
.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter "KitchenQaCapabilityTests|MainMenuQaCapabilityTests|DeveloperQaServiceFactoryMultiRoomTests|MultiRoomAutorunScenarioTests|DeveloperQaCliPanelParityTests"
```

Wave 2 (Maid/Hall) and Wave 3 (Child/Wife/Bed) remain out of scope.

## Commits (Wave 1)

| SHA | Message |
|---|---|
| `6a26b5af` | feat(qa): add kitchen.faucet DeveloperQa capabilities |
| `73e111d2` | feat(qa): add mainmenu.start DeveloperQa capabilities |
| `1409822b` | feat(qa): register Kitchen and MainMenu capabilities in factory |
| `6ab5eb70` | test(qa): add kitchen and mainmenu capability autorun scenarios |
| `dd79b548` | test(qa): extend CLI panel parity to kitchen and mainmenu |
| *(this commit)* | docs(qa): record multi-room Wave 1 completion status |

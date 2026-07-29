# Multi-Room QA Autorun — Wave 2 Completion

**Status:** `DONE_WITH_CONCERNS`  
**Branch:** `feature/self-extending-qa-autorun`  
**Worktree:** `.worktrees/qa-autorun-dev-mode`  
**Date:** 2026-07-27

## Factory capability IDs

`DeveloperQaServiceFactory.Create()` now also registers:

### MaidRoom (`MaidRoomQaAdapter`)
- `maidroom.food.click-tray` → `OnInteraction("food")` (MaidRoom.unity InteractionRoute)
- `maidroom.food.probe`
- `maidroom.food.assert-effect` (`GetFood` via `FlowchartLocator` / `FungusVariableKeys.GetFood`; flowchart missing → `EnvironmentBlocked`)
- `maidroom.food.capture`

Omitted: `maidroom.food.preset.before-tray` — no safe public reset for `GetFood`.

### Hall (`HallQaAdapter`)
- `hall.nav.click-kitchen-entry` → `OnInteraction("left")` (Hall_playerble has no literal `"kitchen"` id; kitchen wing is Left_Clicked)
- `hall.nav.probe`
- `hall.nav.assert-route` (`controllerFound`; missing → `AssertionFailed`)
- `hall.nav.capture`

Target id `hall.kitchen-entry` remains; click maps to interaction id `"left"`.

## Scenario paths

| Scenario ID | Resources path | Asset path |
|---|---|---|
| `maidroom-food-autorun` | `QA/Scenarios/maidroom-food-autorun` | `disputatio/Assets/Resources/QA/Scenarios/maidroom-food-autorun.json` |
| `hall-nav-autorun` | `QA/Scenarios/hall-nav-autorun` | `disputatio/Assets/Resources/QA/Scenarios/hall-nav-autorun.json` |

## Verification

| Check | Result |
|---|---|
| `python -m pytest scripts/qa/tests -q` | **21 passed** |
| C# syntax (`CSharpSyntaxChecker` on SceneAdapters + EditMode/QA/Developer) | **exit 0** |
| Unity EditMode (`unity-cli --project disputatio test --mode EditMode`) | **Not run** — Unity may be unavailable on this worktree |

## EditMode tests added (Wave 2)

- `MaidRoomQaCapabilityTests`
- `HallQaCapabilityTests`
- `DeveloperQaServiceFactoryMultiRoomTests` extended (MaidRoom + Hall)
- `MultiRoomAutorunScenarioTests` extended (maidroom/hall JSON load)

## Concerns / follow-up

Reopen Unity on this worktree and run:

```powershell
.\scripts\unity-cli.cmd --project disputatio editor refresh --compile
.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter "MaidRoomQaCapabilityTests|HallQaCapabilityTests|DeveloperQaServiceFactoryMultiRoomTests|MultiRoomAutorunScenarioTests"
```

Wave 3 (Child/Wife/Bed) remains out of scope. No ForceSolve; missing controller never returns fake Ok.

## Commits (Wave 2)

| SHA | Message |
|---|---|
| `a38855e5` | feat(qa): wire MaidRoom food click and DeveloperQa capabilities |
| `f7bc4876` | feat(qa): wire Hall left-nav click and DeveloperQa capabilities |
| `ef7786b5` | feat(qa): register MaidRoom and Hall capabilities in factory |
| `49527f0d` | test(qa): add maidroom and hall capability autorun scenarios |
| *(this commit)* | docs(qa): record multi-room Wave 2 completion status |

# Multi-Room QA Autorun — Wave 3 Completion

**Status:** `DONE_WITH_CONCERNS`  
**Branch:** `feature/self-extending-qa-autorun`  
**Worktree:** `.worktrees/qa-autorun-dev-mode`  
**Date:** 2026-07-27

## Factory capability IDs

`DeveloperQaServiceFactory.Create()` now also registers:

### ChildRoom (`ChildRoomQaAdapter`)
- `childroom.seals.click-seal5` → `OnInteraction("seal5")` (`ChildRoomPuzzleController`)
- `childroom.seals.probe`
- `childroom.seals.assert-controller` (`controllerFound`; missing → `AssertionFailed`)
- `childroom.seals.capture`

Target id: `childroom.seals.seal5`.

### WifeRoom (`WifeRoomQaAdapter`)
- `wiferoom.wallclock.click` → `OnInteraction("wallclock")` (`WifeRoomPuzzleController`)
- `wiferoom.wallclock.probe`
- `wiferoom.wallclock.assert-controller` (`controllerFound`; missing → `AssertionFailed`)
- `wiferoom.wallclock.capture`

Target id: `wiferoom.wallclock`.

### BedRoom (`BedRoomQaAdapter`)
- `bedroom.book.click` → `OnInteraction("book")` (`BedRoomInteractionController`)
- `bedroom.book.probe`
- `bedroom.book.assert-controller` (`controllerFound`; missing → `AssertionFailed`)
- `bedroom.book.capture`

Target id: `bedroom.book`.

## Scenario paths

| Scenario ID | Resources path | Asset path |
|---|---|---|
| `childroom-seals-autorun` | `QA/Scenarios/childroom-seals-autorun` | `disputatio/Assets/Resources/QA/Scenarios/childroom-seals-autorun.json` |
| `wiferoom-wallclock-autorun` | `QA/Scenarios/wiferoom-wallclock-autorun` | `disputatio/Assets/Resources/QA/Scenarios/wiferoom-wallclock-autorun.json` |
| `bedroom-book-autorun` | `QA/Scenarios/bedroom-book-autorun` | `disputatio/Assets/Resources/QA/Scenarios/bedroom-book-autorun.json` |

## Verification

| Check | Result |
|---|---|
| `python -m pytest scripts/qa/tests -q` | **21 passed** |
| C# syntax (`CSharpSyntaxChecker` on Wave 3 adapters + EditMode tests) | **exit 0** |
| Unity EditMode (`unity-cli --project disputatio test --mode EditMode`) | **Not run** — no Unity instance for this worktree |

## EditMode tests added (Wave 3)

- `ChildRoomQaCapabilityTests`
- `WifeRoomQaCapabilityTests`
- `BedRoomQaCapabilityTests`
- `DeveloperQaServiceFactoryMultiRoomTests` extended (Child/Wife/Bed)
- `MultiRoomAutorunScenarioTests` extended (child/wife/bed JSON load)
- `InitialSceneAdapterSerializationTests` scene/target lists updated

## Concerns / follow-up

Reopen Unity on this worktree and run:

```powershell
.\scripts\unity-cli.cmd --project disputatio editor refresh --compile
.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter "ChildRoomQaCapabilityTests|WifeRoomQaCapabilityTests|BedRoomQaCapabilityTests|DeveloperQaServiceFactoryMultiRoomTests|MultiRoomAutorunScenarioTests|InitialSceneAdapterSerializationTests"
```

No ForceSolve; missing controller never returns fake Ok. Deeper seal/GetFood-style asserts remain optional follow-up.

## Commits (Wave 3)

| SHA | Message |
|---|---|
| `b52333f1` | feat(qa): add ChildRoom seals DeveloperQa adapter |
| `4f3b5942` | feat(qa): add WifeRoom wallclock DeveloperQa adapter |
| `0d7ae86e` | feat(qa): add BedRoom book DeveloperQa adapter |
| `2a2aadce` | feat(qa): register Child Wife Bed capabilities in factory |
| `3fa728ba` | test(qa): add child wife bed capability autorun scenarios |
| *(this commit)* | docs(qa): record multi-room Wave 3 completion status |

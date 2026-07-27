# Multi-Room QA Autorun Design

**Date:** 2026-07-27  
**Status:** Approved direction  
**Parent:** `2026-07-27-self-extending-qa-autorun-developer-mode-design.md`  
**Scope:** Extend DeveloperQa capability autorun beyond StudyRoom using thin wraps of existing QA scene adapters

## 1. Purpose

Expand the self-extending Developer Mode QA autorun from the StudyRoom diary-mirror vertical slice to the other playable rooms. Each room exposes the same `{room}.{feature}.*` capability contract so CLI and the in-game panel remain equal clients of `IDeveloperQaService`.

This design deliberately chooses **thin adapter wraps** over per-room full puzzle rewrites. Existing `IQaSceneAdapter` presets and real interaction routes are registered as DeveloperQa capabilities; missing Inspector or controller seams are fixed before claiming PASS.

## 2. Fixed Decisions

- Approach: thin capability wrap of existing QA adapters (not full StudyRoom-depth slices for every room in one pass; not scenario-JSON-only).
- Work proceeds in waves; a wave is done only when its completion gate passes.
- Capability IDs use `{room}.{feature}.{verb}` and presets use `{room}.{feature}.preset.{name}`.
- Public capability IDs are StudyRoom-style; legacy pointer targets such as `kitchen.sink.faucet` stay internal to adapters.
- `DeveloperQaServiceFactory` registers every room's capabilities (not StudyRoom alone).
- CLI and panel use identical command payloads and must return equivalent `DeveloperQaResultCode` values.
- PASS paths must not use force-solve, assertion weakening, or fake Ok when the real interaction cannot run.
- TutorRoom, Prison, basement suites, and pure hallway/cutscene scenes are deferred until a stable C# boundary exists.
- Autonomous push / merge / PR still requires a separate explicit user instruction.

## 3. Architecture

```text
DeveloperQaServiceFactory
  ├── StudyRoomQaAdapter.RegisterCapabilities     (existing)
  ├── KitchenQaAdapter.RegisterCapabilities       (Wave 1)
  ├── MainMenuQaAdapter.RegisterCapabilities      (Wave 1)
  ├── MaidRoomQaAdapter.RegisterCapabilities      (Wave 2, after real click seam)
  ├── HallQaAdapter.RegisterCapabilities          (Wave 2, after route seam)
  ├── ChildRoomQaAdapter.RegisterCapabilities     (Wave 3)
  ├── WifeRoomQaAdapter.RegisterCapabilities      (Wave 3)
  └── BedRoomQaAdapter.RegisterCapabilities       (Wave 3)

Per room minimal set:
  preset → click|invoke → probe → assert-* → capture  (+ reset when available)

External orchestrator (scripts/qa/autorun) stays room-agnostic:
  MissingCapability / ProductDefect / EnvironmentBlocked / InvalidScenario
```

Scene gameplay controllers remain in `godlotto/Script/Interaction/`. QA code only adapts them. Product seams required for observability must not change normal player behavior and use separate commits from QA capability commits.

## 4. Waves

| Wave | Rooms | Work |
|------|-------|------|
| 0 (done) | StudyRoom | Full mirror capability slice + scenario |
| 1 | Kitchen, MainMenu | Wrap existing real adapters; add capability scenarios |
| 2 | MaidRoom, Hall | Close Inspector/route gaps; then capabilities |
| 3 | ChildRoom, WifeRoom, BedRoom | New adapters + one concrete interaction each |
| Deferred | TutorRoom, Prison, Utility, Dressing, basement | No stable QA boundary yet |

## 5. Capability IDs and Scenarios

### Wave 1 — Kitchen (faucet vertical slice)

- `kitchen.faucet.preset.before-faucet`
- `kitchen.faucet.click` (delegates to existing `OnInteraction("faucet")` / `kitchen.sink.faucet`)
- `kitchen.faucet.probe`
- `kitchen.faucet.assert-clicked`
- `kitchen.faucet.capture`
- `kitchen.faucet.reset` (when a safe public reset exists; otherwise omit)

Scenario: `disputatio/Assets/Resources/QA/Scenarios/kitchen-faucet-autorun.json`  
Existing pointer scenario `2026-07/kitchen.faucet-key.json` remains; the new file is capability-invoke style.

Optional later feature (same wave or follow-up): `kitchen.parret.*` for Cheshire repeat.

### Wave 1 — MainMenu

- `mainmenu.start.click` (delegates to `MainMenu.OnStartButton`)
- `mainmenu.start.probe`
- `mainmenu.start.assert-invoked`
- `mainmenu.start.capture`

Scenario: `disputatio/Assets/Resources/QA/Scenarios/mainmenu-start-autorun.json`

### Wave 2 — MaidRoom

- `maidroom.food.preset.before-tray` (once state mutators are known)
- `maidroom.food.click-tray` (after wiring a real serialized interaction id)
- `maidroom.food.probe`
- `maidroom.food.assert-effect`
- `maidroom.food.capture`

### Wave 2 — Hall

- `hall.nav.click-kitchen-entry` (after reading and fixing scene interaction routes)
- `hall.nav.probe`
- `hall.nav.assert-route`
- `hall.nav.capture`

### Wave 3 — overview only

Feature names lock only after one concrete Inspector interaction is chosen per room:

- `childroom.seals.*` (seal place / progress)
- `wiferoom.<feature>.*`
- `bedroom.panel.*`

TutorRoom remains deferred until a C# quiz/input boundary exists.

## 6. Testing Strategy

### EditMode (required per room in the active wave)

- Capability registration and `capability.describe`
- Unknown id → `MissingCapability`
- Missing scene / DevMode gate → `EnvironmentBlocked` (not fake Ok)
- Assertion failure → `AssertionFailed`
- Mirror StudyRoom EditMode patterns under `Assets/Editor/Tests/EditMode/QA/`

### Scenarios

- Autorun JSON under `Resources/QA/Scenarios/` loads and validates
- Wave 1 scenarios use DeveloperQa capability invoke style

### Orchestrator

- Keep existing `scripts/qa/tests` green
- Add parameterized or per-room MissingCapability classification fixtures as rooms come online

### Contract

- CLI and panel bridges produce the same `DeveloperQaResultCode` for the same payload for each new capability family

If Unity is unavailable, syntax checking may land the wave with an explicit `DONE_WITH_CONCERNS` note; EditMode filters remain a required follow-up before claiming PLAYABLE PASS.

## 7. Wave Completion Gate

A wave is complete only when all of the following are true for its rooms:

1. `DeveloperQaServiceFactory` registers that room's capabilities.
2. CLI and panel share payloads and equivalent result codes for those capabilities.
3. Autorun scenario JSON exists and EditMode (or documented syntax-only concern) covers load/registration.
4. No force-solve and no weakened assertions on the PASS path.
5. No push / PR unless the user explicitly requests it.

## 8. Non-Goals

- Covering every build scene (hallways, cutscenes, basement suite) in the first multi-room pass
- Full StudyRoom-depth puzzle coverage for every room before Wave 1 ships
- Replacing existing pointer-based kitchen/menu scenarios overnight
- Automatic codegen self-extension that invents missing room controllers
- TutorRoom autorun before a stable product seam exists
- Automatic publish, merge, or PR

## 9. Relationship to Parent Design

This document does not replace the parent self-extending QA autorun design. It narrows the multi-room expansion strategy:

- Parent owns the service contract, failure classification, repair state machine, evidence layout, and StudyRoom acceptance rules.
- This document owns wave order, per-room capability ID sets, and thin-wrap rules for additional rooms.

When they conflict on StudyRoom behavior, the parent design wins. When they conflict on multi-room sequencing, this document wins.

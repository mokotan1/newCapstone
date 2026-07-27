# Room-by-Room QA Autorun Scenarios Design

**Date:** 2026-07-27
**Status:** Approved direction
**Scope:** Mansion rooms plus entrance, hallway, unlock, checkpoint, and return transitions
**Source of truth:** [미스터리 스릴러 게임 캡스톤 기획서 (초안)](https://app.notion.com/p/32cea40d2678817b9f32fc52f944c472)

## 1. Purpose

Define a room-by-room scenario system for the self-extending QA autorun. Each playable room must be independently testable from a deterministic starting preset, while transition scenarios verify that rewards, unlocks, checkpoints, death recovery, and progression flow correctly between rooms.

The first delivery covers the mansion's first floor, second floor, and basement as separate implementation tracks. Opening scenes, standalone cutscenes, settings, and credits remain outside the first delivery except where a transition scenario must cross them to validate mansion progression.

## 2. Relationship to Existing QA Designs

This design extends:

- `docs/superpowers/specs/2026-07-22-cursor-subagent-qa-driver-design.md`
- `docs/superpowers/specs/2026-07-27-self-extending-qa-autorun-developer-mode-design.md`

The existing QA Driver provides typed commands, profile isolation, scene adapters, evidence capture, and exclusive Unity mutation. The self-extending Developer Mode provides missing-capability generation, bounded product repair, Git isolation, retry limits, and resume behavior.

This design adds:

- a canonical room and transition catalog;
- a progression graph;
- scenario tiers and exit contracts;
- area-specific implementation boundaries;
- specification mismatch and not-implemented verdicts;
- coverage rules that connect Notion requirements to Unity scenes.

## 3. Planning Decomposition

The implementation documentation is split into four plans:

1. **Master roadmap:** shared schema, catalog, progression graph, coverage audit, orchestration order, and final whole-mansion verification.
2. **First-floor plan:** entrance hall, first-floor halls, Kitchen, UtilityRoom, MaidRoom, StudyRoom, bookcase sub-scenes, Prison, and their transitions.
3. **Second-floor plan:** second-floor halls, TutorRoom, ChildRoom, WifeRoom, DressingRoom, BedRoom, entrances, and their transitions.
4. **Basement plan:** Basement, BasementHallway, ExtractionRoom, ObservationRoom, BrickRoom, ResearchRoom, final progression, and ending handoff.

Each area plan must produce a working, independently testable scenario pack. Area plans may be implemented sequentially without requiring unfinished adapters from another area.

## 4. Canonical Progression Graph

The autorun models progression as contracts between gameplay regions rather than as one uninterruptible script.

```text
Mansion Entry
  -> Hall
  -> First-Floor Left/Right Halls
     -> UtilityRoom
     -> Kitchen
     -> MaidRoom
     -> StudyRoom and Bookcases
     -> Prison
  -> Second-Floor Main Hall
     -> TutorRoom
     -> ChildRoom
     -> WifeRoom and DressingRoom
     -> BedRoom
  -> Basement
     -> BasementHallway
     -> ExtractionRoom
     -> ObservationRoom
     -> BrickRoom
     -> ResearchRoom
  -> Final progression handoff
```

This graph establishes execution order, not unconditional access. Every transition declares its exact prerequisites, expected lock behavior, checkpoint behavior, and state carried into the destination.

The Notion source describes the narrative progression as mansion entry and isolation, breaker restoration, first-floor clues, maid room and study, second-floor family rooms and master bedroom, basement laboratory, final confrontation, and escape. The Unity Build Settings divide those beats into multiple playable, entrance, hallway, cutscene, and detail scenes. The catalog maps both representations without assuming a one-to-one relationship.

## 5. Canonical Room Catalog

### 5.1 First floor

| Region ID | Unity scenes | Scenario responsibility |
|---|---|---|
| `hall` | `Hall_animate`, `Hall_playerble` | Mansion isolation, Cheshire availability, initial quest/input readiness |
| `hall.left` | `Hall_Left`, `Hall_Left2`, `Hallway_Left`, `Hallway_Left2` | Left-side navigation, locked routes, state persistence |
| `hall.right` | `Hall_Right`, `Hall_Right2`, `Hall_RightCross`, `Hallway_Right`, `Hallway_Right2` | Right-side navigation, Study/Maid/Prison route gating |
| `utility-room` | `UtilityRoom` | Breaker/light restoration and downstream power-dependent state |
| `kitchen` | `Kitchen` | Bottle/sink flow, burner/food interactions where active, MaidRoom key reward |
| `maid-room` | `MaidEntrance`, `MaidRoom` | Entry gating, diary/book/puzzle flow, magnifier/Look-panel behavior, reward state |
| `study-room` | `StudyEntrance`, `StudyRoomCutScene`, `StudyRoom` | Entry gating, diary mirror puzzle, Bible commentary document, next progression state |
| `study-bookcases` | `BookCase1`, `BookCase2`, `BookCase2Back`, `BookCase3`, `BookCase4`, `POAnimation` | Detail-view navigation, persistent book state, back/close restoration |
| `prison` | `PrisonEntrance`, `Prison`, `GoPrisonAnimation` | Entry gating, transition/cutscene handoff, prison progression state |

### 5.2 Second floor

| Region ID | Unity scenes | Scenario responsibility |
|---|---|---|
| `second-floor.hall` | `2floorMainHall`, `2floorLeft`, `2floorRight`, `2floorHallway_Left`, `2floorHallway_Right`, `2floorLeftCross`, `2floorRightCross` | Navigation, locked entrances, return position, checkpoint continuity |
| `tutor-room` | `TutorEntrance`, `TutorRoom` | Entry gating, Bible quiz, deterministic grading, completion reward |
| `child-room` | `ChildEntrance`, `ChildRoom` | Entry gating, seal/drag puzzle, clue and completion state |
| `wife-room` | `WifeEntrance`, `WifeRoom`, `DressingRoom` | Entry gating, Bible commentary/calendar/Cheshire clue combination, password flow |
| `bed-room` | `BedEntrance`, `BedRoom` | Entry gating, master-bedroom truth sequence, basement progression unlock |

### 5.3 Basement

| Region ID | Unity scenes | Scenario responsibility |
|---|---|---|
| `basement.entry` | `Basement` | Basement availability, initial state, checkpoint handoff |
| `basement.hall` | `BasementHallway` | Navigation and room gating |
| `basement.extraction` | `BasementExtractionRoom` | Extraction experiment clues and progression state |
| `basement.observation` | `BasementObservationRoom` | Observation clues and cross-room state |
| `basement.brick` | `BasementBrickRoom` | Brick-room puzzle or clue state |
| `basement.research` | `BasementResearchRoom` | Research/laboratory truth, final-item preparation, final handoff |

Scene presence in Build Settings does not prove gameplay implementation. The catalog audit assigns each region one of `IMPLEMENTED`, `PARTIAL`, `NOT_IMPLEMENTED`, or `SPEC_MISMATCH` before runtime scenarios execute.

## 6. Scenario Tiers

Every gameplay region defines three required scenario tiers.

### 6.1 Smoke

Smoke verifies:

- the canonical scene loads within its timeout;
- the region adapter resolves;
- required Flowcharts and controllers exist;
- declared stable target IDs are unique;
- the input gate is released after readiness;
- no new relevant Console error occurs during load;
- a state snapshot and screenshot can be captured.

Smoke must not change puzzle completion state.

### 6.2 Happy path

Happy path:

1. applies the region's deterministic entry preset;
2. performs the player-visible interaction sequence through RealInput;
3. repeats the critical domain operation through the API boundary after reset;
4. validates intermediate checkpoints;
5. validates the exit contract;
6. captures screenshots and Console deltas;
7. restores or closes the QA profile.

Force-solve and direct state mutation may prepare a preset or cleanup a failed run, but cannot provide PASS evidence for a player-visible step.

### 6.3 Guard path

Guard paths cover the smallest high-value negative set:

- wrong item or wrong target;
- missing prerequisite item or flag;
- duplicate pickup or repeated completion;
- interaction while a modal panel should block the background;
- close, Backspace, cancel, or re-entry restoration;
- locked door before prerequisite and unlocked door after prerequisite;
- interrupted interaction cleanup where the room owns an input lock.

A room may add one room-specific guard only when the Notion requirement or existing defect history identifies material risk.

## 7. Transition Scenarios

Transitions are first-class scenarios, not the final step of a room script.

```json
{
  "schemaVersion": 1,
  "id": "transition.kitchen-to-maid-room",
  "sourceRegion": "kitchen",
  "destinationRegion": "maid-room",
  "entryPreset": "kitchen.before-bottle-fill",
  "prerequisites": ["inventory.opaque-bottle"],
  "lockedAssertions": ["door.maid.locked"],
  "sourceExitContract": ["inventory.maid-room-key", "flag.HaveMaidKey"],
  "destinationEntryContract": ["scene.MaidEntrance", "door.maid.unlocked"],
  "checkpointContract": ["resumeRegion.maid-room"]
}
```

Each transition verifies:

- access is denied before prerequisites;
- source-room completion produces its declared reward and flags;
- the door or route becomes usable without reopening the game;
- the expected entrance, hallway, or cutscene plays;
- the destination region reaches ready state;
- source rewards persist;
- the checkpoint records the newly unlocked region;
- death or controlled restart resumes at the latest valid unlocked region;
- returning to the source does not duplicate one-time rewards.

## 8. Room Scenario Contract

Each room pack contains one manifest and separate scenario files:

```text
Resources/QA/Scenarios/Rooms/<area>/<room-id>/
  manifest.json
  smoke.json
  happy-path.json
  guard-wrong-input.json
  guard-reentry.json
```

The manifest contract is:

```json
{
  "schemaVersion": 1,
  "roomId": "kitchen",
  "areaId": "first-floor",
  "notionSource": "https://app.notion.com/p/32cea40d2678817b9f32fc52f944c472",
  "unityScenes": ["Kitchen"],
  "implementationStatus": "IMPLEMENTED",
  "entryPreset": "kitchen.before-bottle-fill",
  "requiredCapabilities": [
    "kitchen.sink.fill-bottle",
    "kitchen.key.probe",
    "inventory.probe"
  ],
  "scenarios": [
    "room.kitchen.smoke",
    "room.kitchen.happy-path",
    "room.kitchen.guard.wrong-item",
    "room.kitchen.guard.reentry"
  ],
  "exitContract": {
    "inventoryContains": ["maid-room-key"],
    "flags": {"HaveMaidKey": true},
    "unlocks": ["maid-room"]
  }
}
```

IDs are lowercase dotted identifiers. Unity scene names retain their exact project spelling. Manifests never contain arbitrary C# member names, screen coordinates as authoritative locators, or unrestricted reflection calls.

## 9. Capability Discovery and Self-Extension

Before running a room pack, the orchestrator compares `requiredCapabilities` with the live capability registry.

If a capability is missing:

1. validation returns `MissingQaCapability` without entering the scenario;
2. the orchestrator creates one bounded QA change request;
3. the AI adds the smallest preset, interaction, probe, assertion, or recovery capability;
4. CLI and panel parity tests are added;
5. Unity compiles and focused tests pass;
6. the capability is committed separately;
7. preflight restarts for that room.

If the capability exists but product state violates an assertion, the failure is classified as `ProductDefect`. The AI creates a reproduction test, applies one minimal fix, commits it separately, and replays the failing room step.

The normalized failure signature has a three-attempt limit inherited from the self-extending Developer Mode design.

## 10. Verdict Model

Runtime scenario verdicts:

- `PASS`: complete evidence contract satisfied.
- `FAIL`: executable product behavior violates a requirement.
- `BLOCKED`: environment or repeated autonomous repair prevents reliable execution.
- `NOT_RUN`: dependency or requested run scope excludes the scenario.

Catalog verdicts:

- `NOT_IMPLEMENTED`: the Notion-planned behavior has no executable implementation yet.
- `SPEC_MISMATCH`: Notion requirements and implemented behavior conflict, and neither side can safely be selected automatically.
- `PARTIAL`: some required controllers, data, or scene connections exist but the room contract cannot yet be completed.

`NOT_IMPLEMENTED`, `PARTIAL`, and `SPEC_MISMATCH` never count as gameplay failures and never count as PASS. They appear as coverage gaps in the master report.

## 11. Evidence

Evidence is grouped by area, room, tier, and attempt:

```text
docs/qa/runs/<run-id>/
  manifest.json
  coverage.json
  report.md
  first-floor/
    kitchen/
      smoke/
      happy-path/
      guard-wrong-item/
  second-floor/
  basement/
```

Every PASS includes:

- initial and final snapshots;
- required intermediate assertion events;
- RealInput and API outcomes where required;
- a Console delta with no new relevant exception;
- named screenshots at declared checkpoints;
- exit-contract and transition-contract results;
- Git commit and capability registry versions.

The master report links the Notion source and lists implementation gaps, specification mismatches, AI-generated QA capabilities, product fixes, retries, and rollbacks.

## 12. Coverage Rules

Coverage audits run without entering Play Mode and fail when:

- an enabled Build Settings gameplay scene maps to no canonical region;
- a canonical region has no manifest;
- a manifest names a missing scene;
- a scenario references an undeclared capability;
- a region lacks `smoke`, `happy-path`, or required guard coverage;
- a transition lacks locked, unlocked, persistence, and checkpoint assertions;
- two region manifests claim the same scene without declaring a shared-scene reason;
- a Notion-derived requirement has no mapped assertion or is not explicitly labeled `NOT_IMPLEMENTED`, `PARTIAL`, or `SPEC_MISMATCH`.

Opening, settings, cutscene-only, and ending scenes may be explicitly excluded with a reason in the catalog. Silent exclusion is not permitted.

## 13. Execution Order

Within an area:

1. run the static catalog and capability audit;
2. run all smoke scenarios;
3. run each room's happy and guard scenarios independently;
4. run transition scenarios in progression order;
5. run the area's chained traversal;
6. aggregate the area report.

Across areas:

1. shared catalog and schema;
2. first-floor pack;
3. second-floor pack;
4. basement pack;
5. whole-mansion progression traversal.

A chained traversal may stop after a failing transition, but independent downstream room scenarios still run from their own presets unless the user requested fail-fast behavior.

## 14. Testing Strategy

- Schema tests for region manifests, room scenarios, transitions, statuses, and stable IDs.
- Build Settings coverage tests for mapping and explicit exclusions.
- Adapter contract tests for capabilities and snapshot fields.
- EditMode tests for each region's preset, probes, assertions, and exit-contract evaluation.
- PlayMode tests for each room's RealInput happy path and modal/input guards.
- Transition PlayMode tests for locked/unlocked routes, persisted rewards, checkpoints, death recovery, and duplicate prevention.
- Orchestrator fixture tests for missing capability generation, product-defect repair, retry limits, rollback, resume, and downstream independent execution.
- Area-level chained traversal tests.
- One final whole-mansion traversal after all three area plans pass independently.

## 15. Initial Area Acceptance

An area plan is complete only when:

- every cataloged region has an implementation status;
- every implemented region has passing smoke, happy-path, and required guard scenarios;
- every transition has passing lock, unlock, persistence, checkpoint, and return assertions;
- all declared missing capabilities have been implemented or the affected region is explicitly `PARTIAL`;
- no unresolved product defect is hidden by a preset or force-solve path;
- the area report contains evidence for every PASS and a reason for every non-PASS status.

The master roadmap is complete when the first floor, second floor, and basement each satisfy this contract and the whole-mansion progression traversal produces a supported verdict.

## 16. Non-Goals

- Full opening, settings, credits, localization, or audio-cue coverage in the first delivery.
- Exhaustive combinatorial testing of every inventory and flag state.
- Treating every Unity scene as an independent player-facing room.
- Automatically resolving a conflict between the Notion plan and implemented gameplay.
- Using direct state mutation as proof that the normal player interaction works.
- Allowing parallel mutation of one Unity Editor.
- Automatically pushing, merging, or publishing autonomous changes.

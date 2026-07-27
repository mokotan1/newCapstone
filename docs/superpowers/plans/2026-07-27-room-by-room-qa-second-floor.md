# Room-by-Room QA Autorun — Second Floor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Deliver second-floor room packs under `Resources/QA/Scenarios/Rooms/second-floor/` per design §5.2 with smoke/happy/guard manifests, using existing thin DeveloperQa capabilities where present and NOT_IMPLEMENTED stubs elsewhere.

**Architecture:** Same contract as first-floor packs (master schema). RealInput §6.2 dual path is required only for rooms that already have a GameObject resolver; otherwise PARTIAL invoke-only happy paths with explicit status.

**Depends on:** master roadmap schema + first-floor pack patterns.

**Regions:** `second-floor.hall`, `tutor-room`, `child-room`, `wife-room`, `bed-room`

---

### Task 1: child-room / wife-room / bed-room packs (existing caps)

Use:
- `childroom.seals.*`
- `wiferoom.wallclock.*`
- `bedroom.book.*`

Create `Rooms/second-floor/<room>/manifest.json` + smoke/happy/guard. Status PARTIAL.
Happy path: preset/click invoke sequence (pointer RealInput optional only if resolver exists — skip RealInput for Wave A).

Commit: `test(qa): add second-floor child wife bed room packs`

### Task 2: second-floor.hall + tutor-room stubs

`second-floor.hall`: PARTIAL or NOT_IMPLEMENTED navigation smoke.
`tutor-room`: NOT_IMPLEMENTED (no stable C# quiz boundary yet) with smoke stub.

Commit: `test(qa): stub second-floor hall and tutor room manifests`

### Task 3: Transitions

- `transition.second-hall-to-child.json`
- `transition.child-to-wife.json` (PARTIAL)
- `transition.wife-to-bed.json` (PARTIAL)

Commit: `test(qa): add second-floor transition scenario stubs`

### Task 4: Acceptance doc + pytest

`docs/qa/rooms/second-floor-acceptance.md`
Commit: `docs(qa): record second-floor room pack acceptance status`

All JSON must pass `validate_room_manifest` / `validate_transition`. pytest green.

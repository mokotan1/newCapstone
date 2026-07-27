# Room-by-Room QA Autorun — First Floor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver an independently testable first-floor scenario pack (`hall`, `hall.left`/`hall.right`, `utility-room`, `kitchen`, `maid-room`, `study-room`, `study-bookcases`, `prison`) with smoke / happy-path / guard manifests per design §6–§8, consuming the master schema from the master roadmap plan.

**Architecture:** Each room lives under `Resources/QA/Scenarios/Rooms/first-floor/<room-id>/`. Capabilities come from existing thin DeveloperQa adapters where present; missing required capabilities return `MissingQaCapability` and may be added in separate `feat(qa):` commits. Happy-path PASS must not use force-solve.

**Tech Stack:** JSON room packs, Python schema validators from master plan, Unity EditMode for capability preflight, existing Kitchen/Maid/Hall/StudyRoom adapters.

**Depends on:** `docs/superpowers/plans/2026-07-27-room-by-room-qa-master-roadmap.md` Tasks 1–3 at minimum.  
**Spec:** `docs/superpowers/specs/2026-07-27-room-by-room-qa-autorun-scenarios-design.md` §5.1, §6–§9, §15.

---

## File Structure

```text
Resources/QA/Scenarios/Rooms/first-floor/
  kitchen/
    manifest.json
    smoke.json
    happy-path.json
    guard-wrong-item.json
    guard-reentry.json
  hall/
    ...
  maid-room/
  study-room/
  utility-room/
  prison/
  study-bookcases/
  hall.left/   (may be PARTIAL with navigation smoke only)
  hall.right/
Transitions/
  transition.kitchen-to-maid-room.json
  transition.hall-to-kitchen.json
```

---

### Task 1: Kitchen room pack (reference implementation)

**Files:**
- Create all five JSON files under `Rooms/first-floor/kitchen/`
- Test: `scripts/qa/tests/test_first_floor_kitchen_pack.py`

Manifest must match design §8 shape. Use currently available capabilities where possible:

```text
requiredCapabilities (initial, may be PARTIAL):
  - kitchen.faucet.preset.before-faucet
  - kitchen.faucet.click
  - kitchen.faucet.probe
  - kitchen.faucet.assert-clicked
  - kitchen.faucet.capture
```

If exit contract (maid-room-key) is not yet probeable, set `implementationStatus` to `PARTIAL` and document gap in manifest `notes` field only if schema allows — otherwise keep status `PARTIAL` and list missing caps in `requiredCapabilities` so audit/self-extend can target them.

Smoke scenario: load Kitchen readiness via capability probe only (no puzzle completion).  
Happy path: preset → click faucet → assert-clicked → capture (document that full bottle/key flow is PARTIAL until those caps exist).  
Guards: wrong target MissingCapability or AssertionFailed; reentry after reset.

- [ ] **Step 1: Failing pytest** — `validate_room_manifest` + files exist under Resources path (resolve from repo root)

- [ ] **Step 2–4: Write JSON + PASS validators**

- [ ] **Step 5: Commit** `test(qa): add first-floor kitchen room pack manifests`

---

### Task 2: Hall + Maid + Study room packs (PARTIAL ok)

Same structure as kitchen for:

| roomId | Status seed | Caps to require (existing) |
|---|---|---|
| `hall` | PARTIAL | `hall.nav.click-kitchen-entry`, probe, assert-route, capture |
| `maid-room` | PARTIAL | `maidroom.food.*` |
| `study-room` | PARTIAL or IMPLEMENTED if mirror pack maps | `studyroom.mirror.*` |

- [ ] Create manifests + smoke/happy/guard stubs that validate under schema
- [ ] Commit: `test(qa): add first-floor hall maid study room packs`

---

### Task 3: Remaining first-floor regions as NOT_IMPLEMENTED / PARTIAL stubs

`utility-room`, `prison`, `study-bookcases`, `hall.left`, `hall.right`:

- Manifest with `implementationStatus: NOT_IMPLEMENTED` or `PARTIAL`
- scenarios array still lists required tier ids
- scenario files may be empty-shell JSON that smoke only documents gap via assertion note — OR omit scenario files and let coverage audit mark gap (prefer: create smoke.json that is valid but marks expected NOT_IMPLEMENTED via status, not PASS)

Design: NOT_IMPLEMENTED never counts as PASS. Prefer minimal smoke.json that the runner maps to NOT_RUN/NOT_IMPLEMENTED without claiming gameplay PASS.

- [ ] Commit: `test(qa): stub remaining first-floor room manifests`

---

### Task 4: First-floor transitions

**Files:**
- `Rooms/Transitions/transition.hall-to-kitchen.json`
- `Rooms/Transitions/transition.kitchen-to-maid-room.json`
- `Rooms/Transitions/transition.maid-to-study.json` (PARTIAL if locks unknown)

Validate with `validate_transition`. Locked/unlocked assertions may reference flag ids as strings even if runtime probes are later.

- [ ] Commit: `test(qa): add first-floor transition scenario stubs`

---

### Task 5: Wire DeveloperQa preflight for kitchen pack

**Files:**
- Python or EditMode helper: given kitchen manifest `requiredCapabilities`, query registry / factory-created service `ListCapabilities()` and return missing ids
- Test: fixture with empty registry → MissingQaCapability list; factory Create() → kitchen faucet ids present

- [ ] Commit: `feat(qa): preflight requiredCapabilities against DeveloperQa registry`

---

### Task 6: First-floor area acceptance doc

- Run coverage audit report-only
- Write `docs/qa/rooms/first-floor-acceptance.md` listing each region status
- Commit: `docs(qa): record first-floor room pack acceptance status`

---

## Acceptance (design §15, first floor)

Complete only when:

1. Every §5.1 region has a manifest + implementationStatus
2. Kitchen (and any IMPLEMENTED room) has validating smoke/happy/guard files
3. Transition stubs validate
4. Coverage audit report is checked in
5. No force-solve used as happy-path PASS evidence

Full RealInput PlayMode and true IMPLEMENTED kitchen bottle→key exit contract are follow-ups once missing capabilities are generated via self-extend.

## Non-goals

- Second floor / basement packs
- Whole-mansion traversal
- Push/PR

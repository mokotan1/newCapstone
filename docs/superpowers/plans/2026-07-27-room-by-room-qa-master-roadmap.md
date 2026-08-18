# Room-by-Room QA Autorun — Master Roadmap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the shared schema, canonical room catalog, progression graph, coverage audit, and orchestration hooks required by `2026-07-27-room-by-room-qa-autorun-scenarios-design.md` before any area pack can claim completeness.

**Architecture:** Python-first catalog/audit under `scripts/qa/rooms/` (no Play Mode required) plus Unity Resources manifests under `Resources/QA/Scenarios/Rooms/`. Existing `DeveloperQaService` / thin room adapters remain capability providers; this plan does not replace them. Area packs (first/second/basement) are separate plans that consume this master contract.

**Tech Stack:** Python 3 + pytest, JSON schema validation, Unity TextAssets under Resources, existing `Godlotto.QA.Developer` capability registry for live preflight.

**Spec:** `docs/superpowers/specs/2026-07-27-room-by-room-qa-autorun-scenarios-design.md`  
**Worktree:** `.worktrees/qa-autorun-dev-mode` · `feature/self-extending-qa-autorun`

---

## File Structure

```text
scripts/qa/rooms/
  __init__.py
  schema.py              # load/validate manifest + scenario + transition JSON
  catalog.py             # canonical region catalog + statuses
  progression.py         # progression graph edges
  coverage_audit.py      # Build Settings / manifest / capability audits
  verdicts.py            # PASS/FAIL/BLOCKED/NOT_RUN + catalog statuses
scripts/qa/tests/
  test_room_schema.py
  test_room_catalog.py
  test_coverage_audit.py
  test_progression.py
disputatio/Assets/Resources/QA/Scenarios/Rooms/
  catalog.json           # machine-readable catalog copy of design §5
  exclusions.json        # explicit opening/settings/cutscene exclusions
  Transitions/           # transition scenario stubs (master lists ids; area packs fill)
docs/qa/rooms/
  coverage-baseline.md
```

---

### Task 1: Schema types and validators

**Files:**
- Create: `scripts/qa/rooms/schema.py`
- Test: `scripts/qa/tests/test_room_schema.py`

- [ ] **Step 1: Failing tests**

```python
from scripts.qa.rooms.schema import validate_room_manifest, validate_room_scenario, SchemaError

def test_manifest_requires_smoke_happy_and_guard():
    bad = {
        "schemaVersion": 1,
        "roomId": "kitchen",
        "areaId": "first-floor",
        "unityScenes": ["Kitchen"],
        "implementationStatus": "IMPLEMENTED",
        "entryPreset": "kitchen.before-bottle-fill",
        "requiredCapabilities": [],
        "scenarios": ["room.kitchen.smoke"],
        "exitContract": {"inventoryContains": [], "flags": {}, "unlocks": []},
    }
    try:
        validate_room_manifest(bad)
        assert False, "expected SchemaError"
    except SchemaError as exc:
        assert "happy-path" in str(exc).lower() or "guard" in str(exc).lower()

def test_manifest_accepts_minimal_valid_kitchen():
    ok = {
        "schemaVersion": 1,
        "roomId": "kitchen",
        "areaId": "first-floor",
        "notionSource": "https://app.notion.com/p/32cea40d2678817b9f32fc52f944c472",
        "unityScenes": ["Kitchen"],
        "implementationStatus": "IMPLEMENTED",
        "entryPreset": "kitchen.before-bottle-fill",
        "requiredCapabilities": ["kitchen.faucet.probe"],
        "scenarios": [
            "room.kitchen.smoke",
            "room.kitchen.happy-path",
            "room.kitchen.guard.wrong-item",
            "room.kitchen.guard.reentry",
        ],
        "exitContract": {
            "inventoryContains": ["maid-room-key"],
            "flags": {"HaveMaidKey": True},
            "unlocks": ["maid-room"],
        },
    }
    validate_room_manifest(ok)
```

Allowed `implementationStatus`: `IMPLEMENTED`, `PARTIAL`, `NOT_IMPLEMENTED`, `SPEC_MISMATCH`.

- [ ] **Step 2: Run** `python -m pytest scripts/qa/tests/test_room_schema.py -q` — expect FAIL

- [ ] **Step 3: Implement `validate_room_manifest` / `validate_room_scenario` / `validate_transition`**

Validate transition shape from design §7 (`sourceRegion`, `destinationRegion`, `prerequisites`, contracts).

- [ ] **Step 4: PASS**

- [ ] **Step 5: Commit** `feat(qa): add room-by-room manifest and scenario schema validators`

---

### Task 2: Canonical catalog + progression graph

**Files:**
- Create: `scripts/qa/rooms/catalog.py`
- Create: `scripts/qa/rooms/progression.py`
- Create: `disputatio/Assets/Resources/QA/Scenarios/Rooms/catalog.json`
- Test: `scripts/qa/tests/test_room_catalog.py`, `test_progression.py`

Catalog must include every region ID from design §5.1–5.3 with `unityScenes` arrays and default `implementationStatus` (use `PARTIAL` for thin-wrap rooms we already have adapters for: kitchen, hall, maid-room, study-room, child-room, wife-room, bed-room; `NOT_IMPLEMENTED` for basement and detail scenes until audited).

Progression edges from design §4 as an adjacency list.

- [ ] **Step 1: Failing tests** — catalog contains `kitchen`, `tutor-room`, `basement.research`; progression has `hall -> kitchen` path via first-floor left/right as documented edges

- [ ] **Step 2–4: Implement + PASS**

- [ ] **Step 5: Commit** `feat(qa): add canonical room catalog and progression graph`

---

### Task 3: Coverage audit (static, no Play Mode)

**Files:**
- Create: `scripts/qa/rooms/coverage_audit.py`
- Create: `disputatio/Assets/Resources/QA/Scenarios/Rooms/exclusions.json`
- Test: `scripts/qa/tests/test_coverage_audit.py`

Audit rules from design §12:

1. Build Settings gameplay scene without region mapping → fail unless in `exclusions.json` with reason
2. Catalog region without `Rooms/<area>/<room>/manifest.json` → fail (or report gap list mode)
3. Manifest scene missing from Build Settings → fail
4. Scenario references capability not in `requiredCapabilities` → fail
5. Missing smoke/happy/guard scenario files for `IMPLEMENTED` regions → fail
6. Silent exclusion forbidden

For Task 3 first green: support `--report-only` that returns structured gaps without raising, plus strict mode that raises `CoverageAuditError`.

Parse Build Settings from `disputatio/ProjectSettings/EditorBuildSettings.asset` (YAML list of `path:` entries); map scene file stem to Unity scene name.

- [ ] **Step 1: Failing test** — with empty Rooms tree, audit reports missing manifests for catalog regions

- [ ] **Step 2–4: Implement + PASS**

- [ ] **Step 5: Commit** `feat(qa): add static room coverage audit`

---

### Task 4: Verdict helpers + baseline report

**Files:**
- Create: `scripts/qa/rooms/verdicts.py`
- Create: `docs/qa/rooms/coverage-baseline.md`
- Test: small verdict unit tests in `test_room_schema.py` or `test_verdicts.py`

Catalog verdicts never equal PASS/FAIL gameplay. Runtime verdicts: PASS/FAIL/BLOCKED/NOT_RUN.

- [ ] Generate baseline markdown by running audit report-only against current repo
- [ ] Commit: `docs(qa): add room coverage baseline from master audit`

---

### Task 5: Master orchestration stub

**Files:**
- Create: `scripts/qa/rooms/orchestrate_area.py` (CLI entry: audit → list room packs → print order)
- Test: `scripts/qa/tests/test_orchestrate_order.py`

Order within area (design §13): audit → smoke → happy/guard → transitions → chained traversal (chained may be stub printing "not implemented").

- [ ] Commit: `feat(qa): add room-area orchestration order stub`

---

## Spec coverage (master only)

| Spec section | Task |
|---|---|
| §3 master roadmap | this plan |
| §4 progression | 2 |
| §5 catalog | 2 |
| §8 manifest contract | 1 |
| §10 verdicts | 4 |
| §12 coverage | 3 |
| §13 execution order | 5 |
| Area packs | deferred to first/second/basement plans |

## Non-goals

- Writing all room smoke/happy/guard JSON here
- PlayMode RealInput
- Tutor/basement full packs
- Push/PR

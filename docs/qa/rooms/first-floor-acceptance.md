# First-floor room pack acceptance

Date: 2026-07-27  
Plan: `docs/superpowers/plans/2026-07-27-room-by-room-qa-first-floor.md`  
Coverage audit: `python -m scripts.qa.rooms.coverage_audit --report-only`

## Coverage audit (report-only)

| Field | Value |
|---|---|
| ok | `false` (expected: second-floor / basement manifests still missing) |
| buildSceneCount | 56 |
| catalogRegionCount | 20 |
| excludedSceneCount | 7 |
| first-floor missingManifests | none |
| missingScenarioFiles | none |
| undeclaredCapabilities | none |
| unmappedBuildScenes | none |

Remaining catalog gaps (out of first-floor scope): `second-floor.hall`, `tutor-room`, `child-room`, `wife-room`, `bed-room`, and all `basement.*` regions.

## Region status

| roomId | implementationStatus | Pack files | Notes |
|---|---|---|---|
| hall | PARTIAL | manifest + smoke/happy/guard | Uses `hall.nav.*` |
| hall.left | NOT_IMPLEMENTED | manifest + smoke stub | Empty smoke; no PASS claim |
| hall.right | NOT_IMPLEMENTED | manifest + smoke stub | Empty smoke; no PASS claim |
| utility-room | NOT_IMPLEMENTED | manifest + smoke stub | Empty smoke; no PASS claim |
| kitchen | PARTIAL | full pack | Happy-path encodes RealInput (`interaction.pointer` / `kitchen.sink.faucet`) then reset then API (`kitchen.faucet.click`) per design §6.2; bottle→key exit / maid-key still PARTIAL; no force-solve PASS |
| maid-room | PARTIAL | full pack | Uses `maidroom.food.*` |
| study-room | PARTIAL | full pack | Uses `studyroom.mirror.*` |
| study-bookcases | NOT_IMPLEMENTED | manifest + smoke stub | Empty smoke; no PASS claim |
| prison | NOT_IMPLEMENTED | manifest + smoke stub | Empty smoke; no PASS claim |

## Transitions

| id | Status |
|---|---|
| transition.hall-to-kitchen | stub validates |
| transition.kitchen-to-maid-room | stub validates |
| transition.maid-to-study | PARTIAL stub validates |

## Acceptance checks (design §15, first floor)

1. Every §5.1 region has a manifest + implementationStatus — **yes**
2. Kitchen (and PARTIAL rooms with adapters) have validating smoke/happy/guard — **yes** for kitchen/hall/maid/study
3. Transition stubs validate — **yes**
4. Coverage audit report recorded — **this document**
5. No force-solve used as happy-path PASS evidence — **yes**

## Preflight

`scripts/qa/rooms/preflight.missing_required_capabilities` compares manifest `requiredCapabilities` to a live id set (no Unity required for the unit test).

# Second-floor room pack acceptance

Date: 2026-07-27  
Plan: `docs/superpowers/plans/2026-07-27-room-by-room-qa-second-floor.md`  
Coverage audit: `python -m scripts.qa.rooms.coverage_audit --report-only`

## Coverage audit (report-only)

| Field | Value |
|---|---|
| ok | `false` (expected: basement manifests still missing) |
| buildSceneCount | 56 |
| catalogRegionCount | 20 |
| excludedSceneCount | 7 |
| second-floor missingManifests | none |
| missingScenarioFiles | none |
| undeclaredCapabilities | none |
| unmappedBuildScenes | none |

Remaining catalog gaps (out of second-floor scope): all `basement.*` regions.

## Region status

| roomId | implementationStatus | Pack files | Notes |
|---|---|---|---|
| second-floor.hall | NOT_IMPLEMENTED | manifest + smoke stub | Empty smoke; no PASS claim; navigation caps deferred |
| tutor-room | NOT_IMPLEMENTED | manifest + smoke stub | Empty smoke; no stable C# quiz boundary yet |
| child-room | PARTIAL | full pack | Uses `childroom.seals.*`; invoke-only happy path (no RealInput Wave A) |
| wife-room | PARTIAL | full pack | Uses `wiferoom.wallclock.*`; invoke-only happy path |
| bed-room | PARTIAL | full pack | Uses `bedroom.book.*`; exit unlocks `basement.entry`; invoke-only happy path |

## Transitions

| id | Status |
|---|---|
| transition.second-hall-to-child | stub validates |
| transition.child-to-wife | PARTIAL stub validates |
| transition.wife-to-bed | PARTIAL stub validates |

## Acceptance checks (design §15, second floor)

1. Every §5.2 region has a manifest + implementationStatus — **yes**
2. PARTIAL rooms with adapters have validating smoke/happy/guard — **yes** for child/wife/bed
3. Transition stubs validate — **yes**
4. Coverage audit report recorded — **this document**
5. No force-solve used as happy-path PASS evidence — **yes**

## Tests

`pytest scripts/qa/tests/test_second_floor_room_packs.py` validates manifests, scenarios, stub smoke-only layout, and transitions.

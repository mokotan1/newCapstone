# Room coverage baseline

Generated: 2026-07-27 (master roadmap Task 4)

Source audit: python -m scripts.qa.rooms.coverage_audit (report-only).

## Summary

- Build Settings enabled scenes: **56**
- Catalog regions: **20**
- Explicit exclusions: **7**
- Audit ok: **False** (expected false until area packs add manifests)

## Catalog statuses

### PARTIAL (thin-wrap adapters present)

- bed-room
- child-room
- hall
- kitchen
- maid-room
- study-room
- wife-room

### NOT_IMPLEMENTED (basement + detail / unaudited)

- basement.brick
- basement.entry
- basement.extraction
- basement.hall
- basement.observation
- basement.research
- hall.left
- hall.right
- prison
- second-floor.hall
- study-bookcases
- tutor-room
- utility-room

## Gaps (report-only)

### Missing manifests

- basement.brick
- basement.entry
- basement.extraction
- basement.hall
- basement.observation
- basement.research
- bed-room
- child-room
- hall
- hall.left
- hall.right
- kitchen
- maid-room
- prison
- second-floor.hall
- study-bookcases
- study-room
- tutor-room
- utility-room
- wife-room

### Unmapped Build Settings scenes

- (none — all enabled scenes map to a region or an exclusion with reason)

### Other gap buckets

- missingScenarioFiles: 0
- manifestScenesMissingFromBuild: 0
- undeclaredCapabilities: 0

## Notes

- Catalog statuses (PARTIAL, NOT_IMPLEMENTED, SPEC_MISMATCH) are coverage gaps, never gameplay PASS/FAIL.
- Opening / settings / ending scenes are listed in exclusions.json with reasons (silent exclusion forbidden).
- Area packs (first/second/basement) own room manifests and scenario JSON.

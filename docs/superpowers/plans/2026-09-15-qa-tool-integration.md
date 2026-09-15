# QA Tool Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Freeze the QA tool contracts (plan, verdict, evidence, report, test-result normalize) so Hall→Kitchen runs cannot treat command success or old manifests as gameplay PASS.

**Architecture:** A new `scripts/qa/tool/` package owns the execution-plan and final judgment contracts. It wraps existing room schema, preflight helpers, and `scripts.unity_harness.result_contract` without merging the two scenario runners. No new Unity MonoBehaviour Manager. Gateway/Hall adapter changes wait until this slice is green.

**Tech Stack:** Python 3, pytest (`python -m pytest scripts/qa/tests -q`), existing SHA-256 / JSON contracts. Unity C# is out of scope for Tasks 1–5.

---

## File map (slice 1)

- Create: `scripts/qa/tool/__init__.py`
- Create: `scripts/qa/tool/errors.py`
- Create: `scripts/qa/tool/plan.py`
- Create: `scripts/qa/tool/verdict.py`
- Create: `scripts/qa/tool/evidence.py`
- Create: `scripts/qa/tool/report.py`
- Create: `scripts/qa/tool/normalize.py`
- Test: `scripts/qa/tests/test_tool_plan.py`
- Test: `scripts/qa/tests/test_tool_verdicts.py`
- Test: `scripts/qa/tests/test_tool_evidence.py`
- Test: `scripts/qa/tests/test_tool_report.py`
- Test: `scripts/qa/tests/test_tool_normalize.py`
- Do not modify: `HallQaAdapter.cs`, `QaRunManifest.cs`, `result_contract.py` (wrap only)

## Task 1: Plan builder (AC01)

**Files:**
- Test: `scripts/qa/tests/test_tool_plan.py`
- Create: `scripts/qa/tool/plan.py`, `scripts/qa/tool/errors.py`

- [x] **Step 1: Write the failing tests** (see `test_tool_plan.py`)
- [x] **Step 2: Run tests to verify they fail**
- [x] **Step 3: Minimal implementation**
- [x] **Step 4: Re-run tests — PASS**
- [x] **Step 5: Committed `61316a26` when the user asked**

## Task 2: Scenario and aggregate verdicts (AC09, AC17)

- [ ] Failing tests in `test_tool_verdicts.py`
- [ ] `judge_scenario`: FAIL beats missing evidence; missing required step/input/artifact/cleanup/console → BLOCKED; never started → NOT_RUN; PASS only when every required layer, assertion, screenshot, console, cleanup is present and passing
- [ ] `aggregate_run`: any FAIL → FAIL; else any BLOCKED/NOT_RUN → BLOCKED; all required PASS → PASS; exclusions do not shrink the denominator
- [ ] Legacy `QaRunManifest` Pass without new fields → BLOCKED (`legacy-schema`)

## Task 3: Evidence validator (AC10)

- [ ] Failing tests in `test_tool_evidence.py`
- [ ] Reject missing file, 0-byte, undecodable PNG, SHA-256 mismatch, path escape (`..`, absolute outside run root)
- [ ] Do not rewrite source artifacts; return artifact id + reasonCodes

## Task 4: Report JSON/Markdown (AC18)

- [ ] Failing tests in `test_tool_report.py`
- [ ] Same verdicts, reason codes, evidence links, NOT_RUN, exclusions, cleanup, review in both formats
- [ ] Reuse `scripts.qa.autorun.report.sanitize_for_report` for secrets

## Task 5: Test adapter normalize (AC21)

- [ ] Failing tests in `test_tool_normalize.py`
- [ ] Wrap `classify_test_counts` / `derive_from_native_exit`
- [ ] Missing counts stay `null` (never coerce unknown → 0)
- [ ] Zero executed and exit-0+failed both cannot be `verificationStatus=passed`, with distinct `resultKind`

## Later slices

- [x] Task 6: preflight BLOCKED reasons (AC02–AC04) — pytest snapshots only; live Editor NOT_RUN
- [x] Task 7: coordinator state machine + journal (AC12–AC15) — RecordingGateway; live NOT_RUN
- [ ] Task 8: Hall path expected scenes + adapter assertions (AC06–AC08) — hops frozen + `HallQaRouteAssertion` EditMode; live Hall→Kitchen NOT_RUN
- [ ] Task 9: live Unity run (AC19) — Editor exclusive
- [ ] Task 10: independent review (AC23)

## Spec coverage (self-review)

| Spec | Task |
|---|---|
| §4.1 plan fields, hashes, timeouts | Task 1 |
| §6.1 scenario priority | Task 2 |
| §6.2 aggregate + exclusions | Task 2 |
| §4.4 artifact integrity | Task 3 |
| §4.3 report + review field | Task 4 |
| §4.3 test counts / AC21 | Task 5 |
| AC02–AC23 remainder | Later slices; not claimed passed |

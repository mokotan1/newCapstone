# Cursor Agent Connection Reliability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Codex-to-Cursor CLI calls verifiable and stable through one explicit Windows CLI/profile context, a read-only connection probe, structured run evidence, and reliable timeout/cancellation handling.

**Architecture:** Keep `scripts/run-cursor-subagent.ps1` as the only Cursor CLI entry point. Extend it to report sanitized CLI/profile diagnostics, refuse ambiguous or unintended API-key authentication, gate work calls behind status plus probe, and run each call as one bounded process with a local run record. Update the operator guide and add isolated wrapper tests using a fake CLI so no Cursor account is needed for most checks.

**Tech Stack:** Windows PowerShell, installed Cursor Agent CLI, Pester 3.4, .NET `System.Diagnostics.Process`.

**Spec:** `docs/superpowers/specs/2026-09-28-cursor-agent-connection-design.md`

## Global Constraints

- The only repository entry point that starts Cursor CLI is `scripts/run-cursor-subagent.ps1`.
- Account login credentials, tokens, and API key values must never be stored in the repository, command line, or logs.
- If `CURSOR_API_KEY` is present, account-based CLI operations fail closed with `api-key-env-present`; this plan adds no API-key opt-in path.
- Cursor Composer handles implementation; Cursor Auto in ask mode handles the independent read-only review.
- Each Cursor operation is one blocking call; do not poll its process or write this checkout while it is running.
- Default operation timeout is 20 minutes; timeout and cancellation must not leave a Cursor process running.
- Do not use the API-key route implicitly; no commit, push, Unity Editor operation, or unrelated QA/game-code change is in scope.

## Review Focus

- Multiple `agent` executables or profile environment variables resolve to the wrong account — pin the selected executable and test the reported identity context.
- `agent status` output or exit code indicates no login — ensure probe and work invocation are blocked with a sanitized auth failure.
- `CURSOR_API_KEY` is present — ensure its value is never surfaced and the account-login route is not silently replaced.
- Probe returns only partial stream output or no final response — ensure it is a failure despite an exit code that looks successful.
- Timeout or Ctrl+C interrupts the wrapper while a child survives — ensure the whole process tree is terminated before another run begins.

---

### Task 1: Make CLI and profile diagnostics deterministic

**Files:**
- Modify: `scripts/run-cursor-subagent.ps1`
- Test: `scripts/tests/Run-CursorSubagent.Tests.ps1`

**Interfaces:**
- Add optional `-AgentPath` so tests and operators can select the exact installed CLI executable. If omitted, resolve `agent` once and retain its absolute path for all steps in that invocation.
- Add `-Diagnose` for sanitized JSON containing `agentPath`, `agentVersion`, `windowsUser`, `userProfile`, `home`, `apiKeyPresent`, `authStatus`, and `exitCode`. Never include account email, credential content, or API key value. When `apiKeyPresent` is true, fail closed before `status`, Probe, or a work operation can use it.
- Preserve `-AuthStatus` as a concise human-readable form backed by the same diagnostic implementation.

- [ ] **Step 1: Add failing Pester tests for deterministic executable and sanitized diagnostics**

Test cases: exact `-AgentPath` is used for version/status; missing CLI is classified as `tool-missing`; auth nonzero or “not logged in” is reported as unauthenticated; `CURSOR_API_KEY` is reported only as a boolean and blocks account-based calls; captured output contains no fake secret marker.

- [ ] **Step 2: Run the focused Pester file and confirm the new cases fail**

Run: `Invoke-Pester -Script 'scripts/tests/Run-CursorSubagent.Tests.ps1' -PassThru`  
Expected: new behavior assertions fail before implementation.

- [ ] **Step 3: Implement `-AgentPath` and a single CLI resolver in `scripts/run-cursor-subagent.ps1`**

Resolve once, canonicalize the absolute path, and use that same executable for `--version`, `status`, Probe, and work. Do not print sensitive environment values.

- [ ] **Step 4: Implement sanitized `-Diagnose` and consistent `-AuthStatus` result mapping**

Map a successful authenticated status to `authenticated`; “not logged in” or nonzero status to `unauthenticated`; executable discovery failure to `tool-missing`. Keep machine JSON stable and human output concise.

- [ ] **Step 5: Run focused Pester tests and confirm the new cases pass**

Run: `Invoke-Pester -Script 'scripts/tests/Run-CursorSubagent.Tests.ps1' -PassThru`  
Expected: all Task 1 cases pass, with no secret marker in output.

### Task 2: Gate calls behind a read-only probe and record run evidence

**Files:**
- Modify: `scripts/run-cursor-subagent.ps1`
- Test: `scripts/tests/Run-CursorSubagent.Tests.ps1`

**Interfaces:**
- Add `-Probe` as a mutually exclusive operation with `-AuthLogin`, `-AuthStatus`, `-Diagnose`, and `-Prompt` work calls.
- Use `$Workspace` as the explicit probe context; the probe is read-only, asks for a fixed recognizable response, and succeeds only when the CLI exits successfully and emits a non-empty final assistant result.
- Write `run.json`, `stdout.log`, and `stderr.log` under `.harness/runs/cursor-agent/<runId>/`; store no raw prompt or credentials. `run.json` records the fields listed in the design.

- [ ] **Step 1: Add failing fake-CLI tests for probe success, missing final result, wrong workspace, and refusal when auth fails**

The fake CLI emits controlled stream-json lines. Assert probe cannot edit files and no Composer/Auto command runs unless both status and probe pass.

- [ ] **Step 2: Run the focused Pester tests and confirm probe assertions fail**

Run: `Invoke-Pester -Script 'scripts/tests/Run-CursorSubagent.Tests.ps1' -PassThru`  
Expected: new probe tests fail before implementation.

- [ ] **Step 3: Implement mutually exclusive operation parsing and read-only `-Probe`**

Use the exact executable from Task 1 and explicit absolute workspace. Parse stream-json sufficiently to distinguish initialization, final assistant result, and failure; do not treat partial output as success.

- [ ] **Step 4: Implement sanitized per-run metadata and output capture**

Create a unique UTC/timestamp-plus-random `runId` directory, record requested and observed model separately, and never save the full prompt or secret-bearing environment values.

- [ ] **Step 5: Gate Composer and Auto operations on authenticated status plus successful Probe**

Return an explicit auth/probe failure before starting a work call. Preserve Composer `composer-2.5` and Auto `auto` with ask mode.

- [ ] **Step 6: Run all wrapper Pester tests and confirm probe and evidence cases pass**

Run: `Invoke-Pester -Script 'scripts/tests/Run-CursorSubagent.Tests.ps1' -PassThru`  
Expected: all wrapper tests pass; outputs and metadata contain no fake secret marker.

### Task 3: Bound execution lifetime and document recovery

**Files:**
- Modify: `scripts/run-cursor-subagent.ps1`
- Modify: `docs/development/cursor-agent-access.md`
- Test: `scripts/tests/Run-CursorSubagent.Tests.ps1`

**Interfaces:**
- Add `-TimeoutSeconds` with default `1200` and validate it is positive.
- Execute work through a .NET process handle that can capture stdout/stderr, wait once up to the deadline, and terminate the entire child process tree after timeout or cancellation.
- Report one of `completed`, `failed`, `unauthenticated`, `timed-out`, or `cancelled` in the result metadata.

- [ ] **Step 1: Add failing Pester tests for timeout, cancellation, nonzero exit, and re-run after termination**

Use a fake CLI that starts a child process then blocks. Assert timeout/cancel removes both fake parent and child and a subsequent fake run can start.

- [ ] **Step 2: Run focused lifecycle tests and confirm they fail against current direct invocation**

Run: `Invoke-Pester -Script 'scripts/tests/Run-CursorSubagent.Tests.ps1' -PassThru`  
Expected: child-survival test fails before implementation.

- [ ] **Step 3: Implement bounded process execution and full process-tree cleanup**

Use the timeout from `-TimeoutSeconds`; write final exit classification and duration into `run.json`. Do not automatically retry a timed-out or cancelled prompt.

- [ ] **Step 4: Update `docs/development/cursor-agent-access.md` with the diagnostic → status → probe → work flow**

Document the one-time interactive CLI login in the same user/profile context, safe log locations, failure states, and recovery steps. State explicitly that secrets are not written and API-key authentication is not an implicit fallback.

- [ ] **Step 5: Run all wrapper Pester tests and check the documented commands**

Run: `Invoke-Pester -Script 'scripts/tests/Run-CursorSubagent.Tests.ps1' -PassThru`  
Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/run-cursor-subagent.ps1 -DryRun -Mode composer -Workspace (Get-Location).Path -Prompt 'dry-run'`  
Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/run-cursor-subagent.ps1 -DryRun -Mode auto -Workspace (Get-Location).Path -Prompt 'dry-run'`  
Expected: tests pass and both dry-runs show the intended workspace and model/mode without contacting Cursor.

- [ ] **Step 6: Verify live authentication and probe in the wrapper environment**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/run-cursor-subagent.ps1 -Diagnose -Workspace (Get-Location).Path`  
Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/run-cursor-subagent.ps1 -Probe -Workspace (Get-Location).Path`  
Expected: authenticated status, successful read-only final response, and a run record with the exact workspace. If auth is absent in this environment, stop before implementation calls and report that the user needs to complete the wrapper's interactive login once.

### Completion gates after this plan

- Run a Cursor Composer implementation task and a separate Cursor Auto read-only review using the verified wrapper; do not write this checkout during either call.
- After the connection is proven stable, resume the already-started QA autorun implementation as a separate scoped task; it is not part of the connection wrapper's file changes.
- Do not commit, push, or claim `verified` until tests, live probe, and required independent review pass.

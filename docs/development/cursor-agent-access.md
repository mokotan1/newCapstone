# Cursor agent access

The repository entry point for Cursor subagents is
`scripts/run-cursor-subagent.ps1`. It is the only supported wrapper for this
checkout and performs one blocking invocation per operation.

## Connection flow

Use the same Windows user profile for interactive login and wrapper calls.

1. **Diagnose** — sanitized JSON with CLI path, version, profile paths, and auth
   classification. No email, tokens, or API key values are printed.
2. **Auth status** — concise human-readable login state from the same diagnostic
   path as `-Diagnose`.
3. **Probe** — read-only ask against the selected `-Workspace`. Succeeds only
   when the CLI exits cleanly and returns a non-empty final assistant result.
4. **Work** — Composer (`composer-2.5`) or Auto review (`auto`, ask mode) runs
   only after the selected authentication route succeeds and the same route
   passes a probe in that invocation.

Direct account-based routes fail closed if `CURSOR_API_KEY` is set. API-key use
is an explicit opt-in on the local bridge worker; the key is never added to the
request queue, command line, run record, or captured CLI output.

## Commands

```powershell
Get-Command agent
.\scripts\run-cursor-subagent.ps1 -Diagnose -Workspace (Get-Location).Path
.\scripts\run-cursor-subagent.ps1 -AuthStatus
.\scripts\run-cursor-subagent.ps1 -Probe -Workspace (Get-Location).Path
```

`-AuthLogin` starts the provider's interactive login flow without accepting a
token argument and without writing credentials to the repository. Complete login
once in the same user/profile context you use for Codex or automation.

For an interactive terminal that should authenticate on demand, add
`-LoginIfUnauthenticated` to a `-Probe` or work invocation. The wrapper checks
status, runs `agent login` in that same terminal when needed, rechecks status,
and only then continues. This switch requires a visible interactive terminal;
it cannot bridge the separate `codexsandboxoffline` user used by non-interactive
Codex shell calls.

Use `-DryRun` to inspect routing (workspace, mode, model) without contacting
Cursor. Auto uses `--model auto` with `--mode ask`; Composer uses
`--model composer-2.5` without ask mode.

## Local PowerShell bridge

Use the bridge when the Codex shell is not logged into the Cursor CLI but a
PowerShell running under the logged-in Windows account can access this checkout.
Start the worker in a visible PowerShell window and leave it open:

```powershell
Set-Location D:\Capstone\newCapstone
.\scripts\cursor-agent-bridge.ps1 -Workspace (Get-Location).Path
```

If this PowerShell session has the key as `$env:cursor`, explicitly select the
API-key route when starting the worker:

```powershell
.\scripts\cursor-agent-bridge.ps1 -Workspace (Get-Location).Path `
  -UseCursorApiKeyEnvironmentVariable
```

The worker passes it to the Cursor child process as the official
`CURSOR_API_KEY` environment variable. This selects Cursor's API-key
authentication; it does not fall back to browser account authentication.
Usage is charged under the Cursor account's current plan and usage rules. The
key is held in process memory and is not written to files.

The worker holds an exclusive local lock and processes one request at a time.
The configured workspace is its allowlist; requests for that directory or its
children are accepted. For browser authentication, log in from that same
PowerShell account before starting the worker.

Codex continues to use the single repository wrapper, adding
`-ViaLocalBridge`:

```powershell
.\scripts\run-cursor-subagent.ps1 -ViaLocalBridge -Mode auto `
  -Workspace (Get-Location).Path -Prompt 'Review the current changes read-only.'

.\scripts\run-cursor-subagent.ps1 -ViaLocalBridge -Mode composer `
  -Workspace (Get-Location).Path -Prompt 'Implement the requested change.'
```

The wrapper blocks for the result. It returns `bridge-unavailable` when the
worker is not running, `bridge-busy` when another request is active, and
`timed-out` if the worker does not finish in time. A timeout sends a cancellation
marker; the worker terminates the Cursor wrapper process tree. Requests already
claimed or queued when a worker exits are marked interrupted and are not replayed.

Requests are written briefly under
`.harness/runtime/cursor-agent-bridge/` and deleted after processing. That path
is gitignored. The queue contains the requested prompt and result, but no account
email, login token, or `CURSOR_API_KEY` value. API-key opt-in is controlled by
the worker, never by a queued request. A request left behind after a forced
process kill should be inspected and removed before restarting the worker.

Optional `-AgentPath` pins the exact `agent` executable for an invocation.
Optional `-TimeoutSeconds` defaults to `1200` (20 minutes).

## Run evidence

Each `-Probe` or work call writes non-secret metadata under
`.harness/runs/cursor-agent/<runId>/`:

- `run.json` — timing, exit classification, workspace, requested/observed model
- `stdout.log` / `stderr.log` — captured CLI streams (no raw work prompt)

Classifications include `completed`, `failed`, `unauthenticated`,
`api-key-env-present`, `timed-out`, and `cancelled`.

## Recovery

| Symptom | Action |
|--------|--------|
| `tool-missing` | Install Cursor CLI; ensure `agent` is on PATH or pass `-AgentPath`. |
| `unauthenticated` | Run `-AuthLogin` interactively in this profile; recheck `-AuthStatus`. |
| `api-key-env-present` | The worker detected API-key authentication; restart without `-UseCursorApiKeyEnvironmentVariable` to use browser account login. |
| Probe `failed` | Compare `-Diagnose` with your terminal `agent status`; retry `-Probe`. |
| `timed-out` / `cancelled` | Wrapper terminates the child process tree; start a new run (new `runId`). |

The wrapper does not auto-retry timed-out or cancelled work. Do not edit this
checkout while a blocking Cursor call is in progress.

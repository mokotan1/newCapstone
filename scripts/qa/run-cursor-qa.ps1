<#
.SYNOPSIS
    Orchestrate a Cursor QA subagent run (Task 11): prefer Cursor's
    interactive Task custom-agent delegation, and fall back to sequential
    `cursor-agent -p --output-format json` invocations when it is unavailable.

.DESCRIPTION
    This wrapper implements the orchestration fallback described in
    .cursor/rules/qa-subagent-orchestration.mdc:

      1. Preflight: is the Cursor CLI (`cursor-agent`) present, and does its
         `--help` output expose every option this pipeline depends on
         (-p, --output-format, --workspace, --prompt)? If any option is
         missing, fail with an actionable message instead of guessing at a
         different CLI surface.
      2. Preflight: does the caller report that the current Cursor session
         exposes custom Task delegation (-TaskDelegationAvailable, or the
         CURSOR_QA_TASK_DELEGATION=1 environment variable)? A standalone
         PowerShell process cannot introspect the interactive IDE session, so
         this is an explicit signal from the caller (normally the
         Cursor agent driving the run via its own Task tool).
           - If available: this script does not invoke any role itself. It
             writes a manifest recording that delegation should be used and
             exits 0; the calling Cursor agent is expected to delegate each
             role via Task instead.
           - If unavailable: invoke each requested role sequentially via
             scripts/qa/invoke-qa-agent.ps1, preserving the JSON envelope
             contract and rejecting a second concurrent qa-playtester lease
             owner.
      3. Every run writes docs/qa/runs/<run-id>/orchestration-manifest.json
         capturing the Cursor CLI version, preflight results, and per-role
         outcomes.

    Normal command approvals are left enabled throughout: this script never
    passes -f/--force, --yolo, --auto-review, or --approve-mcps to
    cursor-agent.

.PARAMETER TaskId
    Identifier for this QA task (e.g. "qa-001").

.PARAMETER ScenarioIds
    Scenario IDs in scope for this run (e.g. "kitchen.faucet-key").

.PARAMETER Roles
    Ordered list of roles to run in the sequential fallback path. Defaults to
    the read-only analysis roles followed by the sole Unity-mutating role and
    the independent evidence reviewer:
    qa-inventory, qa-scenario-author, qa-playtester, qa-evidence-reviewer.
    qa-coordinator is the interactive/delegating role and is normally driven
    by the calling Cursor agent itself, not re-invoked headlessly here.

.PARAMETER TaskDelegationAvailable
    Switch indicating the current Cursor session exposes custom Task
    delegation. When set, this script only records intent to delegate and
    performs no direct cursor-agent invocation.

.PARAMETER Workspace
    Workspace directory passed through to invoke-qa-agent.ps1 / cursor-agent.

.PARAMETER RepoRoot
    Repository root. Defaults to two levels above this script.

.PARAMETER RunId
    Optional explicit run id suffix. Defaults to a random 8-character id.

.EXAMPLE
    ./scripts/qa/run-cursor-qa.ps1 -TaskId qa-001 -ScenarioIds @('kitchen.faucet-key')

.EXAMPLE
    ./scripts/qa/run-cursor-qa.ps1 -TaskId qa-001 -TaskDelegationAvailable
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TaskId,

    [string[]]$ScenarioIds = @(),

    [string[]]$Roles = @('qa-inventory', 'qa-scenario-author', 'qa-playtester', 'qa-evidence-reviewer'),

    [switch]$TaskDelegationAvailable,

    [string]$Workspace = (Get-Location).Path,

    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,

    [string]$RunId
)

$ErrorActionPreference = 'Stop'

$KnownRoles = @('qa-coordinator', 'qa-inventory', 'qa-scenario-author', 'qa-playtester', 'qa-evidence-reviewer')
$RequiredCliOptions = @('-p', '--output-format', '--workspace', '--prompt')

function Exit-WithFatalError {
    # Writes directly to the error stream and exits with the given code.
    # Deliberately bypasses Write-Error: with $ErrorActionPreference = 'Stop'
    # (set above), Write-Error becomes a terminating exception, which would
    # skip the explicit `exit $Code` below and leave the process exit code
    # non-deterministic. Callers (including CI) need the documented exit
    # codes to be real.
    param(
        [Parameter(Mandatory)] [string]$Message,
        [Parameter(Mandatory)] [int]$Code
    )
    [Console]::Error.WriteLine($Message)
    exit $Code
}

foreach ($role in $Roles) {
    if ($KnownRoles -notcontains $role) {
        Exit-WithFatalError -Code 2 -Message "Unknown QA role '$role'. Known roles: $($KnownRoles -join ', '). Do not invent conflicting agent names."
    }
}

# --- Resolve run id / evidence root (must live under docs/qa/runs/) --------
if (-not $RunId) {
    $RunId = [Guid]::NewGuid().ToString('N').Substring(0, 8)
}
$utcStamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH-mm-ssZ')
$evidenceRootRelative = "docs/qa/runs/$utcStamp-run-$RunId"
$evidenceRootAbsolute = Join-Path $RepoRoot $evidenceRootRelative
New-Item -ItemType Directory -Path $evidenceRootAbsolute -Force | Out-Null

$manifestPath = Join-Path $evidenceRootAbsolute 'orchestration-manifest.json'
$leasePath = Join-Path $evidenceRootAbsolute '.qa-playtester.lease'

function Write-RunManifest {
    param([Parameter(Mandatory)] [System.Collections.Specialized.OrderedDictionary]$Manifest)
    $Manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8
}

function Test-CursorAgentPreflight {
    $cmd = Get-Command 'cursor-agent' -ErrorAction SilentlyContinue
    if (-not $cmd) {
        return [ordered]@{
            Available     = $false
            Path          = $null
            Version       = $null
            HelpText      = $null
            MissingOptions = @()
        }
    }

    $helpText = (& $cmd.Source --help 2>&1 | Out-String)
    $helpExit = $LASTEXITCODE
    $version = (& $cmd.Source --version 2>&1 | Out-String).Trim()

    $missing = @()
    if ($helpExit -eq 0) {
        foreach ($option in $RequiredCliOptions) {
            if ($helpText -notmatch [regex]::Escape($option)) {
                $missing += $option
            }
        }
    }
    else {
        $missing = $RequiredCliOptions
    }

    return [ordered]@{
        Available      = $true
        Path           = $cmd.Source
        Version        = $version
        HelpText       = $helpText
        MissingOptions = $missing
    }
}

# --- Task-delegation signal (interactive session cannot be introspected
#     from a standalone process, so this is an explicit caller signal) ------
$taskDelegationRequested = [bool]$TaskDelegationAvailable
if (-not $taskDelegationRequested -and $env:CURSOR_QA_TASK_DELEGATION -eq '1') {
    $taskDelegationRequested = $true
}

$cliInfo = Test-CursorAgentPreflight

$runManifest = [ordered]@{
    taskId                   = $TaskId
    scenarioIds              = @($ScenarioIds)
    evidenceRoot             = $evidenceRootRelative
    startedAtUtc             = (Get-Date).ToUniversalTime().ToString('o')
    cursorCliAvailable       = $cliInfo.Available
    cursorVersion            = $cliInfo.Version
    cursorCliMissingOptions  = @($cliInfo.MissingOptions)
    taskDelegationRequested  = $taskDelegationRequested
    rolesRequested            = @($Roles)
    roles                    = @()
    status                   = 'ready'
}

# --- Path A: interactive Task delegation is available ----------------------
if ($taskDelegationRequested) {
    Write-Output 'Task custom-agent delegation reported available for this Cursor session.'
    Write-Output 'This script performs no direct cursor-agent invocation in this mode.'
    Write-Output 'Delegate each role via the Task tool using .cursor/agents/<role>.md, in this order:'
    foreach ($role in $Roles) {
        Write-Output "  - $role"
    }
    Write-Output 'Parallelize only read-only roles (qa-inventory, qa-scenario-author review, qa-evidence-reviewer);'
    Write-Output 'route all Unity mutation through a single qa-playtester job.'

    $runManifest['status'] = 'delegated'
    $runManifest['finishedAtUtc'] = (Get-Date).ToUniversalTime().ToString('o')
    Write-RunManifest -Manifest $runManifest
    Write-Output "Manifest written: $manifestPath"
    exit 0
}

# --- Path B: fall back to sequential cursor-agent CLI invocations ----------
if (-not $cliInfo.Available) {
    $runManifest['status'] = 'blocked'
    $runManifest['finishedAtUtc'] = (Get-Date).ToUniversalTime().ToString('o')
    Write-RunManifest -Manifest $runManifest
    Exit-WithFatalError -Code 9009 -Message (
        'Neither Cursor Task delegation (-TaskDelegationAvailable) nor the cursor-agent CLI is ' +
        'available. Install the Cursor CLI (https://cursor.com/cli) so scripts/qa/run-cursor-qa.ps1 ' +
        'can fall back to sequential ''cursor-agent -p --output-format json'' invocations, or re-run ' +
        'inside a Cursor session that supports custom Task delegation and pass -TaskDelegationAvailable.'
    )
}

if ($cliInfo.MissingOptions.Count -gt 0) {
    $runManifest['status'] = 'blocked'
    $runManifest['finishedAtUtc'] = (Get-Date).ToUniversalTime().ToString('o')
    Write-RunManifest -Manifest $runManifest
    Exit-WithFatalError -Code 9010 -Message (
        "cursor-agent CLI (version $($cliInfo.Version)) does not expose: " +
        "$($cliInfo.MissingOptions -join ', '). Run 'cursor-agent --help' to inspect the options " +
        'actually available, then upgrade/downgrade the Cursor CLI or update scripts/qa/*.ps1 to ' +
        'match before retrying. Refusing to invoke roles with an unverified CLI surface.'
    )
}

$invokeScript = Join-Path $PSScriptRoot 'invoke-qa-agent.ps1'
if (-not (Test-Path -LiteralPath $invokeScript)) {
    Exit-WithFatalError -Code 11 -Message "Required helper script missing: $invokeScript"
}

foreach ($role in $Roles) {
    $roleEntry = [ordered]@{
        role      = $role
        startedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    }

    if ($role -eq 'qa-playtester') {
        if (Test-Path -LiteralPath $leasePath) {
            $existingLease = Get-Content -LiteralPath $leasePath -Raw
            Exit-WithFatalError -Code 12 -Message "QA lease already held: $existingLease. Rejecting second qa-playtester lease owner for this run."
        }
        [ordered]@{
            ownerId       = "$PID-$RunId"
            acquiredAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        } | ConvertTo-Json | Set-Content -LiteralPath $leasePath -Encoding utf8
    }

    $roleExit = 1
    try {
        & $invokeScript -AgentName $role -TaskId $TaskId -ScenarioIds $ScenarioIds `
            -EvidenceRoot $evidenceRootRelative -Workspace $Workspace -RepoRoot $RepoRoot
        $roleExit = $LASTEXITCODE
    }
    finally {
        if ($role -eq 'qa-playtester' -and (Test-Path -LiteralPath $leasePath)) {
            Remove-Item -LiteralPath $leasePath -Force
        }
    }

    $roleEntry['exitCode'] = $roleExit
    $roleEntry['finishedAtUtc'] = (Get-Date).ToUniversalTime().ToString('o')
    $runManifest['roles'] += $roleEntry

    if ($roleExit -ne 0) {
        $runManifest['status'] = 'fail'
        $runManifest['finishedAtUtc'] = (Get-Date).ToUniversalTime().ToString('o')
        Write-RunManifest -Manifest $runManifest
        Exit-WithFatalError -Code $roleExit -Message "Role '$role' exited with code $roleExit. Halting sequential fallback run."
    }
}

$runManifest['status'] = 'ready'
$runManifest['finishedAtUtc'] = (Get-Date).ToUniversalTime().ToString('o')
Write-RunManifest -Manifest $runManifest

Write-Output "QA orchestration run complete. Manifest: $manifestPath"
exit 0

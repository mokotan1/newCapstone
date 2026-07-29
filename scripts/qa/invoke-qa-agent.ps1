<#
.SYNOPSIS
    Invoke a single Cursor QA role agent (Task 11) via the `cursor-agent` CLI
    non-interactive fallback path.

.DESCRIPTION
    This is the low-level building block used by scripts/qa/run-cursor-qa.ps1
    when Cursor's interactive Task custom-agent delegation is unavailable.

    It:
      1. Loads the role prompt from .cursor/agents/<AgentName>.md.
      2. Runs a Cursor CLI preflight (`cursor-agent --help` + `--version`) and
         verifies every option this wrapper depends on is actually supported
         by the installed CLI. If any option is missing, it fails fast with
         an actionable message instead of guessing at a different CLI
         surface.
      3. Builds the bounded JSON handoff envelope required by
         .cursor/rules/qa-subagent-orchestration.mdc.
      4. Invokes:
           cursor-agent -p --output-format json --workspace <Workspace> --prompt <Prompt>
         Normal command approvals are left enabled: this script never passes
         --force, --yolo, --auto-review, or --approve-mcps.

.PARAMETER AgentName
    One of: qa-coordinator, qa-inventory, qa-scenario-author, qa-playtester,
    qa-evidence-reviewer. Must match a file under .cursor/agents/<AgentName>.md.

.PARAMETER TaskId
    Identifier for this QA task (e.g. "qa-001"). Included in the JSON envelope.

.PARAMETER ScenarioIds
    Scenario IDs in scope for this handoff (e.g. "kitchen.faucet-key").

.PARAMETER EvidenceRoot
    Repo-relative evidence root, must live under docs/qa/runs/.

.PARAMETER Status
    Initial status to seed the outbound envelope with (default: "ready").

.PARAMETER Prompt
    Additional task-specific instructions appended after the role definition
    and JSON envelope.

.PARAMETER Workspace
    Workspace directory passed to `cursor-agent --workspace`. Defaults to the
    current working directory.

.PARAMETER RepoRoot
    Repository root used to resolve .cursor/agents and the evidence root.
    Defaults to two levels above this script (scripts/qa/..\..).

.PARAMETER LogDirectory
    Directory to write the raw CLI transcript to. Defaults to
    <RepoRoot>/<EvidenceRoot>.

.EXAMPLE
    ./scripts/qa/invoke-qa-agent.ps1 -AgentName qa-inventory -TaskId qa-001 `
        -ScenarioIds @('kitchen.faucet-key') -EvidenceRoot 'docs/qa/runs/2026-07-22T10-00-00Z-run-abcd1234'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('qa-coordinator', 'qa-inventory', 'qa-scenario-author', 'qa-playtester', 'qa-evidence-reviewer')]
    [string]$AgentName,

    [Parameter(Mandatory = $true)]
    [string]$TaskId,

    [string[]]$ScenarioIds = @(),

    [Parameter(Mandatory = $true)]
    [string]$EvidenceRoot,

    [string]$Status = 'ready',

    [string]$Prompt = '',

    [string]$Workspace = (Get-Location).Path,

    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,

    [string]$LogDirectory
)

$ErrorActionPreference = 'Stop'

# Options this wrapper relies on. Every one of these must appear literally in
# `cursor-agent --help` output, otherwise we refuse to guess at a different
# CLI surface and fail with an actionable message instead.
$RequiredCliOptions = @('-p', '--output-format', '--workspace', '--prompt')

function Get-CursorAgentCommand {
    $cmd = Get-Command 'cursor-agent' -ErrorAction SilentlyContinue
    if (-not $cmd) {
        return $null
    }
    return $cmd.Source
}

function Exit-WithFatalError {
    # Writes directly to the error stream and exits with the given code.
    # Deliberately bypasses Write-Error: with $ErrorActionPreference = 'Stop'
    # (set above), Write-Error becomes a terminating exception, which would
    # skip the explicit `exit $Code` below and leave the process exit code
    # non-deterministic. Callers need the documented exit codes to be real.
    param(
        [Parameter(Mandatory)] [string]$Message,
        [Parameter(Mandatory)] [int]$Code
    )
    [Console]::Error.WriteLine($Message)
    exit $Code
}

function Assert-CursorAgentSupportsOption {
    param(
        [Parameter(Mandatory)] [string]$HelpText,
        [Parameter(Mandatory)] [string]$Option,
        [Parameter(Mandatory)] [string]$CursorVersion
    )
    if ($HelpText -notmatch [regex]::Escape($Option)) {
        throw (
            "cursor-agent CLI (version $CursorVersion) does not expose the option '$Option' " +
            "that scripts/qa/invoke-qa-agent.ps1 depends on. Run 'cursor-agent --help' to inspect " +
            "the options actually available, then either upgrade/downgrade the Cursor CLI to a " +
            "version documented in docs/superpowers/plans/2026-07-22-cursor-subagent-qa-driver.md " +
            "Task 11, or update this wrapper to match the installed CLI surface before retrying."
        )
    }
}

# --- 1. Resolve the role prompt -------------------------------------------
$agentDefinitionPath = Join-Path $RepoRoot ".cursor\agents\$AgentName.md"
if (-not (Test-Path -LiteralPath $agentDefinitionPath)) {
    Exit-WithFatalError -Message "Agent definition not found: $agentDefinitionPath. Create it before invoking this role." -Code 10
}
$agentDefinition = Get-Content -LiteralPath $agentDefinitionPath -Raw

# --- 2. Preflight: locate cursor-agent -------------------------------------
$cursorAgentPath = Get-CursorAgentCommand
if (-not $cursorAgentPath) {
    Exit-WithFatalError -Code 9009 -Message (
        "cursor-agent CLI not found on PATH. Install the Cursor CLI " +
        "(https://cursor.com/cli) or ensure it is on PATH, then retry. " +
        "This wrapper cannot invoke the '$AgentName' role without it. " +
        "Use Cursor's interactive Task tool delegation instead if available."
    )
}

# --- 3. Preflight: verify --help exposes every option this wrapper uses ---
$helpOutput = (& $cursorAgentPath --help 2>&1 | Out-String)
$helpExitCode = $LASTEXITCODE
if ($helpExitCode -ne 0) {
    Exit-WithFatalError -Code $helpExitCode -Message "cursor-agent --help failed (exit $helpExitCode). Cannot verify CLI compatibility; aborting before invoking '$AgentName'."
}

$cursorVersion = (& $cursorAgentPath --version 2>&1 | Out-String).Trim()

try {
    foreach ($option in $RequiredCliOptions) {
        Assert-CursorAgentSupportsOption -HelpText $helpOutput -Option $option -CursorVersion $cursorVersion
    }
}
catch {
    Exit-WithFatalError -Message $_.Exception.Message -Code 9010
}

# --- 4. Build the bounded JSON handoff envelope ----------------------------
$envelope = [ordered]@{
    taskId       = $TaskId
    scenarioIds  = @($ScenarioIds)
    evidenceRoot = $EvidenceRoot
    status       = $Status
    findings     = @()
}
$envelopeJson = $envelope | ConvertTo-Json -Depth 6

$fullPrompt = @"
$agentDefinition

## Task packet (JSON handoff envelope)

``````json
$envelopeJson
``````

## Additional instructions

$Prompt
"@

# --- 5. Ensure the evidence/log directory exists ---------------------------
if (-not $LogDirectory) {
    $LogDirectory = Join-Path $RepoRoot $EvidenceRoot
}
if (-not (Test-Path -LiteralPath $LogDirectory)) {
    New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null
}

$timestamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
$logPath = Join-Path $LogDirectory "$AgentName-$timestamp.json"

# --- 6. Invoke cursor-agent --------------------------------------------------
# Normal command approvals remain enabled: no -f/--force, --yolo,
# --auto-review, or --approve-mcps flags are ever passed by this wrapper.
$cliArgs = @(
    '-p'
    '--output-format', 'json'
    '--workspace', $Workspace
    '--prompt', $fullPrompt
)

Write-Verbose "Invoking cursor-agent for role '$AgentName' (cursor-agent version $cursorVersion)"
& $cursorAgentPath @cliArgs 2>&1 | Tee-Object -FilePath $logPath
$exitCode = $LASTEXITCODE

[ordered]@{
    agentName     = $AgentName
    cursorVersion = $cursorVersion
    exitCode      = $exitCode
    logPath       = $logPath
} | ConvertTo-Json -Depth 4

exit $exitCode

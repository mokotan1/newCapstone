#Requires -Version 5.1
<#
.SYNOPSIS
  Unity harness dispatcher. Completion criteria live in .harness/unity-verification.md.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("probe", "compile", "console", "test", "qa")]
    [string]$Operation,

    [string]$ProjectPath = "disputatio",

    [ValidateSet("legacy-unity-cli", "official-unity-cli")]
    [string]$Backend = "",

    [string]$Mode = "EditMode",
    [string]$Filter = "",
    [int]$TimeoutSec = 300,
    [string]$QaAction = "status",
    [string]$OwnerId = "",
    [string]$ScenarioId = ""
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot ".harness\unity-toolchain.json"))) {
    $RepoRoot = $PSScriptRoot
}

$ToolchainPath = Join-Path $RepoRoot ".harness\unity-toolchain.json"
$Toolchain = Get-Content -LiteralPath $ToolchainPath -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $Backend) {
    $Backend = [string]$Toolchain.activeBackend
}

if ($Backend -eq "official-unity-cli") {
    & (Join-Path $PSScriptRoot "unity-harness\official.ps1") -Operation $Operation -ProjectPath $ProjectPath
    exit $LASTEXITCODE
}

& (Join-Path $PSScriptRoot "unity-harness\legacy.ps1") `
    -Operation $Operation `
    -ProjectPath $ProjectPath `
    -Mode $Mode `
    -Filter $Filter `
    -TimeoutSec $TimeoutSec `
    -QaAction $QaAction `
    -OwnerId $OwnerId `
    -ScenarioId $ScenarioId `
    -RepoRoot $RepoRoot
exit $LASTEXITCODE

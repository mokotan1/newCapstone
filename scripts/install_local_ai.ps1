#Requires -Version 5.1
<#!
.SYNOPSIS
  Plan or run first-run LiteRT-LM Gemma 4 E2B install on Windows.

.DESCRIPTION
  Shows license notice and approximate download size, then calls backend_ai/local_install.py.
  Does not download unless the user consents. Model files are kept after the game exits
  unless -RemoveModel is passed.
#>
[CmdletBinding()]
param(
    [switch]$Consent,
    [switch]$Offline,
    [switch]$RemoveModel,
    [switch]$Execute,
    [switch]$StartServices
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$NoticePath = Join-Path $RepoRoot "installer\licenses\NOTICE.md"
$Planner = Join-Path $RepoRoot "backend_ai\local_install.py"

if (-not (Test-Path $Planner)) {
    throw "Missing planner: $Planner"
}

if (Test-Path $NoticePath) {
    Write-Host ""
    Get-Content -Path $NoticePath -Encoding UTF8 | Write-Host
    Write-Host ""
}

if (-not $Consent -and -not $RemoveModel) {
    $answer = Read-Host "Type YES to consent to the Gemma 4 download/import (or Ctrl+C to cancel)"
    if ($answer -ne "YES") {
        Write-Error "Consent not given. No download started."
        exit 2
    }
    $Consent = $true
}

$pythonArgs = @($Planner)
if ($Consent) { $pythonArgs += "--consent" }
if ($Offline) { $pythonArgs += "--offline" }
if ($RemoveModel) { $pythonArgs += "--remove-model" }
if ($Execute) { $pythonArgs += "--execute" }

Push-Location (Join-Path $RepoRoot "backend_ai")
try {
    & python @pythonArgs
    $code = $LASTEXITCODE
}
finally {
    Pop-Location
}

if ($null -eq $code) { $code = 0 }
if ($code -ne 0) { exit $code }

if ($StartServices) {
    $serve = @(
        "uvx", "--from", "litert-lm==0.16.1", "litert-lm", "serve",
        "--host", "127.0.0.1", "--port", "9379"
    )
    Start-Process -FilePath $serve[0] -ArgumentList $serve[1..($serve.Length - 1)] -WorkingDirectory $RepoRoot | Out-Null

    $env:AI_PROVIDER = "local"
    Start-Process -FilePath "python" -ArgumentList @(
        "-m", "uvicorn", "main:app", "--host", "127.0.0.1", "--port", "8000"
    ) -WorkingDirectory (Join-Path $RepoRoot "backend_ai") | Out-Null

    $ready = $false
    for ($i = 0; $i -lt 30; $i++) {
        try {
            $health = Invoke-RestMethod -Uri "http://127.0.0.1:8000/" -TimeoutSec 2
            if ($health.local_runtime.model_available -eq $true) {
                $ready = $true
                break
            }
        } catch {
            Start-Sleep -Seconds 2
            continue
        }
        Start-Sleep -Seconds 2
    }

    if (-not $ready) {
        Write-Error "Loopback FastAPI started but local_runtime.model_available was not true. Chat UI will stay blocked."
        exit 3
    }
    Write-Host "Local AI ready on 127.0.0.1:8000"
}

exit 0

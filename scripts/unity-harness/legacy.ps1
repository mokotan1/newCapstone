#Requires -Version 5.1
param(
    [Parameter(Mandatory = $true)]
    [string]$Operation,
    [string]$ProjectPath = "disputatio",
    [string]$Mode = "EditMode",
    [string]$Filter = "",
    [int]$TimeoutSec = 300,
    [string]$QaAction = "status",
    [string]$OwnerId = "",
    [string]$ScenarioId = "",
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"
if (-not $RepoRoot) {
    $RepoRoot = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
}

function Write-HarnessResult {
    param([hashtable]$Fields)
    $payload = [ordered]@{
        schemaVersion      = "1"
        operation          = $Operation
        backend            = "legacy-unity-cli"
        toolVersion        = $null
        connectorVersion   = $null
        projectPath        = $ProjectPath
        editorPid          = $null
        startedAt          = (Get-Date).ToString("o")
        finishedAt         = (Get-Date).ToString("o")
        executionStatus    = "blocked"
        verificationStatus = "blocked"
        nativeExitCode     = $null
        artifacts          = @()
        errorCategory      = "none"
        resultKind         = "ok"
    }
    foreach ($key in $Fields.Keys) {
        $payload[$key] = $Fields[$key]
    }
    $payload | ConvertTo-Json -Compress
}

$expected = "disputatio"
$normalized = ($ProjectPath -replace "\\", "/").TrimEnd("/")
if ($normalized -ne $expected -and -not $normalized.EndsWith("/$expected")) {
    Write-HarnessResult @{
        executionStatus    = "blocked"
        verificationStatus = "blocked"
        errorCategory      = "invalid-input"
        resultKind         = "invalid-project"
    }
    exit 2
}

$wrapper = Join-Path $RepoRoot "scripts\unity-cli.cmd"
if (-not (Test-Path -LiteralPath $wrapper)) {
    Write-HarnessResult @{
        executionStatus    = "blocked"
        verificationStatus = "blocked"
        errorCategory      = "tool-missing"
        resultKind         = "tool-missing"
    }
    exit 3
}

switch ($Operation) {
    "probe" {
        Write-HarnessResult @{
            executionStatus    = "succeeded"
            verificationStatus = "not-applicable"
            errorCategory      = "none"
            resultKind         = "ok"
        }
        exit 0
    }
    "compile" {
        & $wrapper --project $ProjectPath editor refresh --compile
        $code = $LASTEXITCODE
        if ($code -ne 0) {
            Write-HarnessResult @{
                executionStatus    = "failed"
                verificationStatus = "failed"
                errorCategory      = "compilation"
                resultKind         = "compile-failed"
                nativeExitCode     = $code
            }
            exit $code
        }
        Write-HarnessResult @{
            executionStatus    = "succeeded"
            verificationStatus = "passed"
            errorCategory      = "none"
            nativeExitCode     = $code
        }
        exit 0
    }
    "console" {
        & $wrapper --project $ProjectPath console --type error,warning --lines 80
        $code = $LASTEXITCODE
        Write-HarnessResult @{
            executionStatus    = $(if ($code -eq 0) { "succeeded" } else { "failed" })
            verificationStatus = $(if ($code -eq 0) { "passed" } else { "failed" })
            nativeExitCode     = $code
        }
        exit $code
    }
    "test" {
        $cliArgs = @("--project", $ProjectPath, "test", "--mode", $Mode)
        if ($Filter) { $cliArgs += @("--filter", $Filter) }
        $output = & $wrapper @cliArgs 2>&1 | Out-String
        $code = $LASTEXITCODE
        $env:PYTHONPATH = $RepoRoot
        $classified = $output | & python -m scripts.unity_harness.classify_cli --native-exit $code
        Write-Output $classified
        exit $(if ($code -eq 0) { 0 } else { $code })
    }
    "qa" {
        $env:PYTHONPATH = $RepoRoot
        $tool = & python -c "from scripts.unity_harness.result_contract import map_qa_action; print(map_qa_action('$QaAction'))"
        if ($LASTEXITCODE -ne 0 -or -not $tool) {
            Write-HarnessResult @{
                executionStatus    = "blocked"
                verificationStatus = "blocked"
                errorCategory      = "invalid-input"
                resultKind         = "unknown-qa-action"
            }
            exit 2
        }
        $qaArgs = @("--project", $ProjectPath, $tool.Trim())
        if ($QaAction -in @("run", "start") -and $ScenarioId) {
            $params = @{ scenario_id = $ScenarioId } | ConvertTo-Json -Compress
            $qaArgs += @("--params", $params)
        }
        & $wrapper @qaArgs
        $code = $LASTEXITCODE
        Write-HarnessResult @{
            executionStatus    = $(if ($code -eq 0) { "succeeded" } else { "failed" })
            verificationStatus = $(if ($code -eq 0) { "passed" } else { "failed" })
            errorCategory      = $(if ($code -eq 0) { "none" } else { "editor-unavailable" })
            resultKind         = "qa-gateway"
            nativeExitCode     = $code
        }
        exit $code
    }
    default {
        Write-HarnessResult @{
            errorCategory      = "invalid-input"
            resultKind         = "unknown-operation"
        }
        exit 2
    }
}

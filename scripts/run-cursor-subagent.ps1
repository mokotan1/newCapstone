<#
.SYNOPSIS
    Single blocking entry point for Cursor subagent work.

.DESCRIPTION
    This wrapper is intentionally the only repository entry point that starts
    the installed Cursor `agent` CLI. Its invocation shape is the supported
    non-interactive form: `agent -p --mode ask --model <model>
    --output-format stream-json -- <prompt>`.
#>
[CmdletBinding()]
param(
    [string]$Prompt,
    [ValidateSet('auto', 'composer')] [string]$Mode = 'auto',
    [string]$Workspace = (Get-Location).Path,
    [string]$AgentPath,
    [switch]$Json,
    [switch]$DryRun,
    [switch]$AuthLogin,
    [switch]$AuthStatus,
    [switch]$Diagnose,
    [switch]$Probe,
    [switch]$LoginIfUnauthenticated,
    [switch]$ViaLocalBridge,
    [switch]$BridgeWorkerResult,
    [switch]$BridgeAllowApiKey,
    [string]$BridgeDirectory,
    [int]$TimeoutSeconds = 1200
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$script:ProbePromptToken = 'CURSOR_WRAPPER_PROBE_OK'
$script:ProbePrompt = "Read-only connection check. Reply with exactly this token and nothing else: $script:ProbePromptToken"

function Get-RepoRoot {
    return (Split-Path -Path $PSScriptRoot -Parent)
}

function Test-ApiKeyEnvPresent {
    return -not [string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable('CURSOR_API_KEY'))
}

function Resolve-AgentExecutable {
    param([string]$ExplicitPath)

    if ($ExplicitPath) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) {
            throw 'tool-missing'
        }
        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $agent = Get-Command 'agent' -ErrorAction SilentlyContinue
    if (-not $agent) {
        throw 'tool-missing'
    }
    return (Resolve-Path -LiteralPath $agent.Source).Path
}

function ConvertTo-ProcessArgumentString {
    param([string[]]$Arguments)

    $parts = foreach ($arg in $Arguments) {
        if ($null -eq $arg) {
            continue
        }
        if ($arg -match '[\s"]') {
            '"' + ($arg -replace '"', '\"') + '"'
        }
        else {
            $arg
        }
    }
    return ($parts -join ' ')
}

function Invoke-AgentRaw {
    param(
        [string]$Executable,
        [string[]]$Arguments,
        [int]$TimeoutSec,
        [string]$StdoutPath,
        [string]$StderrPath
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    if ($Executable -like '*.ps1') {
        $psi.FileName = (Get-Command powershell.exe -ErrorAction Stop).Source
        $psi.Arguments = '-NoProfile -ExecutionPolicy Bypass -File ' + (ConvertTo-ProcessArgumentString -Arguments (@($Executable) + $Arguments))
    }
    elseif ($Executable -like '*.cmd' -or $Executable -like '*.bat') {
        $psi.FileName = $env:ComSpec
        $psi.Arguments = '/c ' + (ConvertTo-ProcessArgumentString -Arguments (@($Executable) + $Arguments))
    }
    else {
        $psi.FileName = $Executable
        $psi.Arguments = ConvertTo-ProcessArgumentString -Arguments $Arguments
    }

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    $null = $process.Start()
    $completed = $process.WaitForExit([Math]::Max(1, $TimeoutSec) * 1000)
    $cancelled = $false
    if (-not $completed) {
        Stop-ProcessTree -ProcessId $process.Id
        $process.WaitForExit(5000)
    }

    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $apiKey = [Environment]::GetEnvironmentVariable('CURSOR_API_KEY', 'Process')
    if (-not [string]::IsNullOrEmpty($apiKey)) {
        $stdout = $stdout.Replace($apiKey, '[REDACTED]')
        $stderr = $stderr.Replace($apiKey, '[REDACTED]')
    }
    if ($StdoutPath) {
        Set-Content -LiteralPath $StdoutPath -Value $stdout -Encoding UTF8
    }
    if ($StderrPath) {
        Set-Content -LiteralPath $StderrPath -Value $stderr -Encoding UTF8
    }

    return [pscustomobject]@{
        ExitCode  = if ($process.HasExited) { $process.ExitCode } else { -1 }
        Stdout    = $stdout
        Stderr    = $stderr
        TimedOut  = -not $completed
        Cancelled = $cancelled
    }
}

function Stop-ProcessTree {
    param([int]$ProcessId)
    if ($ProcessId -le 0) {
        return
    }
    $null = Start-Process -FilePath 'taskkill.exe' -ArgumentList @('/T', '/F', '/PID', $ProcessId) -Wait -NoNewWindow -ErrorAction SilentlyContinue
}

function Get-AgentVersion {
    param([string]$Executable, [int]$TimeoutSec)

    $result = Invoke-AgentRaw -Executable $Executable -Arguments @('--version') -TimeoutSec $TimeoutSec
    $text = ($result.Stdout + $result.Stderr).Trim()
    if ([string]::IsNullOrWhiteSpace($text)) {
        return 'unknown'
    }
    return ($text -split "`n")[0].Trim()
}

function Get-AgentAuthFromStatus {
    param(
        [string]$Executable,
        [int]$TimeoutSec,
        [string]$StdoutPath,
        [string]$StderrPath
    )

    $result = Invoke-AgentRaw -Executable $Executable -Arguments @('status') -TimeoutSec $TimeoutSec -StdoutPath $StdoutPath -StderrPath $StderrPath
    $combined = ($result.Stdout + $result.Stderr)
    $authenticated = $false
    if ($result.ExitCode -eq 0 -and $combined -notmatch '(?i)not logged in') {
        $authenticated = $true
    }
    return [pscustomobject]@{
        Authenticated = $authenticated
        ExitCode      = $result.ExitCode
        Output        = $combined.Trim()
        TimedOut      = $result.TimedOut
        Cancelled     = $result.Cancelled
    }
}

function Get-StreamJsonFinalResult {
    param([string]$Stdout)

    $final = $null
    foreach ($line in ($Stdout -split "`n")) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }
        try {
            $obj = $trimmed | ConvertFrom-Json
        }
        catch {
            continue
        }
        if ($obj.PSObject.Properties.Name -contains 'type' -and $obj.type -eq 'result') {
            if ($obj.PSObject.Properties.Name -contains 'result' -and -not [string]::IsNullOrWhiteSpace([string]$obj.result)) {
                $final = [string]$obj.result
            }
        }
        if ($obj.PSObject.Properties.Name -contains 'type' -and $obj.type -eq 'assistant') {
            if ($obj.message -and $obj.message.content) {
                foreach ($part in $obj.message.content) {
                    if ($part.text) {
                        $final = [string]$part.text
                    }
                }
            }
        }
    }
    return $final
}

function Get-ObservedModelFromStream {
    param([string]$Stdout)

    foreach ($line in ($Stdout -split "`n")) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }
        try {
            $obj = $trimmed | ConvertFrom-Json
        }
        catch {
            continue
        }
        if ($obj.PSObject.Properties.Name -contains 'model') {
            return [string]$obj.model
        }
    }
    return 'unknown'
}

function New-RunContext {
    param([string]$WorkspacePath)

    $runId = '{0:yyyyMMddTHHmmssZ}-{1}' -f ([DateTime]::UtcNow), ([Guid]::NewGuid().ToString('N').Substring(0, 8))
    $runRoot = Join-Path (Get-RepoRoot) '.harness/runs/cursor-agent'
    $runDir = Join-Path $runRoot $runId
    New-Item -ItemType Directory -Path $runDir -Force | Out-Null
    return [pscustomobject]@{
        RunId        = $runId
        RunDirectory = $runDir
        Workspace    = (Resolve-Path -LiteralPath $WorkspacePath).Path
        StartedUtc   = [DateTime]::UtcNow.ToString('o')
    }
}

function Write-RunRecord {
    param(
        [hashtable]$Record,
        [string]$RunDirectory
    )

    $path = Join-Path $RunDirectory 'run.json'
    $Record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding UTF8
    return $path
}

function Build-WorkCliArgs {
    param(
        [string]$WorkspacePath,
        [string]$WorkMode,
        [string]$PromptText,
        [bool]$UseStreamJson
    )

    $outputFormat = if ($UseStreamJson) { 'stream-json' } else { 'text' }
    if ($WorkMode -eq 'composer') {
        return @('-p', '--workspace', $WorkspacePath, '--model', 'composer-2.5', '--output-format', $outputFormat, '--', $PromptText)
    }
    return @('-p', '--workspace', $WorkspacePath, '--mode', 'ask', '--model', 'auto', '--output-format', $outputFormat, '--', $PromptText)
}

function Get-SanitizedDiagnostics {
    param(
        [string]$Executable,
        [string]$WorkspacePath,
        [int]$TimeoutSec
    )

    $apiKeyPresent = Test-ApiKeyEnvPresent
    $diag = [ordered]@{
        agentPath     = $Executable
        agentVersion  = 'unknown'
        windowsUser   = [Environment]::UserName
        userProfile   = [Environment]::GetEnvironmentVariable('USERPROFILE')
        home          = [Environment]::GetEnvironmentVariable('HOME')
        apiKeyPresent = $apiKeyPresent
        authStatus    = 'unknown'
        exitCode      = 0
    }

    if ($apiKeyPresent) {
        $diag.authStatus = 'api-key-env-present'
        $diag.exitCode = 3
        return $diag
    }

    try {
        $diag.agentVersion = Get-AgentVersion -Executable $Executable -TimeoutSec $TimeoutSec
    }
    catch {
        $diag.authStatus = 'tool-missing'
        $diag.exitCode = 2
        return $diag
    }

    $status = Get-AgentAuthFromStatus -Executable $Executable -TimeoutSec $TimeoutSec
    if ($status.TimedOut) {
        $diag.authStatus = 'failed'
        $diag.exitCode = 1
        return $diag
    }
    if ($status.Cancelled) {
        $diag.authStatus = 'cancelled'
        $diag.exitCode = 130
        return $diag
    }
    if ($status.Authenticated) {
        $diag.authStatus = 'authenticated'
        $diag.exitCode = 0
    }
    else {
        $diag.authStatus = 'unauthenticated'
        $diag.exitCode = 1
    }
    return $diag
}

function Assert-NoApiKeyForAccountRoute {
    param([switch]$AllowApiKey)
    if ((Test-ApiKeyEnvPresent) -and -not $AllowApiKey) {
        throw 'api-key-env-present'
    }
}

function Ensure-Authenticated {
    param(
        [string]$Executable,
        [int]$TimeoutSec,
        [string]$StdoutPath,
        [string]$StderrPath,
        [switch]$LoginIfUnauthenticated
    )

    $status = Get-AgentAuthFromStatus -Executable $Executable -TimeoutSec $TimeoutSec -StdoutPath $StdoutPath -StderrPath $StderrPath
    if ($status.TimedOut) {
        throw 'timed-out'
    }
    if ($status.Cancelled) {
        throw 'cancelled'
    }
    if (-not $status.Authenticated) {
        if (-not $LoginIfUnauthenticated) {
            throw 'unauthenticated'
        }

        Write-Output 'Cursor agent CLI is unauthenticated; starting interactive login in this terminal.'
        & $Executable login
        $loginExitCode = $LASTEXITCODE
        if ($loginExitCode -ne 0) {
            throw 'unauthenticated'
        }

        $status = Get-AgentAuthFromStatus -Executable $Executable -TimeoutSec $TimeoutSec -StdoutPath $StdoutPath -StderrPath $StderrPath
        if ($status.TimedOut) {
            throw 'timed-out'
        }
        if ($status.Cancelled) {
            throw 'cancelled'
        }
        if (-not $status.Authenticated) {
            throw 'unauthenticated'
        }
    }
}

function Invoke-ReadOnlyProbe {
    param(
        [string]$Executable,
        [string]$WorkspacePath,
        [int]$TimeoutSec,
        [string]$RunDirectory
    )

    New-Item -ItemType Directory -Path $RunDirectory -Force | Out-Null
    $stdoutLog = Join-Path $RunDirectory 'stdout.log'
    $stderrLog = Join-Path $RunDirectory 'stderr.log'
    $agentCliArgs = Build-WorkCliArgs -WorkspacePath $WorkspacePath -WorkMode 'auto' -PromptText $script:ProbePrompt -UseStreamJson $true
    $result = Invoke-AgentRaw -Executable $Executable -Arguments $agentCliArgs -TimeoutSec $TimeoutSec -StdoutPath $stdoutLog -StderrPath $stderrLog
    $final = Get-StreamJsonFinalResult -Stdout $result.Stdout
    $ok = (-not $result.TimedOut) -and (-not $result.Cancelled) -and ($result.ExitCode -eq 0) -and (-not [string]::IsNullOrWhiteSpace($final))
    return [pscustomobject]@{
        Success       = $ok
        FinalResponse = $final
        ExitCode      = $result.ExitCode
        TimedOut      = $result.TimedOut
        Cancelled     = $result.Cancelled
        ObservedModel = Get-ObservedModelFromStream -Stdout $result.Stdout
        StdoutLog     = $stdoutLog
        StderrLog     = $stderrLog
    }
}

function Invoke-WorkOperation {
    param(
        [string]$Executable,
        [string]$WorkspacePath,
        [string]$WorkMode,
        [string]$PromptText,
        [bool]$UseStreamJson,
        [int]$TimeoutSec,
        [string]$RunDirectory
    )

    New-Item -ItemType Directory -Path $RunDirectory -Force | Out-Null
    $stdoutLog = Join-Path $RunDirectory 'stdout.log'
    $stderrLog = Join-Path $RunDirectory 'stderr.log'
    $agentCliArgs = Build-WorkCliArgs -WorkspacePath $WorkspacePath -WorkMode $WorkMode -PromptText $PromptText -UseStreamJson $UseStreamJson
    $requestedModel = if ($WorkMode -eq 'composer') { 'composer-2.5' } else { 'auto' }
    $result = Invoke-AgentRaw -Executable $Executable -Arguments $agentCliArgs -TimeoutSec $TimeoutSec -StdoutPath $stdoutLog -StderrPath $stderrLog
    $final = if ($UseStreamJson) { Get-StreamJsonFinalResult -Stdout $result.Stdout } else { $result.Stdout.Trim() }
    $classification = 'failed'
    if ($result.Cancelled) {
        $classification = 'cancelled'
    }
    elseif ($result.TimedOut) {
        $classification = 'timed-out'
    }
    elseif ($result.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($final)) {
        $classification = 'completed'
    }
    return [pscustomobject]@{
        Classification = $classification
        FinalResponse  = $final
        ExitCode       = $result.ExitCode
        RequestedModel = $requestedModel
        ObservedModel  = if ($UseStreamJson) { Get-ObservedModelFromStream -Stdout $result.Stdout } else { 'unknown' }
        StdoutLog      = $stdoutLog
        StderrLog      = $stderrLog
        RawStdout      = $result.Stdout
    }
}

function Test-SingleExclusiveOperation {
    param([string[]]$Selected)

    $count = ($Selected | Where-Object { $_ }).Count
    if ($count -gt 1) {
        throw 'Choose only one operation: -AuthLogin, -AuthStatus, -Diagnose, -Probe, or -Prompt work.'
    }
}

function Get-BridgeDirectories {
    param([string]$CustomDirectory)
    $root = if ([string]::IsNullOrWhiteSpace($CustomDirectory)) {
        Join-Path (Get-RepoRoot) '.harness/runtime/cursor-agent-bridge'
    }
    else {
        [IO.Path]::GetFullPath($CustomDirectory)
    }
    return [pscustomobject]@{
        Root      = $root
        Inbox     = Join-Path $root 'inbox'
        Working   = Join-Path $root 'working'
        Outbox    = Join-Path $root 'outbox'
        Cancelled = Join-Path $root 'cancelled'
        Lock      = Join-Path $root 'bridge.lock'
        SubmitLock = Join-Path $root 'submit.lock'
    }
}

function Invoke-LocalBridgeRequest {
    param(
        [string]$WorkPrompt,
        [string]$WorkMode,
        [string]$WorkWorkspace,
        [string]$CustomBridgeDirectory,
        [int]$TimeoutSec
    )
    if (-not (Test-Path -LiteralPath $WorkWorkspace -PathType Container)) {
        throw 'Workspace does not exist.'
    }
    $workspacePath = (Resolve-Path -LiteralPath $WorkWorkspace).Path
    $dirs = Get-BridgeDirectories -CustomDirectory $CustomBridgeDirectory
    New-Item -ItemType Directory -Path $dirs.Inbox, $dirs.Working, $dirs.Outbox, $dirs.Cancelled -Force | Out-Null

    $workerRunning = $false
    try {
        $lockProbe = [IO.File]::Open($dirs.Lock, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $lockProbe.Dispose()
    }
    catch [IO.IOException] {
        $workerRunning = $true
    }
    if (-not $workerRunning) {
        [Console]::Out.WriteLine(([pscustomobject]@{ classification = 'bridge-unavailable'; exitCode = 5 } | ConvertTo-Json -Compress))
        return 5
    }

    $submitLock = $null
    try {
        $submitLock = [IO.File]::Open($dirs.SubmitLock, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    }
    catch [IO.IOException] {
        [Console]::Out.WriteLine(([pscustomobject]@{ classification = 'bridge-busy'; exitCode = 6 } | ConvertTo-Json -Compress))
        return 6
    }

    try {
        $pending = @()
        $pending += @(Get-ChildItem -LiteralPath $dirs.Inbox -Filter '*.json' -File -ErrorAction SilentlyContinue)
        $pending += @(Get-ChildItem -LiteralPath $dirs.Working -Filter '*.json' -File -ErrorAction SilentlyContinue)
        if ($pending.Count -gt 0) {
            [Console]::Out.WriteLine(([pscustomobject]@{ classification = 'bridge-busy'; exitCode = 6 } | ConvertTo-Json -Compress))
            return 6
        }

        $requestId = [Guid]::NewGuid().ToString()
        $requestPath = Join-Path $dirs.Inbox ($requestId + '.json')
        $tempPath = $requestPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
        $request = [ordered]@{
            requestId      = $requestId
            createdUtc     = [DateTime]::UtcNow.ToString('o')
            mode           = $WorkMode
            workspace      = $workspacePath
            timeoutSeconds = $TimeoutSec
            prompt         = $WorkPrompt
        }
        $request | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $tempPath -Encoding UTF8
        Move-Item -LiteralPath $tempPath -Destination $requestPath

        $requestPublished = $true
        $requestCompleted = $false
        try {
            $responsePath = Join-Path $dirs.Outbox ($requestId + '.json')
            $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSec + 60)
            while ([DateTime]::UtcNow -lt $deadline) {
                if (Test-Path -LiteralPath $responsePath -PathType Leaf) {
                    try {
                        $response = Get-Content -LiteralPath $responsePath -Raw | ConvertFrom-Json
                        if ($response.requestId -ne $requestId) {
                            throw 'Bridge response identifier mismatch.'
                        }
                    }
                    finally {
                        Remove-Item -LiteralPath $responsePath -Force -ErrorAction SilentlyContinue
                    }
                    $requestCompleted = $true
                    if ($response.stdout) { [Console]::Out.WriteLine(([string]$response.stdout).TrimEnd()) }
                    elseif ($response.classification -ne 'completed') {
                        $summary = [pscustomobject]@{
                            classification = [string]$response.classification
                            exitCode       = [int]$response.exitCode
                            runId          = [string]$response.runId
                            runRecord      = [string]$response.runRecord
                        }
                        [Console]::Out.WriteLine(($summary | ConvertTo-Json -Compress))
                    }
                    if ($response.stderr) { [Console]::Error.WriteLine(([string]$response.stderr).TrimEnd()) }
                    return [int]$response.exitCode
                }
                Start-Sleep -Milliseconds 300
            }

            $cancelPath = Join-Path $dirs.Cancelled ($requestId + '.cancel')
            Set-Content -LiteralPath $cancelPath -Value 'client-timeout' -Encoding ASCII
            $requestCompleted = $true
            [Console]::Out.WriteLine(([pscustomobject]@{ classification = 'timed-out'; requestId = $requestId; exitCode = 124 } | ConvertTo-Json -Compress))
            return 124
        }
        finally {
            if ($requestPublished -and -not $requestCompleted) {
                $stillQueued = (Test-Path -LiteralPath $requestPath) -or (Test-Path -LiteralPath (Join-Path $dirs.Working ($requestId + '.json')))
                if ($stillQueued) {
                    Set-Content -LiteralPath (Join-Path $dirs.Cancelled ($requestId + '.cancel')) -Value 'client-interrupted' -Encoding ASCII
                }
            }
        }
    }
    finally {
        if ($submitLock) { $submitLock.Dispose() }
    }
}

if ($TimeoutSeconds -le 0) {
    throw '-TimeoutSeconds must be a positive integer.'
}

$exclusive = @(
    if ($AuthLogin) { 'AuthLogin' }
    if ($AuthStatus) { 'AuthStatus' }
    if ($Diagnose) { 'Diagnose' }
    if ($Probe) { 'Probe' }
    if ($Prompt) { 'Prompt' }
)
Test-SingleExclusiveOperation -Selected $exclusive

if ($ViaLocalBridge) {
    if (-not $Prompt) { throw '-Prompt is required with -ViaLocalBridge.' }
    if ($AuthLogin -or $AuthStatus -or $Diagnose -or $Probe -or $DryRun -or $LoginIfUnauthenticated -or $BridgeWorkerResult -or $BridgeAllowApiKey) {
        throw '-ViaLocalBridge supports work requests only; use the local bridge worker for Cursor authentication.'
    }
    if ($AgentPath) { throw 'Set -AgentPath on the local bridge worker, not on the Codex request.' }
    if ($Json) { throw '-Json is not needed with -ViaLocalBridge; the bridge returns the final response.' }
    $bridgeExitCode = Invoke-LocalBridgeRequest -WorkPrompt $Prompt -WorkMode $Mode -WorkWorkspace $Workspace -CustomBridgeDirectory $BridgeDirectory -TimeoutSec $TimeoutSeconds
    exit $bridgeExitCode
}

if ($BridgeAllowApiKey -and -not $BridgeWorkerResult) {
    throw '-BridgeAllowApiKey is reserved for an explicitly configured local bridge worker.'
}

$resolvedAgent = $null
try {
    $resolvedAgent = Resolve-AgentExecutable -ExplicitPath $AgentPath
}
catch {
    if ($Diagnose) {
        $diag = [ordered]@{
            agentPath     = $AgentPath
            agentVersion  = 'unknown'
            windowsUser   = [Environment]::UserName
            userProfile   = [Environment]::GetEnvironmentVariable('USERPROFILE')
            home          = [Environment]::GetEnvironmentVariable('HOME')
            apiKeyPresent = (Test-ApiKeyEnvPresent)
            authStatus    = 'tool-missing'
            exitCode      = 2
        }
        $diag | ConvertTo-Json -Compress | Write-Output
        exit 2
    }
    if ($AuthStatus) {
        Write-Output 'Cursor agent CLI: tool-missing'
        exit 2
    }
    throw 'Cursor agent CLI was not found on PATH. Install Cursor CLI or use the interactive Task surface.'
}

if ($AuthLogin -and $AuthStatus) {
    throw 'Choose only one of -AuthLogin or -AuthStatus.'
}

if ($AuthLogin) {
    if (Test-ApiKeyEnvPresent) {
        Write-Output 'CURSOR_API_KEY is set; account login route is blocked. Unset CURSOR_API_KEY first.'
        exit 3
    }
    if ($DryRun) {
        [pscustomobject]@{ executable = $resolvedAgent; command = 'login' } | ConvertTo-Json -Compress
        exit 0
    }
    & $resolvedAgent login
    exit $LASTEXITCODE
}

if ($Diagnose) {
    $diag = Get-SanitizedDiagnostics -Executable $resolvedAgent -WorkspacePath $Workspace -TimeoutSec $TimeoutSeconds
    $diag | ConvertTo-Json -Compress | Write-Output
    exit [int]$diag.exitCode
}

if ($AuthStatus) {
    if (Test-ApiKeyEnvPresent) {
        Write-Output 'Cursor agent CLI: api-key-env-present (account route blocked)'
        exit 3
    }
    if ($DryRun) {
        [pscustomobject]@{ executable = $resolvedAgent; command = 'status' } | ConvertTo-Json -Compress
        exit 0
    }
    $status = Get-AgentAuthFromStatus -Executable $resolvedAgent -TimeoutSec $TimeoutSeconds
    if ($status.Authenticated) {
        Write-Output 'Cursor agent CLI: authenticated'
        exit 0
    }
    Write-Output 'Cursor agent CLI: unauthenticated'
    exit 1
}

if ($Probe) {
    $run = New-RunContext -WorkspacePath $Workspace
    $endedUtc = $null
    $classification = 'failed'
    $probeResult = $null
    $started = [DateTime]::UtcNow
    try {
        Assert-NoApiKeyForAccountRoute
        Ensure-Authenticated -Executable $resolvedAgent -TimeoutSec $TimeoutSeconds -LoginIfUnauthenticated:$LoginIfUnauthenticated
        $probeResult = Invoke-ReadOnlyProbe -Executable $resolvedAgent -WorkspacePath $run.Workspace -TimeoutSec $TimeoutSeconds -RunDirectory $run.RunDirectory
        if ($probeResult.Cancelled) {
            $classification = 'cancelled'
        }
        elseif ($probeResult.TimedOut) {
            $classification = 'timed-out'
        }
        elseif ($probeResult.Success) {
            $classification = 'completed'
        }
    }
    catch {
        $classification = $_.Exception.Message
        if ($classification -notin @('unauthenticated', 'api-key-env-present', 'timed-out', 'cancelled')) {
            $classification = 'failed'
        }
    }
    $endedUtc = [DateTime]::UtcNow
    $durationMs = [int](($endedUtc - $started).TotalMilliseconds)
    $record = @{
        runId              = $run.RunId
        operation          = 'probe'
        startedUtc         = $run.StartedUtc
        endedUtc           = $endedUtc.ToString('o')
        durationMs         = $durationMs
        exitCode           = if ($probeResult) { $probeResult.ExitCode } else { 1 }
        agentPath          = $resolvedAgent
        workspace          = $run.Workspace
        authPrecheck       = if ($classification -eq 'unauthenticated') { 'unauthenticated' } elseif ($classification -eq 'api-key-env-present') { 'api-key-env-present' } else { 'authenticated' }
        requestedTask      = 'probe'
        requestedModel     = 'auto'
        observedModel      = if ($probeResult) { $probeResult.ObservedModel } else { 'unknown' }
        classification     = $classification
        lastResponse       = if ($probeResult) { $probeResult.FinalResponse } else { $null }
        stdoutLog          = if ($probeResult) { $probeResult.StdoutLog } else { (Join-Path $run.RunDirectory 'stdout.log') }
        stderrLog          = if ($probeResult) { $probeResult.StderrLog } else { (Join-Path $run.RunDirectory 'stderr.log') }
    }
    $recordPath = Write-RunRecord -Record $record -RunDirectory $run.RunDirectory
    $payload = [pscustomobject]@{
        classification = $classification
        workspace      = $run.Workspace
        finalResponse  = $record.lastResponse
        runId          = $run.RunId
        runRecord      = $recordPath
    }
    $payload | ConvertTo-Json -Compress | Write-Output
    if ($classification -eq 'completed') {
        exit 0
    }
    if ($classification -eq 'unauthenticated') {
        exit 1
    }
    if ($classification -eq 'api-key-env-present') {
        exit 3
    }
    if ($classification -eq 'timed-out') {
        exit 124
    }
    if ($classification -eq 'cancelled') {
        exit 130
    }
    exit 1
}

if (-not $Prompt) {
    throw '-Prompt is required unless -AuthLogin, -AuthStatus, -Diagnose, or -Probe is used.'
}

$outputFormatStream = $Json
$cliArgs = Build-WorkCliArgs -WorkspacePath $Workspace -WorkMode $Mode -PromptText $Prompt -UseStreamJson $outputFormatStream

if ($DryRun) {
    [pscustomobject]@{
        executable = $resolvedAgent
        workspace  = (Resolve-Path -LiteralPath $Workspace).Path
        mode       = $Mode
        args       = $cliArgs
    } | ConvertTo-Json -Compress
    exit 0
}

$run = New-RunContext -WorkspacePath $Workspace
$started = [DateTime]::UtcNow
$workResult = $null
$classification = 'failed'
try {
    Assert-NoApiKeyForAccountRoute -AllowApiKey:$BridgeAllowApiKey
    if ($BridgeAllowApiKey) {
        if (-not (Test-ApiKeyEnvPresent)) { throw 'api-key-env-missing' }
    }
    else {
        Ensure-Authenticated -Executable $resolvedAgent -TimeoutSec $TimeoutSeconds -StdoutPath (Join-Path $run.RunDirectory 'auth-stdout.log') -StderrPath (Join-Path $run.RunDirectory 'auth-stderr.log') -LoginIfUnauthenticated:$LoginIfUnauthenticated
    }
    $probeGate = Invoke-ReadOnlyProbe -Executable $resolvedAgent -WorkspacePath $run.Workspace -TimeoutSec $TimeoutSeconds -RunDirectory (Join-Path $run.RunDirectory 'probe-gate')
    if (-not $probeGate.Success) {
        if ($probeGate.Cancelled) {
            throw 'cancelled'
        }
        if ($probeGate.TimedOut) {
            throw 'timed-out'
        }
        throw 'probe-failed'
    }
    $workResult = Invoke-WorkOperation -Executable $resolvedAgent -WorkspacePath $run.Workspace -WorkMode $Mode -PromptText $Prompt -UseStreamJson $outputFormatStream -TimeoutSec $TimeoutSeconds -RunDirectory $run.RunDirectory
    $classification = $workResult.Classification
}
catch {
    $classification = $_.Exception.Message
    if ($classification -eq 'api-key-env-present') {
        $classification = 'api-key-env-present'
    }
    elseif ($classification -eq 'unauthenticated') {
        $classification = 'unauthenticated'
    }
    elseif ($classification -in @('timed-out', 'cancelled', 'probe-failed')) {
        # keep
    }
    else {
        $classification = 'failed'
    }
}

$endedUtc = [DateTime]::UtcNow
$durationMs = [int](($endedUtc - $started).TotalMilliseconds)
$record = @{
    runId          = $run.RunId
    operation      = 'work'
    startedUtc     = $run.StartedUtc
    endedUtc       = $endedUtc.ToString('o')
    durationMs     = $durationMs
    exitCode       = if ($workResult) { $workResult.ExitCode } else { 1 }
    agentPath      = $resolvedAgent
    workspace      = $run.Workspace
    authPrecheck   = if ($BridgeAllowApiKey -and (Test-ApiKeyEnvPresent)) { 'api-key-env-present' } elseif ($classification -eq 'unauthenticated') { 'unauthenticated' } elseif ($classification -eq 'api-key-env-present') { 'api-key-env-present' } elseif ($classification -eq 'probe-failed') { 'probe-failed' } else { 'authenticated' }
    requestedTask  = $Mode
    requestedModel = if ($workResult) { $workResult.RequestedModel } elseif ($Mode -eq 'composer') { 'composer-2.5' } else { 'auto' }
    observedModel  = if ($workResult) { $workResult.ObservedModel } else { 'unknown' }
    classification = $classification
    lastResponse   = if ($workResult) { $workResult.FinalResponse } else { $null }
    stdoutLog      = if ($workResult) { $workResult.StdoutLog } else { (Join-Path $run.RunDirectory 'stdout.log') }
    stderrLog      = if ($workResult) { $workResult.StderrLog } else { (Join-Path $run.RunDirectory 'stderr.log') }
}
Write-RunRecord -Record $record -RunDirectory $run.RunDirectory | Out-Null

if (-not $BridgeWorkerResult -and $workResult -and $workResult.RawStdout) {
    Write-Output $workResult.RawStdout.TrimEnd()
}

if ($BridgeWorkerResult) {
    $workerExitCode = switch ($classification) {
        'completed' { 0 }
        'api-key-env-present' { 3 }
        'timed-out' { 124 }
        'cancelled' { 130 }
        default { 1 }
    }
    $workerPayload = [pscustomobject]@{
        classification = $classification
        exitCode       = [int]$workerExitCode
        finalResponse  = $record.lastResponse
        runId          = $record.runId
        runRecord      = (Join-Path $run.RunDirectory 'run.json')
    }
    $workerPayload | ConvertTo-Json -Compress | Write-Output
}

if ($classification -eq 'completed') {
    exit 0
}
if ($classification -eq 'unauthenticated') {
    exit 1
}
if ($classification -eq 'api-key-env-present') {
    exit 3
}
if ($classification -eq 'timed-out') {
    exit 124
}
if ($classification -eq 'cancelled') {
    exit 130
}
if ($classification -eq 'probe-failed') {
    exit 1
}
exit 1

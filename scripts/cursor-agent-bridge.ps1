<#
.SYNOPSIS
    Runs Cursor wrapper requests under this Windows user's authenticated profile.

.DESCRIPTION
    Keep this script running in the visible PowerShell session that owns the
    Cursor CLI login. Codex submits work through run-cursor-subagent.ps1 -ViaLocalBridge.
    The bridge passes prompts and results through a private, ignored local queue;
    credentials never enter that queue.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$Workspace,
    [string]$BridgeDirectory,
    [string]$AgentPath,
    [switch]$UseCursorApiKeyEnvironmentVariable,
    [ValidateRange(50, 5000)] [int]$PollIntervalMilliseconds = 250,
    [switch]$Once
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$wrapperPath = Join-Path $PSScriptRoot 'run-cursor-subagent.ps1'
$allowedWorkspace = (Resolve-Path -LiteralPath $Workspace).Path.TrimEnd('\')
$script:CursorApiKey = $null
if ($UseCursorApiKeyEnvironmentVariable) {
    $script:CursorApiKey = [Environment]::GetEnvironmentVariable('cursor', 'Process')
    if ([string]::IsNullOrWhiteSpace($script:CursorApiKey)) {
        throw 'The cursor environment variable is empty or unavailable in this PowerShell process.'
    }
}

function Test-PathWithinRoot {
    param([string]$Path, [string]$Root)
    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    return $fullPath.Equals($fullRoot, [StringComparison]::OrdinalIgnoreCase) -or
        $fullPath.StartsWith($fullRoot + '\', [StringComparison]::OrdinalIgnoreCase)
}

if ([string]::IsNullOrWhiteSpace($BridgeDirectory)) {
    $BridgeDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) '.harness/runtime/cursor-agent-bridge'
}
$BridgeDirectory = [IO.Path]::GetFullPath($BridgeDirectory)
$inbox = Join-Path $BridgeDirectory 'inbox'
$working = Join-Path $BridgeDirectory 'working'
$outbox = Join-Path $BridgeDirectory 'outbox'
$cancelled = Join-Path $BridgeDirectory 'cancelled'
$lockPath = Join-Path $BridgeDirectory 'bridge.lock'
New-Item -ItemType Directory -Path $inbox, $working, $outbox, $cancelled -Force | Out-Null

function ConvertTo-PowerShellLiteral {
    param([AllowEmptyString()] [string]$Value)
    if ($Value.IndexOf([char]0) -ge 0) { throw 'NUL characters are not supported in bridge requests.' }
    return "'" + $Value.Replace("'", "''") + "'"
}

function Stop-ProcessTree {
    param([int]$ProcessId)
    if ($ProcessId -gt 0) {
        $null = Start-Process -FilePath 'taskkill.exe' -ArgumentList @('/T', '/F', '/PID', $ProcessId) -Wait -NoNewWindow -ErrorAction SilentlyContinue
    }
}

function Write-BridgeResponse {
    param(
        [string]$RequestId,
        [string]$Classification,
        [int]$ExitCode,
        [string]$Stdout = '',
        [string]$Stderr = '',
        [string]$RunId = '',
        [string]$RunRecord = ''
    )
    $target = Join-Path $outbox ($RequestId + '.json')
    $temp = $target + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    $payload = [ordered]@{
        requestId      = $RequestId
        classification = $Classification
        exitCode       = $ExitCode
        completedUtc   = [DateTime]::UtcNow.ToString('o')
        stdout         = $Stdout
        stderr         = $Stderr
        runId          = $RunId
        runRecord      = $RunRecord
    }
    $payload | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $temp -Encoding UTF8
    Move-Item -LiteralPath $temp -Destination $target -Force
}

function Invoke-WrapperRequest {
    param([pscustomobject]$Request)

    if ($Request.mode -notin @('auto', 'composer')) {
        return [pscustomobject]@{ classification = 'invalid-request'; exitCode = 2; stdout = ''; stderr = 'Unsupported mode.' }
    }
    if (-not $Request.workspace -or -not (Test-Path -LiteralPath $Request.workspace -PathType Container)) {
        return [pscustomobject]@{ classification = 'invalid-request'; exitCode = 2; stdout = ''; stderr = 'Workspace does not exist.' }
    }
    $requestWorkspace = (Resolve-Path -LiteralPath $Request.workspace).Path
    if (-not (Test-PathWithinRoot -Path $requestWorkspace -Root $allowedWorkspace)) {
        return [pscustomobject]@{ classification = 'workspace-rejected'; exitCode = 2; stdout = ''; stderr = 'Workspace is outside the bridge allowlist.' }
    }
    if ([string]::IsNullOrWhiteSpace([string]$Request.prompt) -or $Request.prompt.IndexOf([char]0) -ge 0) {
        return [pscustomobject]@{ classification = 'invalid-request'; exitCode = 2; stdout = ''; stderr = 'Prompt is empty or contains a NUL character.' }
    }
    $timeoutSeconds = [int]$Request.timeoutSeconds
    if ($timeoutSeconds -le 0 -or $timeoutSeconds -gt 7200) {
        return [pscustomobject]@{ classification = 'invalid-request'; exitCode = 2; stdout = ''; stderr = 'Timeout is outside the allowed range.' }
    }

    $scriptText = '& ' + (ConvertTo-PowerShellLiteral $wrapperPath) +
        ' -Mode ' + (ConvertTo-PowerShellLiteral ([string]$Request.mode)) +
        ' -Workspace ' + (ConvertTo-PowerShellLiteral $requestWorkspace) +
        ' -Prompt ' + (ConvertTo-PowerShellLiteral ([string]$Request.prompt)) +
        ' -Json -BridgeWorkerResult -TimeoutSeconds ' + $timeoutSeconds
    if ($UseCursorApiKeyEnvironmentVariable) {
        $scriptText += ' -BridgeAllowApiKey'
    }
    if (-not [string]::IsNullOrWhiteSpace($AgentPath)) {
        $scriptText += ' -AgentPath ' + (ConvertTo-PowerShellLiteral $AgentPath)
    }
    $encodedCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($scriptText))
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = Join-Path $PSHOME 'powershell.exe'
    $psi.Arguments = '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand ' + $encodedCommand
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    if ($UseCursorApiKeyEnvironmentVariable) {
        $psi.EnvironmentVariables['CURSOR_API_KEY'] = $script:CursorApiKey
    }
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    $processStarted = $false
    try {
        $null = $process.Start()
        $processStarted = $true
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $deadline = [DateTime]::UtcNow.AddSeconds($timeoutSeconds + 60)
        $termination = $null
        while (-not $process.WaitForExit(250)) {
            if (Test-Path -LiteralPath (Join-Path $cancelled ($Request.requestId + '.cancel'))) {
                $termination = 'cancelled'
                break
            }
            if ([DateTime]::UtcNow -ge $deadline) {
                $termination = 'timed-out'
                break
            }
        }
        if ($termination) {
            Stop-ProcessTree -ProcessId $process.Id
            $process.WaitForExit(5000) | Out-Null
            return [pscustomobject]@{ classification = $termination; exitCode = if ($termination -eq 'cancelled') { 130 } else { 124 }; stdout = ''; stderr = '' }
        }
        $process.WaitForExit()
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        try { $workerPayload = $stdout.Trim() | ConvertFrom-Json } catch { $workerPayload = $null }
        if ($workerPayload -and $workerPayload.PSObject.Properties.Name -contains 'classification') {
            return [pscustomobject]@{
                classification = [string]$workerPayload.classification
                exitCode       = [int]$workerPayload.exitCode
                stdout         = [string]$workerPayload.finalResponse
                stderr         = $stderr
                runId          = [string]$workerPayload.runId
                runRecord      = [string]$workerPayload.runRecord
            }
        }
        $classification = switch ($process.ExitCode) {
            0 { 'completed' }
            3 { 'api-key-env-present' }
            124 { 'timed-out' }
            130 { 'cancelled' }
            default {
                if ($stdout -match '(?i)unauthenticated') { 'unauthenticated' } else { 'failed' }
            }
        }
        return [pscustomobject]@{
            classification = $classification
            exitCode       = $process.ExitCode
            stdout         = $stdout
            stderr         = $stderr
        }
    }
    catch {
        if ($processStarted -and -not $process.HasExited) {
            Stop-ProcessTree -ProcessId $process.Id
        }
        throw
    }
    finally {
        $process.Dispose()
    }
}

function Publish-FailureForInterruptedRequests {
    foreach ($directory in @($working, $inbox)) {
        foreach ($file in @(Get-ChildItem -LiteralPath $directory -Filter '*.json' -File -ErrorAction SilentlyContinue)) {
            $requestId = [IO.Path]::GetFileNameWithoutExtension($file.Name)
            if ($requestId -match '^[0-9a-fA-F-]{36}$') {
                Write-BridgeResponse -RequestId $requestId -Classification 'bridge-interrupted' -ExitCode 1 -Stderr 'The previous bridge worker stopped before completing this request.'
            }
            Remove-Item -LiteralPath $file.FullName -Force -ErrorAction SilentlyContinue
        }
    }
}

$lockStream = $null
try {
    try {
        $lockStream = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    }
    catch [IO.IOException] {
        throw 'A Cursor bridge worker is already running for this queue.'
    }
    $lockStream.SetLength(0)
    $lockBytes = [Text.Encoding]::ASCII.GetBytes([string]$PID)
    $lockStream.Write($lockBytes, 0, $lockBytes.Length)
    $lockStream.Flush()
    Publish-FailureForInterruptedRequests

    Write-Output ('Cursor bridge ready. Workspace: ' + $allowedWorkspace)
    $processedCount = 0
    while ($true) {
        $files = @(Get-ChildItem -LiteralPath $inbox -Filter '*.json' -File -ErrorAction SilentlyContinue | Sort-Object Name)
        if ($files.Count -eq 0) {
            Start-Sleep -Milliseconds $PollIntervalMilliseconds
            continue
        }

        foreach ($file in $files) {
            $requestId = [IO.Path]::GetFileNameWithoutExtension($file.Name)
            if ($requestId -notmatch '^[0-9a-fA-F-]{36}$') {
                Remove-Item -LiteralPath $file.FullName -Force -ErrorAction SilentlyContinue
                continue
            }
            $claimedPath = Join-Path $working ($requestId + '.json')
            try { Move-Item -LiteralPath $file.FullName -Destination $claimedPath -ErrorAction Stop }
            catch { continue }

            try {
                $request = Get-Content -LiteralPath $claimedPath -Raw | ConvertFrom-Json
                if ($request.requestId -ne $requestId) {
                    throw 'Request identifier mismatch.'
                }
                if (Test-Path -LiteralPath (Join-Path $cancelled ($requestId + '.cancel'))) {
                    Write-BridgeResponse -RequestId $requestId -Classification 'cancelled' -ExitCode 130
                }
                else {
                    $result = Invoke-WrapperRequest -Request $request
                    Write-BridgeResponse -RequestId $requestId -Classification $result.classification -ExitCode ([int]$result.exitCode) -Stdout ([string]$result.stdout) -Stderr ([string]$result.stderr) -RunId ([string]$result.runId) -RunRecord ([string]$result.runRecord)
                }
            }
            catch {
                Write-BridgeResponse -RequestId $requestId -Classification 'bridge-failed' -ExitCode 1 -Stderr 'The bridge could not process this request.'
            }
            finally {
                Remove-Item -LiteralPath $claimedPath -Force -ErrorAction SilentlyContinue
                Remove-Item -LiteralPath (Join-Path $cancelled ($requestId + '.cancel')) -Force -ErrorAction SilentlyContinue
            }
            $processedCount++
            if ($Once -and $processedCount -ge 1) { exit 0 }
        }
    }
}
finally {
    if ($lockStream) { $lockStream.Dispose() }
}

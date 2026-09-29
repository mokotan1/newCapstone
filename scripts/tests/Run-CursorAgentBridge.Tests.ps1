$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent (Split-Path -Parent $scriptRoot)
$wrapperPath = Join-Path $repoRoot 'scripts/run-cursor-subagent.ps1'
$bridgeWorkerPath = Join-Path $repoRoot 'scripts/cursor-agent-bridge.ps1'

function ConvertTo-PowerShellLiteral {
    param([string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

function New-BridgeFakeAgent {
    param([string]$Path)
    @'
$Rest = @($args)
if ($Rest.Count -eq 1 -and $Rest[0] -eq 'status') {
    if ($env:FAKE_AGENT_PROFILE -eq 'not_logged_in') { Write-Output 'Not logged in'; exit 1 }
    Write-Output 'Logged in'
    exit 0
}
if ($Rest -contains '--version') { Write-Output 'fake-agent bridge-test'; exit 0 }
if ($Rest -contains '-p') {
    $promptIndex = [Array]::IndexOf($Rest, '--')
    $prompt = if ($promptIndex -ge 0) { $Rest[$promptIndex + 1] } else { '' }
    if ($prompt -like '*CURSOR_WRAPPER_PROBE_OK*') {
        Write-Output '{"type":"result","subtype":"success","result":"CURSOR_WRAPPER_PROBE_OK","model":"auto"}'
        exit 0
    }
    if ($env:FAKE_AGENT_REQUIRE_API_KEY -eq '1' -and [string]::IsNullOrEmpty($env:CURSOR_API_KEY)) {
        Write-Output '{"type":"result","subtype":"error","result":"missing-api-key","model":"composer-2.5"}'
        exit 9
    }
    if ($env:FAKE_AGENT_ECHO_API_KEY -eq '1' -and -not [string]::IsNullOrEmpty($env:CURSOR_API_KEY)) {
        [Console]::Error.WriteLine($env:CURSOR_API_KEY)
    }
    Write-Output '{"type":"result","subtype":"success","result":"bridge-work-done","model":"composer-2.5"}'
    exit 0
}
Write-Output 'unsupported'
exit 1
'@ | Set-Content -LiteralPath $Path -Encoding UTF8
    return $Path
}

Describe 'Cursor local bridge' {
    $fixtureDir = Join-Path $TestDrive 'workspace with spaces'
    $childWorkspace = Join-Path $fixtureDir 'child-workspace'
    $bridgeDir = Join-Path $TestDrive 'bridge queue'
    $agentPath = Join-Path $TestDrive 'fake-agent.ps1'
    $workerOut = Join-Path $TestDrive 'worker.stdout.log'
    $workerErr = Join-Path $TestDrive 'worker.stderr.log'

    BeforeEach {
        New-Item -ItemType Directory -Path $fixtureDir -Force | Out-Null
        New-Item -ItemType Directory -Path $childWorkspace -Force | Out-Null
        if (Test-Path -LiteralPath $bridgeDir) { Remove-Item -LiteralPath $bridgeDir -Recurse -Force }
        New-BridgeFakeAgent -Path $agentPath | Out-Null
        Remove-Item Env:\CURSOR_API_KEY -ErrorAction SilentlyContinue
        Remove-Item Env:\cursor -ErrorAction SilentlyContinue
        Remove-Item Env:\FAKE_AGENT_REQUIRE_API_KEY -ErrorAction SilentlyContinue
        Remove-Item Env:\FAKE_AGENT_ECHO_API_KEY -ErrorAction SilentlyContinue
        Remove-Item Env:\FAKE_AGENT_PROFILE -ErrorAction SilentlyContinue
    }

    It 'submits one blocking request to the authenticated worker and returns its result' {
        $workerCode = '& ' + (ConvertTo-PowerShellLiteral $bridgeWorkerPath) +
            ' -Workspace ' + (ConvertTo-PowerShellLiteral $fixtureDir) +
            ' -BridgeDirectory ' + (ConvertTo-PowerShellLiteral $bridgeDir) +
            ' -AgentPath ' + (ConvertTo-PowerShellLiteral $agentPath) + ' -Once'
        $workerEncoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($workerCode))
        $worker = Start-Process -FilePath 'powershell.exe' -ArgumentList ('-NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand ' + $workerEncoded) -PassThru -WindowStyle Hidden -RedirectStandardOutput $workerOut -RedirectStandardError $workerErr

        $lockPath = Join-Path $bridgeDir 'bridge.lock'
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        $workerReady = $false
        while ([DateTime]::UtcNow -lt $deadline -and -not $workerReady) {
            if (Test-Path -LiteralPath $lockPath) {
                try {
                    $probe = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
                    $probe.Dispose()
                }
                catch [IO.IOException] { $workerReady = $true }
            }
            if (-not $workerReady) { Start-Sleep -Milliseconds 100 }
        }
        if (-not $workerReady) {
            if (Test-Path -LiteralPath $workerOut) { Write-Host ('worker stdout: ' + (Get-Content -Raw $workerOut)) }
            if (Test-Path -LiteralPath $workerErr) { Write-Host ('worker stderr: ' + (Get-Content -Raw $workerErr)) }
            if ($worker.HasExited) { Write-Host ('worker exit: ' + $worker.ExitCode) }
        }
        $workerReady | Should Be $true

        $clientCode = '& ' + (ConvertTo-PowerShellLiteral $wrapperPath) +
            ' -ViaLocalBridge -Mode composer -Workspace ' + (ConvertTo-PowerShellLiteral $childWorkspace) +
            ' -BridgeDirectory ' + (ConvertTo-PowerShellLiteral $bridgeDir) +
            ' -Prompt ' + (ConvertTo-PowerShellLiteral 'verify bridge work') + ' -TimeoutSeconds 30'
        $clientEncoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($clientCode))
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = 'powershell.exe'
        $psi.UseShellExecute = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.CreateNoWindow = $true
        $psi.Arguments = '-NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand ' + $clientEncoded
        $client = [System.Diagnostics.Process]::Start($psi)
        $stdout = $client.StandardOutput.ReadToEnd()
        $stderr = $client.StandardError.ReadToEnd()
        $client.WaitForExit()
        if ($client.ExitCode -ne 0) { Write-Host ('client stderr: ' + $stderr) }
        $worker.WaitForExit(30000) | Out-Null

        $client.ExitCode | Should Be 0
        ($stdout + $stderr) | Should Match 'bridge-work-done'
        $worker.ExitCode | Should Be 0
        (Get-ChildItem -LiteralPath (Join-Path $bridgeDir 'inbox') -ErrorAction SilentlyContinue | Measure-Object).Count | Should Be 0
        (Get-ChildItem -LiteralPath (Join-Path $bridgeDir 'working') -ErrorAction SilentlyContinue | Measure-Object).Count | Should Be 0
    }

    It 'fails immediately when no worker owns the bridge lock' {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = 'powershell.exe'
        $psi.UseShellExecute = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.CreateNoWindow = $true
        $psi.Arguments = '-NoLogo -NoProfile -ExecutionPolicy Bypass -File "' + $wrapperPath + '" -ViaLocalBridge -Workspace "' + $fixtureDir + '" -BridgeDirectory "' + $bridgeDir + '" -Prompt "do not run" -TimeoutSeconds 1'
        $process = [System.Diagnostics.Process]::Start($psi)
        $stdout = $process.StandardOutput.ReadToEnd()
        $stderr = $process.StandardError.ReadToEnd()
        $process.WaitForExit()

        ($stdout + $stderr) | Should Match 'bridge-unavailable'
        $process.ExitCode | Should Be 5
    }

    It 'refuses a concurrent request while the submit lock is held' {
        $null = New-Item -ItemType Directory -Path (Join-Path $bridgeDir 'inbox'), (Join-Path $bridgeDir 'working'), (Join-Path $bridgeDir 'outbox'), (Join-Path $bridgeDir 'cancelled') -Force
        $workerLock = [IO.File]::Open((Join-Path $bridgeDir 'bridge.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $submitLock = [IO.File]::Open((Join-Path $bridgeDir 'submit.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        try {
            $arguments = '-NoLogo -NoProfile -ExecutionPolicy Bypass -File "' + $wrapperPath + '" -ViaLocalBridge -Workspace "' + $fixtureDir + '" -BridgeDirectory "' + $bridgeDir + '" -Prompt "do not overlap" -TimeoutSeconds 1'
            $psi = New-Object System.Diagnostics.ProcessStartInfo
            $psi.FileName = 'powershell.exe'
            $psi.UseShellExecute = $false
            $psi.RedirectStandardOutput = $true
            $psi.RedirectStandardError = $true
            $psi.CreateNoWindow = $true
            $psi.Arguments = $arguments
            $process = [System.Diagnostics.Process]::Start($psi)
            $stdout = $process.StandardOutput.ReadToEnd()
            $process.WaitForExit()

            $result = $stdout | ConvertFrom-Json
            $result.classification | Should Be 'bridge-busy'
            $process.ExitCode | Should Be 6
        }
        finally {
            $submitLock.Dispose()
            $workerLock.Dispose()
        }
    }

    It 'rejects a request outside the worker workspace allowlist' {
        $inbox = Join-Path $bridgeDir 'inbox'
        $id = [Guid]::NewGuid().ToString()
        New-Item -ItemType Directory -Path $inbox -Force | Out-Null
        $workerCode = '& ' + (ConvertTo-PowerShellLiteral $bridgeWorkerPath) +
            ' -Workspace ' + (ConvertTo-PowerShellLiteral $fixtureDir) +
            ' -BridgeDirectory ' + (ConvertTo-PowerShellLiteral $bridgeDir) +
            ' -AgentPath ' + (ConvertTo-PowerShellLiteral $agentPath) + ' -Once'
        $workerEncoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($workerCode))
        $worker = Start-Process -FilePath 'powershell.exe' -ArgumentList ('-NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand ' + $workerEncoded) -PassThru -WindowStyle Hidden -RedirectStandardOutput $workerOut -RedirectStandardError $workerErr
        $lockPath = Join-Path $bridgeDir 'bridge.lock'
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        $workerReady = $false
        while ([DateTime]::UtcNow -lt $deadline -and -not $workerReady) {
            if (Test-Path -LiteralPath $lockPath) {
                try {
                    $probe = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
                    $probe.Dispose()
                }
                catch [IO.IOException] { $workerReady = $true }
            }
            if (-not $workerReady) { Start-Sleep -Milliseconds 100 }
        }
        $workerReady | Should Be $true
        [ordered]@{
            requestId = $id
            mode = 'composer'
            workspace = $repoRoot
            timeoutSeconds = 30
            prompt = 'must not run outside allowlist'
        } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $inbox ($id + '.json')) -Encoding UTF8

        $worker.WaitForExit(30000) | Should Be $true
        $worker.ExitCode | Should Be 0

        $response = Get-Content -LiteralPath (Join-Path (Join-Path $bridgeDir 'outbox') ($id + '.json')) -Raw | ConvertFrom-Json
        $response.classification | Should Be 'workspace-rejected'
        $response.exitCode | Should Be 2
    }

    It 'returns unauthenticated from the worker account without invoking work' {
        $env:FAKE_AGENT_PROFILE = 'not_logged_in'
        $workerCode = '& ' + (ConvertTo-PowerShellLiteral $bridgeWorkerPath) +
            ' -Workspace ' + (ConvertTo-PowerShellLiteral $fixtureDir) +
            ' -BridgeDirectory ' + (ConvertTo-PowerShellLiteral $bridgeDir) +
            ' -AgentPath ' + (ConvertTo-PowerShellLiteral $agentPath) + ' -Once'
        $workerEncoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($workerCode))
        $worker = Start-Process -FilePath 'powershell.exe' -ArgumentList ('-NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand ' + $workerEncoded) -PassThru -WindowStyle Hidden -RedirectStandardOutput $workerOut -RedirectStandardError $workerErr
        $lockPath = Join-Path $bridgeDir 'bridge.lock'
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        $workerReady = $false
        while ([DateTime]::UtcNow -lt $deadline -and -not $workerReady) {
            if (Test-Path -LiteralPath $lockPath) {
                try {
                    $probe = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
                    $probe.Dispose()
                }
                catch [IO.IOException] { $workerReady = $true }
            }
            if (-not $workerReady) { Start-Sleep -Milliseconds 100 }
        }
        $workerReady | Should Be $true

        $clientCode = '& ' + (ConvertTo-PowerShellLiteral $wrapperPath) +
            ' -ViaLocalBridge -Mode auto -Workspace ' + (ConvertTo-PowerShellLiteral $fixtureDir) +
            ' -BridgeDirectory ' + (ConvertTo-PowerShellLiteral $bridgeDir) +
            ' -Prompt ' + (ConvertTo-PowerShellLiteral 'auth check') + ' -TimeoutSeconds 30'
        $clientEncoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($clientCode))
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = 'powershell.exe'
        $psi.UseShellExecute = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.CreateNoWindow = $true
        $psi.Arguments = '-NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand ' + $clientEncoded
        $client = [System.Diagnostics.Process]::Start($psi)
        $stdout = $client.StandardOutput.ReadToEnd()
        $stderr = $client.StandardError.ReadToEnd()
        $client.WaitForExit()
        $worker.WaitForExit(30000) | Out-Null

        $result = $stdout | ConvertFrom-Json
        $result.classification | Should Be 'unauthenticated'
        $result.runRecord | Should Exist
        $client.ExitCode | Should Be 1
        $worker.ExitCode | Should Be 0
    }

    It 'reports API-key environment presence without exposing its value through the bridge' {
        $secret = 'FAKE_BRIDGE_API_KEY_SECRET_71e2'
        $env:CURSOR_API_KEY = $secret
        try {
            $workerCode = '& ' + (ConvertTo-PowerShellLiteral $bridgeWorkerPath) +
                ' -Workspace ' + (ConvertTo-PowerShellLiteral $fixtureDir) +
                ' -BridgeDirectory ' + (ConvertTo-PowerShellLiteral $bridgeDir) +
                ' -AgentPath ' + (ConvertTo-PowerShellLiteral $agentPath) + ' -Once'
            $workerEncoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($workerCode))
            $worker = Start-Process -FilePath 'powershell.exe' -ArgumentList ('-NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand ' + $workerEncoded) -PassThru -WindowStyle Hidden -RedirectStandardOutput $workerOut -RedirectStandardError $workerErr
            $lockPath = Join-Path $bridgeDir 'bridge.lock'
            $deadline = [DateTime]::UtcNow.AddSeconds(15)
            $workerReady = $false
            while ([DateTime]::UtcNow -lt $deadline -and -not $workerReady) {
                if (Test-Path -LiteralPath $lockPath) {
                    try {
                        $probe = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
                        $probe.Dispose()
                    }
                    catch [IO.IOException] { $workerReady = $true }
                }
                if (-not $workerReady) { Start-Sleep -Milliseconds 100 }
            }
            $workerReady | Should Be $true

            $psi = New-Object System.Diagnostics.ProcessStartInfo
            $psi.FileName = 'powershell.exe'
            $psi.UseShellExecute = $false
            $psi.RedirectStandardOutput = $true
            $psi.RedirectStandardError = $true
            $psi.CreateNoWindow = $true
            $psi.Arguments = '-NoLogo -NoProfile -ExecutionPolicy Bypass -File "' + $wrapperPath + '" -ViaLocalBridge -Mode composer -Workspace "' + $fixtureDir + '" -BridgeDirectory "' + $bridgeDir + '" -Prompt "api-key guard" -TimeoutSeconds 30'
            $client = [System.Diagnostics.Process]::Start($psi)
            $stdout = $client.StandardOutput.ReadToEnd()
            $stderr = $client.StandardError.ReadToEnd()
            $client.WaitForExit()
            $worker.WaitForExit(30000) | Out-Null

            $result = $stdout | ConvertFrom-Json
            $result.classification | Should Be 'api-key-env-present'
            ($stdout + $stderr) | Should Not Match $secret
            (Get-Content -LiteralPath $result.runRecord -Raw) | Should Not Match $secret
            $client.ExitCode | Should Be 3
            $worker.ExitCode | Should Be 0
        }
        finally {
            Remove-Item Env:\CURSOR_API_KEY -ErrorAction SilentlyContinue
        }
    }

    It 'maps the opted-in cursor environment variable to CURSOR_API_KEY only for the worker child process' {
        $secret = 'FAKE_BRIDGE_API_KEY_SECRET_ALIAS_4a19'
        $env:cursor = $secret
        $env:FAKE_AGENT_REQUIRE_API_KEY = '1'
        $env:FAKE_AGENT_ECHO_API_KEY = '1'
        try {
            $workerCode = '& ' + (ConvertTo-PowerShellLiteral $bridgeWorkerPath) +
                ' -Workspace ' + (ConvertTo-PowerShellLiteral $fixtureDir) +
                ' -BridgeDirectory ' + (ConvertTo-PowerShellLiteral $bridgeDir) +
                ' -AgentPath ' + (ConvertTo-PowerShellLiteral $agentPath) + ' -UseCursorApiKeyEnvironmentVariable -Once'
            $workerEncoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($workerCode))
            $worker = Start-Process -FilePath 'powershell.exe' -ArgumentList ('-NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand ' + $workerEncoded) -PassThru -WindowStyle Hidden -RedirectStandardOutput $workerOut -RedirectStandardError $workerErr
            $lockPath = Join-Path $bridgeDir 'bridge.lock'
            $deadline = [DateTime]::UtcNow.AddSeconds(15)
            $workerReady = $false
            while ([DateTime]::UtcNow -lt $deadline -and -not $workerReady) {
                if (Test-Path -LiteralPath $lockPath) {
                    try {
                        $probe = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
                        $probe.Dispose()
                    }
                    catch [IO.IOException] { $workerReady = $true }
                }
                if (-not $workerReady) { Start-Sleep -Milliseconds 100 }
            }
            $workerReady | Should Be $true

            $clientCode = '& ' + (ConvertTo-PowerShellLiteral $wrapperPath) +
                ' -ViaLocalBridge -Mode composer -Workspace ' + (ConvertTo-PowerShellLiteral $childWorkspace) +
                ' -BridgeDirectory ' + (ConvertTo-PowerShellLiteral $bridgeDir) +
                ' -Prompt ' + (ConvertTo-PowerShellLiteral 'api key alias route') + ' -TimeoutSeconds 30'
            $clientEncoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($clientCode))
            $psi = New-Object System.Diagnostics.ProcessStartInfo
            $psi.FileName = 'powershell.exe'
            $psi.UseShellExecute = $false
            $psi.RedirectStandardOutput = $true
            $psi.RedirectStandardError = $true
            $psi.CreateNoWindow = $true
            $psi.Arguments = '-NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand ' + $clientEncoded
            $client = [System.Diagnostics.Process]::Start($psi)
            $stdout = $client.StandardOutput.ReadToEnd()
            $stderr = $client.StandardError.ReadToEnd()
            $client.WaitForExit()
            $worker.WaitForExit(30000) | Out-Null

            $client.ExitCode | Should Be 0
            ($stdout + $stderr) | Should Match 'bridge-work-done'
            ($stdout + $stderr) | Should Not Match $secret
            $latestRun = Get-ChildItem -LiteralPath (Join-Path $repoRoot '.harness/runs/cursor-agent') -Directory |
                Sort-Object LastWriteTime -Descending | Select-Object -First 1
            (Get-Content -Raw (Join-Path $latestRun.FullName 'stderr.log')) | Should Not Match $secret
            (Get-Content -Raw (Join-Path $latestRun.FullName 'run.json')) | Should Not Match $secret
            $worker.ExitCode | Should Be 0
            (Get-ChildItem -LiteralPath (Join-Path $bridgeDir 'inbox') -ErrorAction SilentlyContinue | Measure-Object).Count | Should Be 0
            (Get-ChildItem -LiteralPath (Join-Path $bridgeDir 'working') -ErrorAction SilentlyContinue | Measure-Object).Count | Should Be 0
        }
        finally {
            Remove-Item Env:\cursor -ErrorAction SilentlyContinue
            Remove-Item Env:\FAKE_AGENT_REQUIRE_API_KEY -ErrorAction SilentlyContinue
            Remove-Item Env:\FAKE_AGENT_ECHO_API_KEY -ErrorAction SilentlyContinue
        }
    }
}

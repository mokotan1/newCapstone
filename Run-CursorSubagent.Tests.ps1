$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent (Split-Path -Parent $scriptRoot)
$wrapperPath = Join-Path $repoRoot 'scripts/run-cursor-subagent.ps1'
$fakeSecretMarker = 'FAKE_CURSOR_API_SECRET_MARKER_9f3c2a'

function New-FakeAgentFixture {
    param([string]$FixtureDir)

    $logicPath = Join-Path $FixtureDir 'fake-agent-logic.ps1'
    $cmdPath = Join-Path $FixtureDir 'fake-agent.cmd'
    $logic = @'
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Rest)
$profile = $env:FAKE_AGENT_PROFILE
if (-not $profile) { $profile = 'logged_in' }
if ($Rest.Count -eq 1 -and $Rest[0] -eq 'status') {
    switch ($profile) {
        'logged_in' { Write-Output 'Logged in'; exit 0 }
        'not_logged_in' { Write-Output 'Not logged in'; exit 1 }
        default { Write-Output 'Not logged in'; exit 1 }
    }
}
if ($Rest -contains '--version') {
    Write-Output 'fake-agent 9.9.9-test'
    exit 0
}
if ($Rest -contains '-p') {
    $promptIndex = [Array]::IndexOf($Rest, '--')
    $prompt = if ($promptIndex -ge 0 -and $promptIndex + 1 -lt $Rest.Count) { $Rest[$promptIndex + 1] } else { '' }
    $mode = $env:FAKE_AGENT_MODE
    if ($mode -eq 'block') {
        Write-Output 'blocked'
        exit 1
    }
    if ($mode -eq 'partial_only') {
        Write-Output '{"type":"system","subtype":"init"}'
        exit 0
    }
    if ($prompt -like '*CURSOR_WRAPPER_PROBE_OK*') {
        Write-Output '{"type":"system","subtype":"init"}'
        Write-Output '{"type":"assistant","message":{"content":[{"type":"text","text":"CURSOR_WRAPPER_PROBE_OK"}]}}'
        Write-Output '{"type":"result","subtype":"success","result":"CURSOR_WRAPPER_PROBE_OK","model":"auto"}'
        exit 0
    }
    if ($mode -eq 'timeout_parent') {
        $pidFile = Join-Path $env:FAKE_AGENT_FIXTURE_DIR 'child.pid'
        $child = Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-Command', 'Start-Sleep -Seconds 120') -PassThru
        Set-Content -LiteralPath $pidFile -Value $child.Id -Encoding ASCII
        Start-Sleep -Seconds 120
        exit 0
    }
    Write-Output '{"type":"result","subtype":"success","result":"work-done","model":"composer-2.5"}'
    exit 0
}
Write-Output 'unknown'
exit 1
'@
    Set-Content -LiteralPath $logicPath -Value $logic -Encoding UTF8
    $cmd = "@echo off`r`npowershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$logicPath`" %*"
    Set-Content -LiteralPath $cmdPath -Value $cmd -Encoding ASCII
    return $cmdPath
}

function Invoke-Wrapper {
    param(
        [hashtable]$Params,
        [string]$FixtureDir,
        [string]$Profile = 'logged_in',
        [string]$Mode = $null
    )

    $fakeAgent = New-FakeAgentFixture -FixtureDir $FixtureDir
    $env:FAKE_AGENT_PROFILE = $Profile
    $env:FAKE_AGENT_FIXTURE_DIR = $FixtureDir
    if ($Mode) {
        $env:FAKE_AGENT_MODE = $Mode
    }
    else {
        Remove-Item Env:\FAKE_AGENT_MODE -ErrorAction SilentlyContinue
    }

    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $wrapperPath)
    foreach ($key in $Params.Keys) {
        $value = $Params[$key]
        if ($value -is [bool]) {
            if ($value) {
                $arguments += "-$key"
            }
        }
        elseif ($null -ne $value -and "$value" -ne '') {
            $arguments += "-$key"
            $arguments += "$value"
        }
    }
    $arguments += '-AgentPath'
    $arguments += $fakeAgent
    $arguments += '-TimeoutSeconds'
    $arguments += '30'

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'powershell.exe'
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $psi.Arguments = ($arguments | ForEach-Object {
            if ($_ -match '\s') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ }
        }) -join ' '
    $process = [System.Diagnostics.Process]::Start($psi)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    return [pscustomobject]@{
        ExitCode = $process.ExitCode
        Stdout   = $stdout
        Stderr   = $stderr
    }
}

Describe 'Run-CursorSubagent wrapper' {
    $fixtureDir = Join-Path $TestDrive 'fake-agent-fixture'
    BeforeEach {
        New-Item -ItemType Directory -Path $fixtureDir -Force | Out-Null
        Remove-Item Env:\CURSOR_API_KEY -ErrorAction SilentlyContinue
        Remove-Item Env:\FAKE_AGENT_CHILD_PID -ErrorAction SilentlyContinue
    }

    Context 'Task 1 diagnostics' {
        It 'uses the exact -AgentPath for version and status' {
            $result = Invoke-Wrapper -FixtureDir $fixtureDir -Params @{
                Diagnose = $true
                Workspace = $fixtureDir
            }
            $json = $result.Stdout | ConvertFrom-Json
            $json.agentVersion | Should Be 'fake-agent 9.9.9-test'
            $json.agentPath | Should Match 'fake-agent\.cmd$'
            $json.authStatus | Should Be 'authenticated'
        }

        It 'classifies missing CLI as tool-missing' {
            $missing = Join-Path $fixtureDir 'missing-agent.cmd'
            $psi = New-Object System.Diagnostics.ProcessStartInfo
            $psi.FileName = 'powershell.exe'
            $psi.UseShellExecute = $false
            $psi.RedirectStandardOutput = $true
            $psi.RedirectStandardError = $true
            $psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$wrapperPath`" -Diagnose -AgentPath `"$missing`""
            $process = [System.Diagnostics.Process]::Start($psi)
            $stdout = $process.StandardOutput.ReadToEnd()
            $process.WaitForExit()
            $json = $stdout | ConvertFrom-Json
            $json.authStatus | Should Be 'tool-missing'
            $process.ExitCode | Should Be 2
        }

        It 'reports unauthenticated when status says not logged in' {
            $result = Invoke-Wrapper -FixtureDir $fixtureDir -Profile 'not_logged_in' -Params @{
                AuthStatus = $true
            }
            $result.Stdout.Trim() | Should Be 'Cursor agent CLI: unauthenticated'
            $result.ExitCode | Should Be 1
        }

        It 'reports api key presence without leaking the secret marker' {
            $env:CURSOR_API_KEY = $fakeSecretMarker
            $result = Invoke-Wrapper -FixtureDir $fixtureDir -Params @{
                Diagnose = $true
                Workspace = $fixtureDir
            }
            $json = $result.Stdout | ConvertFrom-Json
            $json.apiKeyPresent | Should Be $true
            $json.authStatus | Should Be 'api-key-env-present'
            ($result.Stdout + $result.Stderr) | Should Not Match $fakeSecretMarker
            $result.ExitCode | Should Be 3
        }

        It 'maps -AuthStatus to concise authenticated output' {
            $result = Invoke-Wrapper -FixtureDir $fixtureDir -Params @{
                AuthStatus = $true
            }
            $result.Stdout.Trim() | Should Be 'Cursor agent CLI: authenticated'
            $result.ExitCode | Should Be 0
        }
    }

    Context 'Task 2 probe and evidence' {
        It 'runs probe successfully with final response and run record' {
            $result = Invoke-Wrapper -FixtureDir $fixtureDir -Params @{
                Probe = $true
                Workspace = $fixtureDir
            }
            $json = $result.Stdout | ConvertFrom-Json
            $json.classification | Should Be 'completed'
            $json.finalResponse | Should Be 'CURSOR_WRAPPER_PROBE_OK'
            Test-Path -LiteralPath $json.runRecord | Should Be $true
            $record = Get-Content -LiteralPath $json.runRecord -Raw | ConvertFrom-Json
            $record.workspace | Should Be (Resolve-Path -LiteralPath $fixtureDir).Path
            $result.ExitCode | Should Be 0
        }

        It 'fails probe when stream has no final result' {
            $env:FAKE_AGENT_MODE = 'partial_only'
            $fakeAgent = New-FakeAgentFixture -FixtureDir $fixtureDir
            $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$wrapperPath`" -Probe -Workspace `"$fixtureDir`" -AgentPath `"$fakeAgent`" -TimeoutSeconds 30"
            $env:FAKE_AGENT_PROFILE = 'logged_in'
            $psi = New-Object System.Diagnostics.ProcessStartInfo
            $psi.FileName = 'powershell.exe'
            $psi.UseShellExecute = $false
            $psi.RedirectStandardOutput = $true
            $psi.RedirectStandardError = $true
            $psi.Arguments = $arguments
            $process = [System.Diagnostics.Process]::Start($psi)
            $stdout = $process.StandardOutput.ReadToEnd()
            $process.WaitForExit()
            $json = $stdout | ConvertFrom-Json
            $json.classification | Should Be 'failed'
            $process.ExitCode | Should Not Be 0
        }

        It 'blocks probe when auth fails' {
            $result = Invoke-Wrapper -FixtureDir $fixtureDir -Profile 'not_logged_in' -Params @{
                Probe = $true
                Workspace = $fixtureDir
            }
            $json = $result.Stdout | ConvertFrom-Json
            $json.classification | Should Be 'unauthenticated'
            $result.ExitCode | Should Be 1
        }

        It 'does not run work when auth fails' {
            $result = Invoke-Wrapper -FixtureDir $fixtureDir -Profile 'not_logged_in' -Params @{
                Prompt = 'do work'
                Mode = 'composer'
                Workspace = $fixtureDir
            }
            $result.ExitCode | Should Not Be 0
            $result.Stdout | Should Not Match 'work-done'
        }
    }

    Context 'Task 3 lifecycle' {
        It 'times out and terminates child process tree' {
            $fakeAgent = New-FakeAgentFixture -FixtureDir $fixtureDir
            $env:FAKE_AGENT_PROFILE = 'logged_in'
            $env:FAKE_AGENT_MODE = 'timeout_parent'
            $env:FAKE_AGENT_FIXTURE_DIR = $fixtureDir
            $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$wrapperPath`" -Prompt `"block`" -Mode composer -Workspace `"$fixtureDir`" -AgentPath `"$fakeAgent`" -TimeoutSeconds 2"
            $psi = New-Object System.Diagnostics.ProcessStartInfo
            $psi.FileName = 'powershell.exe'
            $psi.UseShellExecute = $false
            $psi.RedirectStandardOutput = $true
            $psi.RedirectStandardError = $true
            $psi.Arguments = $arguments
            $process = [System.Diagnostics.Process]::Start($psi)
            $stdout = $process.StandardOutput.ReadToEnd()
            $process.WaitForExit()
            $process.ExitCode | Should Be 124
            $pidFile = Join-Path $fixtureDir 'child.pid'
            if (Test-Path -LiteralPath $pidFile) {
                $childId = [int](Get-Content -LiteralPath $pidFile -Raw)
                $childAlive = Get-Process -Id $childId -ErrorAction SilentlyContinue
                $childAlive | Should Be $null
            }
        }

        It 'allows a subsequent run after timeout cleanup' {
            $first = Invoke-Wrapper -FixtureDir $fixtureDir -Mode 'block' -Params @{
                Prompt = 'first'
                Mode = 'composer'
                Workspace = $fixtureDir
            }
            $first.ExitCode | Should Not Be 0
            $second = Invoke-Wrapper -FixtureDir $fixtureDir -Params @{
                Probe = $true
                Workspace = $fixtureDir
            }
            $second.ExitCode | Should Be 0
        }
    }

    Context 'Dry run' {
        It 'shows composer dry-run without contacting cursor' {
            $fakeAgent = New-FakeAgentFixture -FixtureDir $fixtureDir
            $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$wrapperPath`" -DryRun -Mode composer -Workspace `"$fixtureDir`" -Prompt dry-run -AgentPath `"$fakeAgent`""
            $psi = New-Object System.Diagnostics.ProcessStartInfo
            $psi.FileName = 'powershell.exe'
            $psi.UseShellExecute = $false
            $psi.RedirectStandardOutput = $true
            $psi.Arguments = $arguments
            $process = [System.Diagnostics.Process]::Start($psi)
            $stdout = $process.StandardOutput.ReadToEnd()
            $process.WaitForExit()
            $json = $stdout | ConvertFrom-Json
            $json.mode | Should Be 'composer'
            ($json.args -contains 'composer-2.5') | Should Be $true
            $process.ExitCode | Should Be 0
        }
    }
}

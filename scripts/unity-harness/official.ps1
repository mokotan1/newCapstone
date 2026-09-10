#Requires -Version 5.1
param(
    [Parameter(Mandatory = $true)]
    [string]$Operation,
    [string]$ProjectPath = "disputatio"
)

$payload = [ordered]@{
    schemaVersion      = "1"
    operation          = $Operation
    backend            = "official-unity-cli"
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
    errorCategory      = "version-mismatch"
    resultKind         = "backend-not-verified"
}
$payload | ConvertTo-Json -Compress
exit 2

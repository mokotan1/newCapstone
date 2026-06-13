$ErrorActionPreference = "Stop"

$unityCli = Join-Path $env:LOCALAPPDATA "unity-cli\unity-cli.exe"

if (-not (Test-Path -LiteralPath $unityCli)) {
    Write-Error "unity-cli.exe not found at $unityCli. Install or repair unity-cli, then retry."
    exit 9009
}

& $unityCli @args
exit $LASTEXITCODE

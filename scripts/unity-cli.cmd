@echo off
setlocal

set "UNITY_CLI=%LOCALAPPDATA%\unity-cli\unity-cli.exe"

if not exist "%UNITY_CLI%" (
  echo unity-cli.exe not found at "%UNITY_CLI%" 1>&2
  echo Install or repair unity-cli, then retry. 1>&2
  exit /b 9009
)

"%UNITY_CLI%" %*

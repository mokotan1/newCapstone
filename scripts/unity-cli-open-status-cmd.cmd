@echo off
setlocal

set "ROOT=%~dp0.."
set "WATCHER=%~dp0unity-cli-status-watch.cmd"

if not exist "%WATCHER%" (
  echo Unity CLI status watcher not found at "%WATCHER%" 1>&2
  exit /b 1
)

start "Unity CLI Status - newCapstone" cmd /k "cd /d "%ROOT%" && "%WATCHER%""

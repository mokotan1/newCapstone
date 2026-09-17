@echo off
setlocal

set "INTERVAL=%~1"
if "%INTERVAL%"=="" set "INTERVAL=2"

set "ROOT=%~dp0.."
set "WATCHPY=%ROOT%\scripts\qa\tool\status_watch.py"

if not exist "%WATCHPY%" (
  echo status_watch.py not found at "%WATCHPY%" 1>&2
  exit /b 1
)

:loop
cls
echo.
python -u "%WATCHPY%"
echo.
echo Refreshing every %INTERVAL% seconds. Press Ctrl+C to stop.
timeout /t %INTERVAL% /nobreak >nul
goto loop

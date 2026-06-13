@echo off
setlocal

set "INTERVAL=%~1"
if "%INTERVAL%"=="" set "INTERVAL=2"

:loop
cls
echo Unity CLI status watcher
echo Project: disputatio
echo Time: %date% %time%
echo.
call "%~dp0unity-cli.cmd" --project disputatio status
echo.
echo Refreshing every %INTERVAL% seconds. Press Ctrl+C to stop.
timeout /t %INTERVAL% /nobreak >nul
goto loop

@echo off
setlocal

set "POWERSHELL_EXE=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
"%POWERSHELL_EXE%" -ExecutionPolicy Bypass -NoProfile -File "%~dp0run-windows-client.ps1" %*
if errorlevel 1 (
    set "EXIT_CODE=%errorlevel%"
    echo.
    echo SnapLingo Windows client failed to start.
    pause
    exit /b %EXIT_CODE%
)

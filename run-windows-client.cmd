@echo off
setlocal

powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0run-windows-client.ps1" %*
if errorlevel 1 (
    echo.
    echo SnapLingo Windows client failed to start.
    pause
    exit /b %errorlevel%
)

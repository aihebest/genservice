@echo off
REM GenService Platform — Local Launcher
REM Double-click this file to start the platform without Docker.

echo.
echo Starting GenService Platform locally...
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0start-local.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Launch failed. See error above.
    pause
)

@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0DevTools.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo DevTools exited with error code %ERRORLEVEL%
    pause
)

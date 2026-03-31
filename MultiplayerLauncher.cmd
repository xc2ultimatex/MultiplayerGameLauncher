@echo off
setlocal

set "ROOT_DIR=%~dp0"
set "CONFIGURATION=Release"
set "RUNTIME=win-x64"
set "OUTPUT_DIR=%ROOT_DIR%bin\%CONFIGURATION%\net8.0-windows\%RUNTIME%"
set "APP_EXE=%OUTPUT_DIR%\MultiplayerLauncher.exe"

if not exist "%OUTPUT_DIR%" (
    mkdir "%OUTPUT_DIR%" >nul 2>&1
)

if not exist "%APP_EXE%" (
    where dotnet >nul 2>&1
    if errorlevel 1 (
        echo The launcher executable was not found and the .NET SDK is not installed.
        echo Install the .NET 8 SDK or publish the launcher before running this script.
        exit /b 1
    )

    echo Building MultiplayerLauncher for this machine...
    pushd "%ROOT_DIR%"
    dotnet build ".\MultiplayerLauncher.csproj" -c %CONFIGURATION% -r %RUNTIME% -nologo
    set "BUILD_EXIT=%ERRORLEVEL%"
    popd

    if not "%BUILD_EXIT%"=="0" (
        exit /b %BUILD_EXIT%
    )
)

if not exist "%APP_EXE%" (
    echo Build completed, but "%APP_EXE%" was not created.
    exit /b 1
)

start "" "%APP_EXE%"

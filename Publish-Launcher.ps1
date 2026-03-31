param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputDirectory = Join-Path $projectRoot "dist\\MultiplayerLauncher"

if (Test-Path $outputDirectory) {
    Remove-Item -Recurse -Force $outputDirectory
}

New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

dotnet publish (Join-Path $projectRoot "MultiplayerLauncher.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:DebugSymbols=false `
    -p:DebugType=None `
    -o $outputDirectory

$pdbPath = Join-Path $outputDirectory "MultiplayerLauncher.pdb"
if (Test-Path $pdbPath) {
    Remove-Item -Force $pdbPath
}

$settingsPath = Join-Path $outputDirectory "launcher.settings.json"
if (Test-Path $settingsPath) {
    Remove-Item -Force $settingsPath
}

Write-Host ""
Write-Host "Published launcher to:"
Write-Host "  $outputDirectory"
Write-Host ""
Write-Host "Give users this application:"
Write-Host "  $(Join-Path $outputDirectory 'MultiplayerLauncher.exe')"

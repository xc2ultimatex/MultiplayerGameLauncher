param(
    [string]$FeedRoot = "C:\MultiplayerPrototypeBuilds",
    [int]$Port = 8080,
    [switch]$RefreshArchive
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $FeedRoot)) {
    throw "Feed root not found: $FeedRoot"
}

$latestPath = Join-Path $FeedRoot "Latest"
if (-not (Test-Path $latestPath)) {
    throw "Latest feed folder not found: $latestPath"
}

$manifestPath = Join-Path $latestPath "manifest.json"
if (-not (Test-Path $manifestPath)) {
    throw "Manifest not found: $manifestPath"
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$packageDirectoryName = if ([string]::IsNullOrWhiteSpace($manifest.packageDirectory)) { "payload" } else { [string]$manifest.packageDirectory }
$packageArchiveName = if ([string]::IsNullOrWhiteSpace($manifest.packageArchive)) { "$packageDirectoryName.zip" } else { [string]$manifest.packageArchive }
$packageDirectoryPath = Join-Path $latestPath $packageDirectoryName
$packageArchivePath = Join-Path $latestPath $packageArchiveName

if (-not (Test-Path $packageDirectoryPath)) {
    throw "Package directory not found: $packageDirectoryPath"
}

if ($RefreshArchive -or -not (Test-Path $packageArchivePath)) {
    if (Test-Path $packageArchivePath) {
        Remove-Item -Force $packageArchivePath
    }

    Write-Host ""
    Write-Host "Creating package archive:"
    Write-Host "  $packageArchivePath"
    Compress-Archive -Path (Join-Path $packageDirectoryPath "*") -DestinationPath $packageArchivePath -Force
}

$python = Get-Command python -ErrorAction Stop

$publicIp = $null
try {
    $publicIp = (Invoke-RestMethod "https://api.ipify.org?format=json").ip
}
catch {
}

Write-Host ""
Write-Host "Serving update feed from:"
Write-Host "  $FeedRoot"
Write-Host ""
Write-Host "Package archive:"
Write-Host "  $packageArchivePath"
Write-Host ""
Write-Host "Local URL:"
Write-Host "  http://localhost:$Port/Latest"

if ($publicIp) {
    Write-Host ""
    Write-Host "Public URL:"
    Write-Host "  http://$publicIp`:$Port/Latest"
}

Write-Host ""
Write-Host "Keep this window open while clients are updating."
Write-Host "Make sure your router forwards TCP port $Port to this PC."
Write-Host ""

& $python.Source -m http.server $Port --bind 0.0.0.0 --directory $FeedRoot

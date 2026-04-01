param(
    [string]$FeedRoot = "C:\MultiplayerPrototypeBuilds",
    [int]$Port = 8080
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

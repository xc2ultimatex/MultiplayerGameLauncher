param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$PublishScript = Join-Path $ScriptDir "Publish-LatestBuild.ps1"

$Games = @("ShopGame", "CamgirlSim")

$results = @()

foreach ($game in $Games) {
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    Write-Host " Publishing: $game"
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    try {
        $params = @{ GameName = $game }
        if (-not [string]::IsNullOrWhiteSpace($Version)) {
            $params.Version = $Version
        }

        & $PublishScript @params

        $results += [PSCustomObject]@{ Game = $game; Status = "OK"; Error = "" }
    } catch {
        Write-Warning "[$game] Publish failed: $($_.Exception.Message)"
        $results += [PSCustomObject]@{ Game = $game; Status = "FAILED"; Error = $_.Exception.Message }
    }
}

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
Write-Host " Summary"
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
foreach ($r in $results) {
    $icon = if ($r.Status -eq "OK") { "✓" } else { "✗" }
    Write-Host "  $icon $($r.Game): $($r.Status)"
    if ($r.Error) { Write-Host "      $($r.Error)" }
}

$failed = $results | Where-Object { $_.Status -ne "OK" }
if ($failed) {
    Write-Host ""
    exit 1
}

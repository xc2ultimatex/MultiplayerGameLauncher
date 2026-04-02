param(
    [string]$FeedRoot = "C:\MultiplayerPrototypeBuilds",
    [int]$Port = 8080,
    [switch]$RefreshArchive,
    [int]$QuietSeconds = 3
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
$logDirectoryPath = Join-Path $FeedRoot ".feed-logs"
$stdoutLogPath = Join-Path $logDirectoryPath "http-server.out.log"
$stderrLogPath = Join-Path $logDirectoryPath "http-server.err.log"

if (-not (Test-Path $packageDirectoryPath)) {
    throw "Package directory not found: $packageDirectoryPath"
}

function Get-LatestPayloadWriteTime {
    $items = @()

    if (Test-Path $packageDirectoryPath) {
        $items += Get-Item -LiteralPath $packageDirectoryPath
        $items += Get-ChildItem -LiteralPath $packageDirectoryPath -Recurse -Force -ErrorAction SilentlyContinue
    }

    $latestItem = $items |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($latestItem) {
        return $latestItem.LastWriteTime
    }

    return [DateTime]::MinValue
}

function Test-ArchiveRefreshNeeded {
    if (-not (Test-Path $packageArchivePath -PathType Leaf)) {
        return $true
    }

    $archiveWriteTime = (Get-Item -LiteralPath $packageArchivePath).LastWriteTime
    $payloadWriteTime = Get-LatestPayloadWriteTime
    return $payloadWriteTime -gt $archiveWriteTime
}

function Update-PackageArchive {
    if (Test-Path $packageArchivePath) {
        Remove-Item -Force $packageArchivePath
    }

    Write-Host ""
    Write-Host "Refreshing package archive:"
    Write-Host "  $packageArchivePath"
    Compress-Archive -Path (Join-Path $packageDirectoryPath "*") -DestinationPath $packageArchivePath -Force
}

if ($RefreshArchive -or (Test-ArchiveRefreshNeeded)) {
    Update-PackageArchive
}

New-Item -ItemType Directory -Force -Path $logDirectoryPath | Out-Null

$existingListeners = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
foreach ($listener in $existingListeners) {
    try {
        Stop-Process -Id $listener.OwningProcess -Force -ErrorAction Stop
        Write-Host ""
        Write-Host "Stopped existing process on port ${Port}:"
        Write-Host "  PID $($listener.OwningProcess)"
    }
    catch {
        Write-Warning "Could not stop existing process $($listener.OwningProcess) on port ${Port}: $($_.Exception.Message)"
    }
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
Write-Host "Watching payload folder for changes:"
Write-Host "  $packageDirectoryPath"
Write-Host ""
Write-Host "Local URL:"
Write-Host "  http://localhost:$Port/Latest"

if ($publicIp) {
    if (Test-Path $packageArchivePath) {
        Write-Host ""
        Write-Host "Public URL:"
        Write-Host "  http://$publicIp`:$Port/Latest"
    }
}

Write-Host ""
Write-Host "Keep this window open while clients are updating or while you are publishing builds."
Write-Host "Make sure your router forwards TCP port $Port to this PC."
Write-Host ""

$pythonProcess = Start-Process -FilePath $python.Source `
    -ArgumentList @("-m", "http.server", $Port, "--bind", "0.0.0.0", "--directory", $FeedRoot) `
    -PassThru `
    -RedirectStandardOutput $stdoutLogPath `
    -RedirectStandardError $stderrLogPath

$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = $latestPath
$watcher.IncludeSubdirectories = $true
$watcher.EnableRaisingEvents = $true

$script:archiveRefreshPending = $false
$script:archiveRefreshAfter = [DateTime]::MinValue

$queueRefresh = {
    $eventArgs = $Event.SourceEventArgs
    $changedPaths = @()

    if ($eventArgs -and $eventArgs.PSObject.Properties.Name -contains "FullPath") {
        $changedPaths += $eventArgs.FullPath
    }

    if ($eventArgs -and $eventArgs.PSObject.Properties.Name -contains "OldFullPath") {
        $changedPaths += $eventArgs.OldFullPath
    }

    $payloadPrefix = $packageDirectoryPath.TrimEnd('\') + '\'
    $matchesPayload = $false

    foreach ($changedPath in $changedPaths) {
        if ([string]::IsNullOrWhiteSpace($changedPath)) {
            continue
        }

        $normalizedPath = [System.IO.Path]::GetFullPath($changedPath)
        if ($normalizedPath.StartsWith($payloadPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
            [System.StringComparer]::OrdinalIgnoreCase.Equals($normalizedPath.TrimEnd('\'), $packageDirectoryPath.TrimEnd('\'))) {
            $matchesPayload = $true
            break
        }
    }

    if ($matchesPayload) {
        $script:archiveRefreshPending = $true
        $script:archiveRefreshAfter = (Get-Date).AddSeconds($QuietSeconds)
    }
}

$createdEvent = Register-ObjectEvent -InputObject $watcher -EventName Created -Action $queueRefresh
$changedEvent = Register-ObjectEvent -InputObject $watcher -EventName Changed -Action $queueRefresh
$deletedEvent = Register-ObjectEvent -InputObject $watcher -EventName Deleted -Action $queueRefresh
$renamedEvent = Register-ObjectEvent -InputObject $watcher -EventName Renamed -Action $queueRefresh

try {
    while (-not $pythonProcess.HasExited) {
        Start-Sleep -Seconds 1
        $pythonProcess.Refresh()

        if ($script:archiveRefreshPending -and (Get-Date) -ge $script:archiveRefreshAfter) {
            $script:archiveRefreshPending = $false

            try {
                Update-PackageArchive
                Write-Host ""
                Write-Host "Archive refresh complete."
            }
            catch {
                Write-Warning "Archive refresh failed: $($_.Exception.Message)"
                $script:archiveRefreshPending = $true
                $script:archiveRefreshAfter = (Get-Date).AddSeconds($QuietSeconds)
            }
        }
    }
}
finally {
    Unregister-Event -SourceIdentifier $createdEvent.Name -ErrorAction SilentlyContinue
    Unregister-Event -SourceIdentifier $changedEvent.Name -ErrorAction SilentlyContinue
    Unregister-Event -SourceIdentifier $deletedEvent.Name -ErrorAction SilentlyContinue
    Unregister-Event -SourceIdentifier $renamedEvent.Name -ErrorAction SilentlyContinue

    $watcher.EnableRaisingEvents = $false
    $watcher.Dispose()

    if (-not $pythonProcess.HasExited) {
        Stop-Process -Id $pythonProcess.Id -Force
    }
}

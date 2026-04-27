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

$logDirectoryPath = Join-Path $FeedRoot ".feed-logs"
$stdoutLogPath    = Join-Path $logDirectoryPath "http-server.out.log"
$stderrLogPath    = Join-Path $logDirectoryPath "http-server.err.log"

# ── Discover all game feeds ────────────────────────────────────────────────────
# A feed is any subdirectory of $FeedRoot that contains a manifest.json.

function Get-GameFeeds {
    param([string]$Root)
    $feeds = @()
    foreach ($dir in Get-ChildItem -LiteralPath $Root -Directory -ErrorAction SilentlyContinue) {
        if ($dir.Name.StartsWith('.')) { continue }
        $manifestPath = Join-Path $dir.FullName "manifest.json"
        if (-not (Test-Path $manifestPath)) { continue }

        $manifest         = Get-Content $manifestPath -Raw | ConvertFrom-Json
        $packageDirName   = if (-not [string]::IsNullOrWhiteSpace($manifest.packageDirectory)) { [string]$manifest.packageDirectory } else { "payload" }
        $packageArchive   = if (-not [string]::IsNullOrWhiteSpace($manifest.packageArchive))   { [string]$manifest.packageArchive   } else { "$packageDirName.zip" }

        $feeds += [PSCustomObject]@{
            Name             = $dir.Name
            Directory        = $dir.FullName
            ManifestPath     = $manifestPath
            PayloadDirectory = Join-Path $dir.FullName $packageDirName
            ArchivePath      = Join-Path $dir.FullName $packageArchive
        }
    }
    return $feeds
}

$feeds = Get-GameFeeds -Root $FeedRoot

if ($feeds.Count -eq 0) {
    Write-Warning "No game feeds found in $FeedRoot. A feed is a subdirectory containing a manifest.json."
}

# ── Archive helpers ────────────────────────────────────────────────────────────

function Get-LatestPayloadWriteTime {
    param($Feed)
    $items = @()
    if (Test-Path $Feed.PayloadDirectory) {
        $items += Get-Item -LiteralPath $Feed.PayloadDirectory
        $items += Get-ChildItem -LiteralPath $Feed.PayloadDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
    $latest = $items | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    return if ($latest) { $latest.LastWriteTime } else { [DateTime]::MinValue }
}

function Test-ArchiveRefreshNeeded {
    param($Feed)
    if (-not (Test-Path $Feed.ArchivePath -PathType Leaf)) { return $true }
    $archiveTime = (Get-Item -LiteralPath $Feed.ArchivePath).LastWriteTime
    $payloadTime = Get-LatestPayloadWriteTime -Feed $Feed
    return $payloadTime -gt $archiveTime
}

function Update-PackageArchive {
    param($Feed)
    if (-not (Test-Path $Feed.PayloadDirectory)) {
        Write-Warning "[$($Feed.Name)] Payload directory not found: $($Feed.PayloadDirectory)"
        return
    }
    if (Test-Path $Feed.ArchivePath) { Remove-Item -Force $Feed.ArchivePath }
    Write-Host ""
    Write-Host "[$($Feed.Name)] Refreshing archive:"
    Write-Host "  $($Feed.ArchivePath)"
    Compress-Archive -Path (Join-Path $Feed.PayloadDirectory "*") -DestinationPath $Feed.ArchivePath -Force
}

# ── Initial archive refresh ────────────────────────────────────────────────────

foreach ($feed in $feeds) {
    if ($RefreshArchive -or (Test-ArchiveRefreshNeeded -Feed $feed)) {
        Update-PackageArchive -Feed $feed
    }
}

# ── Start HTTP server ──────────────────────────────────────────────────────────

New-Item -ItemType Directory -Force -Path $logDirectoryPath | Out-Null

$existingListeners = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
foreach ($listener in $existingListeners) {
    try {
        Stop-Process -Id $listener.OwningProcess -Force -ErrorAction Stop
        Write-Host "Stopped existing process on port ${Port} (PID $($listener.OwningProcess))"
    } catch {
        Write-Warning "Could not stop PID $($listener.OwningProcess): $($_.Exception.Message)"
    }
}

$python = Get-Command python -ErrorAction Stop

$publicIp = $null
try { $publicIp = (Invoke-RestMethod "https://api.ipify.org?format=json").ip } catch { }

Write-Host ""
Write-Host "Serving update feed from:"
Write-Host "  $FeedRoot"
Write-Host ""
Write-Host "Game feeds:"
foreach ($feed in $feeds) {
    $manifest = Get-Content $feed.ManifestPath -Raw | ConvertFrom-Json
    Write-Host "  [$($feed.Name)]  version $($manifest.version)"
    Write-Host "    Local:  http://localhost:$Port/$($feed.Name)"
    if ($publicIp) {
        Write-Host "    Public: http://$publicIp`:$Port/$($feed.Name)"
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
    -RedirectStandardError  $stderrLogPath

# ── Watch all payload directories for changes ──────────────────────────────────

# Track pending refreshes per feed: feedName -> DateTime when to refresh
$script:pendingRefreshes = @{}

$eventRegistrations = @()

foreach ($feed in $feeds) {
    if (-not (Test-Path $feed.PayloadDirectory)) { continue }

    $watcher = New-Object System.IO.FileSystemWatcher
    $watcher.Path                  = $feed.Directory
    $watcher.IncludeSubdirectories = $true
    $watcher.EnableRaisingEvents   = $true

    $feedName        = $feed.Name
    $payloadPrefix   = $feed.PayloadDirectory.TrimEnd('\') + '\'

    $queueRefresh = [scriptblock]::Create(@"
        `$eventArgs = `$Event.SourceEventArgs
        `$changedPaths = @()
        if (`$eventArgs -and `$eventArgs.PSObject.Properties.Name -contains 'FullPath')    { `$changedPaths += `$eventArgs.FullPath }
        if (`$eventArgs -and `$eventArgs.PSObject.Properties.Name -contains 'OldFullPath') { `$changedPaths += `$eventArgs.OldFullPath }

        `$payloadPrefix = '$($feed.PayloadDirectory.TrimEnd('\'))\'
        `$match = `$false
        foreach (`$p in `$changedPaths) {
            if ([string]::IsNullOrWhiteSpace(`$p)) { continue }
            `$norm = [System.IO.Path]::GetFullPath(`$p)
            if (`$norm.StartsWith(`$payloadPrefix, [System.StringComparison]::OrdinalIgnoreCase)) { `$match = `$true; break }
        }
        if (`$match) {
            `$script:pendingRefreshes['$feedName'] = (Get-Date).AddSeconds($QuietSeconds)
        }
"@)

    $eventRegistrations += Register-ObjectEvent -InputObject $watcher -EventName Created -Action $queueRefresh
    $eventRegistrations += Register-ObjectEvent -InputObject $watcher -EventName Changed -Action $queueRefresh
    $eventRegistrations += Register-ObjectEvent -InputObject $watcher -EventName Deleted -Action $queueRefresh
    $eventRegistrations += Register-ObjectEvent -InputObject $watcher -EventName Renamed -Action $queueRefresh
}

# ── Main loop ──────────────────────────────────────────────────────────────────

$feedsByName = @{}
foreach ($feed in $feeds) { $feedsByName[$feed.Name] = $feed }

try {
    while (-not $pythonProcess.HasExited) {
        Start-Sleep -Seconds 1
        $pythonProcess.Refresh()

        $toRemove = @()
        foreach ($feedName in @($script:pendingRefreshes.Keys)) {
            if ((Get-Date) -ge $script:pendingRefreshes[$feedName]) {
                $toRemove += $feedName
                try {
                    Update-PackageArchive -Feed $feedsByName[$feedName]
                    Write-Host "[$feedName] Archive refresh complete."
                } catch {
                    Write-Warning "[$feedName] Archive refresh failed: $($_.Exception.Message)"
                    # Retry after another quiet period
                    $script:pendingRefreshes[$feedName] = (Get-Date).AddSeconds($QuietSeconds)
                    $toRemove = $toRemove | Where-Object { $_ -ne $feedName }
                }
            }
        }
        foreach ($key in $toRemove) { $script:pendingRefreshes.Remove($key) }
    }
} finally {
    foreach ($reg in $eventRegistrations) {
        Unregister-Event -SourceIdentifier $reg.Name -ErrorAction SilentlyContinue
    }
    if (-not $pythonProcess.HasExited) {
        Stop-Process -Id $pythonProcess.Id -Force
    }
}

param(
    [string]$SourceBuildDirectory = "C:\UnityBuilds",
    [string]$Version = "",
    [string]$DestinationRoot = "C:\MultiplayerPrototypeBuilds\Latest",
    [string]$LaunchExecutable = "",
    [string]$PayloadDirectoryName = "payload",
    [string]$VersionFileName = "version.txt",
    [string]$PackageArchiveName = ""
)

$ErrorActionPreference = "Stop"

function Resolve-ExistingPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return (Resolve-Path -LiteralPath $Path).Path
}

function Resolve-FullPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

function Resolve-LaunchExecutable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BuildDirectory,

        [Parameter(Mandatory = $false)]
        [string]$RequestedExecutable
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedExecutable)) {
        return $RequestedExecutable.Trim()
    }

    $candidates = Get-ChildItem -LiteralPath $BuildDirectory -Filter *.exe -File |
        Where-Object { $_.Name -notmatch '^UnityCrashHandler' } |
        Sort-Object LastWriteTime -Descending

    if (-not $candidates) {
        throw "Could not auto-detect a launch executable in $BuildDirectory"
    }

    return $candidates[0].Name
}

function Resolve-Version {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BuildDirectory,

        [Parameter(Mandatory = $false)]
        [string]$RequestedVersion,

        [Parameter(Mandatory = $true)]
        [string]$VersionFileName
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedVersion)) {
        return $RequestedVersion.Trim()
    }

    $latestBuildFile = Get-ChildItem -LiteralPath $BuildDirectory -Recurse -File |
        Where-Object { $_.Name -ne $VersionFileName } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $latestBuildFile) {
        throw "Could not determine an automatic version from $BuildDirectory"
    }

    return $latestBuildFile.LastWriteTime.ToString("yyyy.MM.dd.HHmmss")
}

function Copy-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,

        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory
    )

    New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null

    foreach ($item in Get-ChildItem -LiteralPath $SourceDirectory -Force) {
        Copy-Item -LiteralPath $item.FullName -Destination $DestinationDirectory -Recurse -Force
    }
}

function Clear-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Directory
    )

    if (-not (Test-Path -LiteralPath $Directory -PathType Container)) {
        return
    }

    foreach ($item in Get-ChildItem -LiteralPath $Directory -Force) {
        Remove-Item -LiteralPath $item.FullName -Recurse -Force
    }
}

$resolvedSourceBuildDirectory = Resolve-ExistingPath -Path $SourceBuildDirectory
$resolvedDestinationRoot = Resolve-FullPath -Path $DestinationRoot
$resolvedPayloadDirectory = Join-Path $resolvedDestinationRoot $PayloadDirectoryName
$manifestPath = Join-Path $resolvedDestinationRoot "manifest.json"
$resolvedLaunchExecutable = Resolve-LaunchExecutable -BuildDirectory $resolvedSourceBuildDirectory -RequestedExecutable $LaunchExecutable
$resolvedVersion = Resolve-Version -BuildDirectory $resolvedSourceBuildDirectory -RequestedVersion $Version -VersionFileName $VersionFileName
$versionFilePath = Join-Path $resolvedPayloadDirectory $VersionFileName
$launchExecutablePath = Join-Path $resolvedSourceBuildDirectory $resolvedLaunchExecutable
$resolvedPackageArchiveName = if ([string]::IsNullOrWhiteSpace($PackageArchiveName)) { "$PayloadDirectoryName.zip" } else { $PackageArchiveName.Trim() }
$packageArchivePath = Join-Path $resolvedDestinationRoot $resolvedPackageArchiveName
$sourceIsPayloadDirectory = [System.StringComparer]::OrdinalIgnoreCase.Equals(
    $resolvedSourceBuildDirectory.TrimEnd('\'),
    $resolvedPayloadDirectory.TrimEnd('\'))

if (-not (Test-Path -LiteralPath $resolvedSourceBuildDirectory -PathType Container)) {
    throw "Source build directory does not exist: $resolvedSourceBuildDirectory"
}

if (-not (Test-Path -LiteralPath $launchExecutablePath -PathType Leaf)) {
    throw "Launch executable was not found in the source build directory: $launchExecutablePath"
}

if ($resolvedPayloadDirectory.Length -le 3) {
    throw "Resolved payload directory path is not safe to delete: $resolvedPayloadDirectory"
}

New-Item -ItemType Directory -Path $resolvedDestinationRoot -Force | Out-Null

if (-not $sourceIsPayloadDirectory) {
    $stagingRoot = Join-Path $resolvedDestinationRoot ".publish-temp"
    $stagingDirectory = Join-Path $stagingRoot ("payload-" + [Guid]::NewGuid().ToString("N"))
    $backupDirectory = Join-Path $stagingRoot ("payload-backup-" + [Guid]::NewGuid().ToString("N"))

    New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

    try {
        Copy-DirectoryContents -SourceDirectory $resolvedSourceBuildDirectory -DestinationDirectory $stagingDirectory

        $swappedDirectory = $false
        try {
            if (Test-Path -LiteralPath $resolvedPayloadDirectory) {
                Move-Item -LiteralPath $resolvedPayloadDirectory -Destination $backupDirectory
            }

            Move-Item -LiteralPath $stagingDirectory -Destination $resolvedPayloadDirectory
            $swappedDirectory = $true
        }
        catch {
            if (Test-Path -LiteralPath $backupDirectory -PathType Container) {
                Move-Item -LiteralPath $backupDirectory -Destination $resolvedPayloadDirectory
            }

            New-Item -ItemType Directory -Path $resolvedPayloadDirectory -Force | Out-Null
            Clear-DirectoryContents -Directory $resolvedPayloadDirectory
            Copy-DirectoryContents -SourceDirectory $stagingDirectory -DestinationDirectory $resolvedPayloadDirectory
            Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
            $swappedDirectory = $false
        }
    }
    catch {
        if (-not (Test-Path -LiteralPath $resolvedPayloadDirectory) -and (Test-Path -LiteralPath $backupDirectory)) {
            Move-Item -LiteralPath $backupDirectory -Destination $resolvedPayloadDirectory
        }

        if (Test-Path -LiteralPath $stagingDirectory) {
            Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
        }

        throw
    }
    finally {
        if (Test-Path -LiteralPath $backupDirectory) {
            Remove-Item -LiteralPath $backupDirectory -Recurse -Force
        }

        if (Test-Path -LiteralPath $stagingRoot) {
            $remainingEntries = Get-ChildItem -LiteralPath $stagingRoot -Force -ErrorAction SilentlyContinue
            if (-not $remainingEntries) {
                Remove-Item -LiteralPath $stagingRoot -Force
            }
        }
    }
}
elseif (-not (Test-Path -LiteralPath $resolvedPayloadDirectory -PathType Container)) {
    throw "Payload directory does not exist: $resolvedPayloadDirectory"
}

Set-Content -LiteralPath $versionFilePath -Value $resolvedVersion

$manifest = [ordered]@{
    version = $resolvedVersion
    packageDirectory = $PayloadDirectoryName
    packageArchive = $resolvedPackageArchiveName
    launchExecutable = $resolvedLaunchExecutable
}

$manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath

if (Test-Path -LiteralPath $packageArchivePath) {
    Remove-Item -LiteralPath $packageArchivePath -Force
}

Compress-Archive -Path (Join-Path $resolvedPayloadDirectory "*") -DestinationPath $packageArchivePath -Force

Write-Host "Published build version $resolvedVersion to $resolvedDestinationRoot"
Write-Host "Source build: $resolvedSourceBuildDirectory"
Write-Host "Launch executable: $resolvedLaunchExecutable"
Write-Host "Manifest: $manifestPath"
Write-Host "Payload: $resolvedPayloadDirectory"
Write-Host "Package archive: $packageArchivePath"
if ($sourceIsPayloadDirectory) {
    Write-Host "Copy step: skipped because the source build is already the payload directory"
}

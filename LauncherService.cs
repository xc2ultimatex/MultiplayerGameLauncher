using System.Diagnostics;
using System.Text.Json;

namespace MultiplayerLauncher;

internal static class LauncherService
{
    public static async Task<LauncherStatus> CheckForUpdatesAsync(string launcherRoot, LauncherSettings settings)
    {
        string sourceDirectory = settings.UpdateSourceDirectory.Trim();
        string localGameDirectory = Path.Combine(launcherRoot, settings.GameDirectoryName);
        string localVersionPath = Path.Combine(localGameDirectory, settings.LocalVersionFileName);
        string? defaultLaunchPath = ResolveLaunchPath(localGameDirectory, settings.GameExecutableRelativePath);

        HideDirectoryIfPresent(localGameDirectory);

        if (string.IsNullOrWhiteSpace(sourceDirectory) || sourceDirectory.Contains("DEV-MACHINE", StringComparison.OrdinalIgnoreCase))
        {
            return new LauncherStatus
            {
                IsConfigured = false,
                SourceDirectory = sourceDirectory,
                LocalVersion = await ReadVersionIfPresentAsync(localVersionPath),
                CanLaunch = defaultLaunchPath is not null,
                LaunchPath = defaultLaunchPath
            };
        }

        string manifestPath = Path.Combine(sourceDirectory, settings.ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return new LauncherStatus
            {
                IsConfigured = true,
                RemoteManifestAvailable = false,
                SourceDirectory = sourceDirectory,
                LocalVersion = await ReadVersionIfPresentAsync(localVersionPath),
                CanLaunch = defaultLaunchPath is not null,
                LaunchPath = defaultLaunchPath
            };
        }

        UpdateManifest manifest = await LoadManifestAsync(manifestPath);
        string launchExecutableRelativePath = string.IsNullOrWhiteSpace(manifest.LaunchExecutable)
            ? settings.GameExecutableRelativePath
            : manifest.LaunchExecutable.Trim();
        string? launchPath = ResolveLaunchPath(localGameDirectory, launchExecutableRelativePath);
        string remoteVersion = manifest.Version.Trim();
        string localVersion = await ReadVersionIfPresentAsync(localVersionPath) ?? string.Empty;
        bool hasInstalledBuild = launchPath is not null;
        bool updateAvailable = !hasInstalledBuild || !string.Equals(localVersion, remoteVersion, StringComparison.OrdinalIgnoreCase);

        return new LauncherStatus
        {
            IsConfigured = true,
            RemoteManifestAvailable = true,
            UpdateAvailable = updateAvailable,
            CanLaunch = hasInstalledBuild,
            CanUpdateOrInstall = true,
            SourceDirectory = sourceDirectory,
            LocalVersion = string.IsNullOrWhiteSpace(localVersion) ? null : localVersion,
            RemoteVersion = remoteVersion,
            LaunchPath = hasInstalledBuild ? launchPath : null
        };
    }

    public static async Task<UpdateResult> UpdateAsync(string launcherRoot, LauncherSettings settings)
    {
        LauncherStatus status = await CheckForUpdatesAsync(launcherRoot, settings);
        if (!status.IsConfigured)
            throw new InvalidOperationException("Launcher settings are not configured.");

        if (!status.RemoteManifestAvailable)
            throw new FileNotFoundException("Remote manifest not found.", Path.Combine(settings.UpdateSourceDirectory, settings.ManifestFileName));

        string manifestPath = Path.Combine(settings.UpdateSourceDirectory, settings.ManifestFileName);
        UpdateManifest manifest = await LoadManifestAsync(manifestPath);

        string packageDirectoryName = string.IsNullOrWhiteSpace(manifest.PackageDirectory)
            ? settings.PackageDirectoryName
            : manifest.PackageDirectory.Trim();

        string launchExecutableRelativePath = string.IsNullOrWhiteSpace(manifest.LaunchExecutable)
            ? settings.GameExecutableRelativePath
            : manifest.LaunchExecutable.Trim();

        string remotePayloadDirectory = Path.Combine(settings.UpdateSourceDirectory, packageDirectoryName);
        if (!Directory.Exists(remotePayloadDirectory))
            throw new DirectoryNotFoundException($"Remote payload directory was not found: {remotePayloadDirectory}");

        string localGameDirectory = Path.Combine(launcherRoot, settings.GameDirectoryName);
        string localVersionPath = Path.Combine(localGameDirectory, settings.LocalVersionFileName);
        string? installedLaunchPath = ResolveLaunchPath(localGameDirectory, launchExecutableRelativePath);

        if (IsGameProcessRunning(installedLaunchPath))
            throw new InvalidOperationException("Close the game before updating.");

        bool updateRequired = installedLaunchPath is null ||
                              !string.Equals(await ReadVersionIfPresentAsync(localVersionPath), manifest.Version, StringComparison.OrdinalIgnoreCase);

        if (updateRequired)
        {
            string stagingRoot = Path.Combine(launcherRoot, ".launcher-temp");
            string stagingDirectory = Path.Combine(stagingRoot, $"staging-{Guid.NewGuid():N}");
            string backupDirectory = Path.Combine(stagingRoot, $"backup-{Guid.NewGuid():N}");

            Directory.CreateDirectory(stagingRoot);
            HideDirectoryIfPresent(stagingRoot);
            CopyDirectory(remotePayloadDirectory, stagingDirectory);

            string stagedVersionFile = Path.Combine(stagingDirectory, settings.LocalVersionFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(stagedVersionFile)!);
            await File.WriteAllTextAsync(stagedVersionFile, manifest.Version.Trim());

            try
            {
                if (Directory.Exists(localGameDirectory))
                {
                    Directory.Move(localGameDirectory, backupDirectory);
                }

                Directory.Move(stagingDirectory, localGameDirectory);
                HideDirectoryIfPresent(localGameDirectory);

                if (Directory.Exists(backupDirectory))
                {
                    Directory.Delete(backupDirectory, true);
                }
            }
            catch
            {
                if (!Directory.Exists(localGameDirectory) && Directory.Exists(backupDirectory))
                {
                    Directory.Move(backupDirectory, localGameDirectory);
                }

                if (Directory.Exists(stagingDirectory))
                {
                    Directory.Delete(stagingDirectory, true);
                }

                throw;
            }
        }

        string? finalLaunchPath = ResolveLaunchPath(localGameDirectory, launchExecutableRelativePath);
        if (!File.Exists(finalLaunchPath))
            throw new FileNotFoundException("The installed game executable could not be found after update.", finalLaunchPath);

        LaunchGame(finalLaunchPath, launcherRoot);

        return new UpdateResult
        {
            Updated = updateRequired,
            Launched = true,
            Message = updateRequired
                ? $"Updated to {manifest.Version.Trim()} and launched the game."
                : "Installed build is already current. Launched the game."
        };
    }

    public static void LaunchGame(string launchPath, string launcherRoot)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = launchPath,
            WorkingDirectory = Path.GetDirectoryName(launchPath) ?? launcherRoot,
            UseShellExecute = true
        });
    }

    private static async Task<UpdateManifest> LoadManifestAsync(string manifestPath)
    {
        string json = await File.ReadAllTextAsync(manifestPath);
        UpdateManifest? manifest = JsonSerializer.Deserialize<UpdateManifest>(json, JsonOptions.Default);
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
            throw new InvalidDataException("Manifest is missing a valid version.");

        return manifest;
    }

    private static string? ResolveLaunchPath(string gameDirectory, string configuredRelativePath)
    {
        if (!string.IsNullOrWhiteSpace(configuredRelativePath))
        {
            string configuredLaunchPath = Path.Combine(gameDirectory, configuredRelativePath.Trim());
            if (File.Exists(configuredLaunchPath))
                return configuredLaunchPath;
        }

        return AutoDetectLaunchPath(gameDirectory);
    }

    private static string? AutoDetectLaunchPath(string gameDirectory)
    {
        if (!Directory.Exists(gameDirectory))
            return null;

        string[] candidates = Directory.GetFiles(gameDirectory, "*.exe", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileName(path).StartsWith("UnityCrashHandler", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static async Task<string?> ReadVersionIfPresentAsync(string versionPath)
    {
        if (!File.Exists(versionPath))
            return null;

        string version = await File.ReadAllTextAsync(versionPath);
        version = version.Trim();
        return string.IsNullOrWhiteSpace(version) ? null : version;
    }

    private static bool IsGameProcessRunning(string? launchPath)
    {
        if (string.IsNullOrWhiteSpace(launchPath))
            return false;

        string processName = Path.GetFileNameWithoutExtension(launchPath);
        if (string.IsNullOrWhiteSpace(processName))
            return false;

        foreach (Process process in Process.GetProcessesByName(processName))
        {
            try
            {
                if (!process.HasExited)
                    return true;
            }
            catch
            {
                return true;
            }
        }

        return false;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (string filePath in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, filePath);
            string destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(filePath, destinationPath, true);
        }
    }

    private static void HideDirectoryIfPresent(string path)
    {
        if (!Directory.Exists(path))
            return;

        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.Hidden) == 0)
        {
            File.SetAttributes(path, attributes | FileAttributes.Hidden);
        }
    }
}

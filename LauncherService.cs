using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text.Json;

namespace MultiplayerLauncher;

internal static class LauncherService
{
    private static readonly HttpClient HttpClient = new();

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
                LocalGameDirectory = localGameDirectory,
                LocalVersion = await ReadVersionIfPresentAsync(localVersionPath),
                CanLaunch = defaultLaunchPath is not null,
                LaunchPath = defaultLaunchPath
            };
        }

        UpdateManifest? manifest = await TryLoadManifestAsync(sourceDirectory, settings.ManifestFileName);
        if (manifest is null)
        {
            return new LauncherStatus
            {
                IsConfigured = true,
                RemoteManifestAvailable = false,
                SourceDirectory = sourceDirectory,
                LocalGameDirectory = localGameDirectory,
                LocalVersion = await ReadVersionIfPresentAsync(localVersionPath),
                CanLaunch = defaultLaunchPath is not null,
                LaunchPath = defaultLaunchPath
            };
        }

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
            LocalGameDirectory = localGameDirectory,
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
            throw new FileNotFoundException("Remote manifest not found.", BuildSourceLocation(settings.UpdateSourceDirectory, settings.ManifestFileName));

        UpdateManifest? manifest = await TryLoadManifestAsync(settings.UpdateSourceDirectory, settings.ManifestFileName);
        if (manifest is null)
            throw new FileNotFoundException("Remote manifest not found.", BuildSourceLocation(settings.UpdateSourceDirectory, settings.ManifestFileName));

        string packageDirectoryName = string.IsNullOrWhiteSpace(manifest.PackageDirectory)
            ? settings.PackageDirectoryName
            : manifest.PackageDirectory.Trim();

        string launchExecutableRelativePath = string.IsNullOrWhiteSpace(manifest.LaunchExecutable)
            ? settings.GameExecutableRelativePath
            : manifest.LaunchExecutable.Trim();

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

            if (IsHttpSource(settings.UpdateSourceDirectory))
            {
                await DownloadAndExtractPackageAsync(
                    settings.UpdateSourceDirectory,
                    manifest,
                    packageDirectoryName,
                    launchExecutableRelativePath,
                    settings.LocalVersionFileName,
                    stagingRoot,
                    stagingDirectory);
            }
            else
            {
                string remotePayloadDirectory = Path.Combine(settings.UpdateSourceDirectory, packageDirectoryName);
                if (!Directory.Exists(remotePayloadDirectory))
                    throw new DirectoryNotFoundException($"Remote payload directory was not found: {remotePayloadDirectory}");

                CopyDirectory(remotePayloadDirectory, stagingDirectory);
            }

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

    private static async Task<UpdateManifest?> TryLoadManifestAsync(string sourceDirectory, string manifestFileName)
    {
        if (IsHttpSource(sourceDirectory))
        {
            try
            {
                Uri manifestUri = BuildSourceUri(sourceDirectory, manifestFileName);
                return await LoadManifestAsync(manifestUri);
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (TaskCanceledException)
            {
                return null;
            }
        }

        string manifestPath = Path.Combine(sourceDirectory, manifestFileName);
        if (!File.Exists(manifestPath))
            return null;

        return await LoadManifestAsync(manifestPath);
    }

    private static async Task<UpdateManifest> LoadManifestAsync(string manifestPath)
    {
        string json = await File.ReadAllTextAsync(manifestPath);
        return ParseManifest(json);
    }

    private static async Task<UpdateManifest> LoadManifestAsync(Uri manifestUri)
    {
        using HttpResponseMessage response = await HttpClient.GetAsync(manifestUri, HttpCompletionOption.ResponseHeadersRead);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new HttpRequestException($"Manifest not found at {manifestUri}.", null, response.StatusCode);

        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync();
        return ParseManifest(json);
    }

    private static UpdateManifest ParseManifest(string json)
    {
        UpdateManifest? manifest = JsonSerializer.Deserialize<UpdateManifest>(json, JsonOptions.Default);
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
            throw new InvalidDataException("Manifest is missing a valid version.");

        return manifest;
    }

    private static async Task DownloadAndExtractPackageAsync(
        string sourceDirectory,
        UpdateManifest manifest,
        string packageDirectoryName,
        string launchExecutableRelativePath,
        string localVersionFileName,
        string stagingRoot,
        string stagingDirectory)
    {
        string archiveName = string.IsNullOrWhiteSpace(manifest.PackageArchive)
            ? $"{packageDirectoryName.TrimEnd('/', '\\')}.zip"
            : manifest.PackageArchive.Trim();

        Uri archiveUri = BuildSourceUri(sourceDirectory, archiveName);
        string archivePath = Path.Combine(stagingRoot, $"package-{Guid.NewGuid():N}.zip");
        string extractDirectory = Path.Combine(stagingRoot, $"extract-{Guid.NewGuid():N}");

        try
        {
            using (HttpResponseMessage response = await HttpClient.GetAsync(archiveUri, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                await using Stream remoteStream = await response.Content.ReadAsStreamAsync();
                await using FileStream archiveStream = File.Create(archivePath);
                await remoteStream.CopyToAsync(archiveStream);
            }

            Directory.CreateDirectory(extractDirectory);
            ZipFile.ExtractToDirectory(archivePath, extractDirectory);

            string payloadRoot = ResolveExtractedPayloadRoot(extractDirectory, launchExecutableRelativePath, localVersionFileName);
            CopyDirectory(payloadRoot, stagingDirectory);
        }
        finally
        {
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            if (Directory.Exists(extractDirectory))
            {
                Directory.Delete(extractDirectory, true);
            }
        }
    }

    private static string ResolveExtractedPayloadRoot(string extractDirectory, string launchExecutableRelativePath, string localVersionFileName)
    {
        if (LooksLikePayloadRoot(extractDirectory, launchExecutableRelativePath, localVersionFileName))
            return extractDirectory;

        string[] childDirectories = Directory.GetDirectories(extractDirectory, "*", SearchOption.TopDirectoryOnly);
        if (childDirectories.Length == 1 && LooksLikePayloadRoot(childDirectories[0], launchExecutableRelativePath, localVersionFileName))
            return childDirectories[0];

        return extractDirectory;
    }

    private static bool LooksLikePayloadRoot(string directory, string launchExecutableRelativePath, string localVersionFileName)
    {
        if (!string.IsNullOrWhiteSpace(launchExecutableRelativePath) &&
            File.Exists(Path.Combine(directory, launchExecutableRelativePath.Trim())))
        {
            return true;
        }

        if (File.Exists(Path.Combine(directory, localVersionFileName)))
            return true;

        return Directory.GetFiles(directory, "*.exe", SearchOption.TopDirectoryOnly)
            .Any(path => !Path.GetFileName(path).StartsWith("UnityCrashHandler", StringComparison.OrdinalIgnoreCase));
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

    private static bool IsHttpSource(string sourceDirectory)
    {
        return Uri.TryCreate(sourceDirectory, UriKind.Absolute, out Uri? uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static Uri BuildSourceUri(string sourceDirectory, string relativeOrAbsolutePath)
    {
        if (Uri.TryCreate(relativeOrAbsolutePath, UriKind.Absolute, out Uri? absoluteUri) &&
            (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
        {
            return absoluteUri;
        }

        if (!Uri.TryCreate(sourceDirectory.TrimEnd('/') + "/", UriKind.Absolute, out Uri? baseUri))
            throw new InvalidOperationException($"Invalid HTTP update source: {sourceDirectory}");

        return new Uri(baseUri, relativeOrAbsolutePath.TrimStart('/'));
    }

    private static string BuildSourceLocation(string sourceDirectory, string manifestFileName)
    {
        if (IsHttpSource(sourceDirectory))
            return BuildSourceUri(sourceDirectory, manifestFileName).ToString();

        return Path.Combine(sourceDirectory, manifestFileName);
    }
}

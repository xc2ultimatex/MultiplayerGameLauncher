using System.Text.Json;

namespace MultiplayerLauncher;

public partial class Form1 : Form
{
    private readonly string launcherRoot;
    private readonly string settingsPath;
    private LauncherSettings settings = LauncherSettings.Default;
    private bool isBusy;
    private bool startupProcessed;

    public Form1()
    {
        launcherRoot = AppContext.BaseDirectory;
        settingsPath = Path.Combine(launcherRoot, "launcher.settings.json");

        InitializeComponent();
        Shown += async (_, _) => await LoadSettingsAndRefreshAsync();
    }

    private async Task LoadSettingsAndRefreshAsync()
    {
        try
        {
            await EnsureSettingsFileExistsAsync();
            settings = await LoadSettingsAsync();
            LauncherStatus? launcherStatus = await RefreshStatusAsync();

            if (startupProcessed || launcherStatus is null)
                return;

            startupProcessed = true;

            if (launcherStatus is { IsConfigured: true, RemoteManifestAvailable: true, UpdateAvailable: true, CanUpdateOrInstall: true })
            {
                await AutoUpdateAsync();
                return;
            }

            if (launcherStatus is { IsConfigured: true, RemoteManifestAvailable: true, UpdateAvailable: false, CanLaunch: true } &&
                !string.IsNullOrWhiteSpace(launcherStatus.LaunchPath))
            {
                LauncherService.LaunchGame(launcherStatus.LaunchPath, launcherRoot);
                Close();
                return;
            }

            if (launcherStatus is { CanLaunch: true, LaunchPath: not null })
            {
                LauncherService.LaunchGame(launcherStatus.LaunchPath, launcherRoot);
                Close();
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to load launcher settings: {ex.Message}", isError: true);
        }
    }

    private async Task EnsureSettingsFileExistsAsync()
    {
        if (File.Exists(settingsPath))
        {
            HideFileIfPresent(settingsPath);
            return;
        }

        string json = JsonSerializer.Serialize(LauncherSettings.Default, JsonOptions.WriteIndented);
        await File.WriteAllTextAsync(settingsPath, json);
        HideFileIfPresent(settingsPath);
    }

    private async Task<LauncherSettings> LoadSettingsAsync()
    {
        string json = await File.ReadAllTextAsync(settingsPath);
        LauncherSettings? loadedSettings = JsonSerializer.Deserialize<LauncherSettings>(json, JsonOptions.Default);
        return loadedSettings ?? LauncherSettings.Default;
    }

    private async Task<LauncherStatus?> RefreshStatusAsync()
    {
        if (isBusy)
            return null;

        SetBusy(true, "Checking for updates...");

        try
        {
            LauncherStatus launcherStatus = await LauncherService.CheckForUpdatesAsync(launcherRoot, settings);
            UpdateStatusView(launcherStatus);
            UpdateDetailsView(launcherStatus);
            return launcherStatus;
        }
        catch (Exception ex)
        {
            SetStatus($"Update check failed: {ex.Message}", isError: true);
            UpdateDetailsView(null);
            return null;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task AutoUpdateAsync()
    {
        if (isBusy)
            return;

        SetBusy(true, "Outdated build detected. Updating automatically...");

        try
        {
            UpdateResult result = await LauncherService.UpdateAsync(launcherRoot, settings);
            SetStatus(result.Message, isError: false);

            if (result.Launched)
            {
                Close();
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Automatic update failed: {ex.Message}", isError: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void UpdateStatusView(LauncherStatus launcherStatus)
    {
        retryButton.Enabled = true;
        launchInstalledButton.Enabled = launcherStatus.CanLaunch;

        if (!launcherStatus.IsConfigured)
        {
            SetStatus("Launcher configuration is missing or invalid.", isError: true);
            return;
        }

        if (!launcherStatus.RemoteManifestAvailable)
        {
            SetStatus(
                launcherStatus.CanLaunch
                    ? "Update feed unavailable. Launching installed build."
                    : $"Could not reach the update feed at {launcherStatus.SourceDirectory}.",
                isError: !launcherStatus.CanLaunch);
            return;
        }

        if (launcherStatus.UpdateAvailable)
        {
            SetStatus("Updating to the latest build...", isError: false);
            return;
        }

        if (launcherStatus.CanLaunch)
        {
            SetStatus("Build is current. Launching game...", isError: false);
            return;
        }

        SetStatus("No installed build was found.", isError: true);
    }

    private void UpdateDetailsView(LauncherStatus? launcherStatus)
    {
        if (launcherStatus is null)
        {
            detailsValueLabel.Text = BuildDetailsText(
                sourceDirectory: settings.UpdateSourceDirectory,
                localGameDirectory: Path.Combine(launcherRoot, settings.GameDirectoryName),
                localVersion: null,
                remoteVersion: null);
            return;
        }

        detailsValueLabel.Text = BuildDetailsText(
            launcherStatus.SourceDirectory,
            launcherStatus.LocalGameDirectory,
            launcherStatus.LocalVersion,
            launcherStatus.RemoteVersion);
    }

    private static string BuildDetailsText(
        string sourceDirectory,
        string localGameDirectory,
        string? localVersion,
        string? remoteVersion)
    {
        return string.Join(Environment.NewLine, new[]
        {
            $"Source: {DisplayValue(sourceDirectory)}",
            $"Game:   {DisplayValue(localGameDirectory)}",
            $"Local:  {DisplayValue(localVersion)}",
            $"Remote: {DisplayValue(remoteVersion)}"
        });
    }

    private static string DisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim();
    }

    private void SetStatus(string message, bool isError)
    {
        statusValueLabel.Text = message;
        statusValueLabel.ForeColor = isError ? Color.FromArgb(190, 58, 58) : Color.FromArgb(30, 78, 30);
    }

    private void SetBusy(bool busy, string? message = null)
    {
        isBusy = busy;
        retryButton.Enabled = !busy;
        launchInstalledButton.Enabled = !busy;

        if (!string.IsNullOrWhiteSpace(message))
        {
            statusValueLabel.Text = message;
            statusValueLabel.ForeColor = Color.FromArgb(30, 30, 30);
        }
    }

    private async void retryButton_Click(object sender, EventArgs e)
    {
        startupProcessed = false;
        await LoadSettingsAndRefreshAsync();
    }

    private async void launchInstalledButton_Click(object sender, EventArgs e)
    {
        if (isBusy)
            return;

        try
        {
            LauncherStatus launcherStatus = await LauncherService.CheckForUpdatesAsync(launcherRoot, settings);
            if (!launcherStatus.CanLaunch || string.IsNullOrWhiteSpace(launcherStatus.LaunchPath))
            {
                SetStatus("No installed build is available to launch.", isError: true);
                return;
            }

            LauncherService.LaunchGame(launcherStatus.LaunchPath, launcherRoot);
            Close();
        }
        catch (Exception ex)
        {
            SetStatus($"Launch failed: {ex.Message}", isError: true);
        }
    }

    private void closeButton_Click(object sender, EventArgs e)
    {
        Close();
    }

    private static void HideFileIfPresent(string path)
    {
        if (!File.Exists(path))
            return;

        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.Hidden) == 0)
        {
            File.SetAttributes(path, attributes | FileAttributes.Hidden);
        }
    }
}

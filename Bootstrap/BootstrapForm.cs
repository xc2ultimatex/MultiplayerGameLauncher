using System.Text.Json;

namespace Bootstrap;

public partial class BootstrapForm : Form
{
    private readonly string _root;
    private readonly string _settingsPath;

    public BootstrapForm()
    {
        _root         = AppContext.BaseDirectory;
        _settingsPath = Path.Combine(_root, "bootstrap.settings.json");
        InitializeComponent();
        Shown += async (_, _) => await RunAsync();
    }

    // ── Main flow ──────────────────────────────────────────────────────────────

    private async Task RunAsync()
    {
        BootstrapSettings settings = LoadSettings();
        string launcherPath = Path.Combine(_root, settings.LauncherExeName);
        string versionPath  = Path.Combine(_root, "launcher-version.txt");

        try
        {
            SetStatus("Checking for updates...");

            string? localVersion = await ReadVersionAsync(versionPath);
            GitHubRelease? release = await UpdaterService.FetchLatestReleaseAsync(settings.GitHubRepo);

            if (release != null &&
                !string.Equals(release.TagName, localVersion, StringComparison.OrdinalIgnoreCase))
            {
                GitHubAsset? asset = release.Assets.FirstOrDefault(a =>
                    string.Equals(a.Name, settings.AssetName, StringComparison.OrdinalIgnoreCase));

                if (asset != null)
                {
                    SetStatus($"Downloading {release.TagName}...");
                    ShowProgress(true);

                    await UpdaterService.DownloadAndReplaceAsync(
                        asset.BrowserDownloadUrl,
                        launcherPath,
                        pct =>
                        {
                            progressBar.Value = pct;
                            statusLabel.Text  = $"Downloading {release.TagName}...  {pct}%";
                            Application.DoEvents();
                        });

                    await File.WriteAllTextAsync(versionPath, release.TagName);
                    ShowProgress(false);
                    SetStatus("Update installed. Launching...");
                    await Task.Delay(400);
                }
            }
            else
            {
                SetStatus("Launching...");
                await Task.Delay(200);
            }
        }
        catch (Exception ex)
        {
            // Update failed — launch existing version if present, warn if not
            if (!File.Exists(launcherPath))
            {
                MessageBox.Show(
                    $"Could not download the launcher and no local copy was found.\n\n{ex.Message}",
                    "Download Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Close();
                return;
            }

            SetStatus("Update check failed. Launching existing version...");
            await Task.Delay(800);
        }

        Launch(launcherPath);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void Launch(string launcherPath)
    {
        if (!File.Exists(launcherPath))
        {
            MessageBox.Show(
                "Launcher not found. Please re-download from GitHub.",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName         = launcherPath,
            WorkingDirectory = _root,
            UseShellExecute  = true
        });

        Close();
    }

    private void SetStatus(string text)
    {
        statusLabel.Text = text;
        Application.DoEvents();
    }

    private void ShowProgress(bool visible)
    {
        progressBar.Visible = visible;
        Application.DoEvents();
    }

    private BootstrapSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<BootstrapSettings>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? BootstrapSettings.Default;
            }
        }
        catch { }
        return BootstrapSettings.Default;
    }

    private static async Task<string?> ReadVersionAsync(string path)
    {
        if (!File.Exists(path)) return null;
        string v = (await File.ReadAllTextAsync(path)).Trim();
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }
}

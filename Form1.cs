using System.Text.Json;

namespace MultiplayerLauncher;

public partial class Form1 : Form
{
    private readonly string _launcherRoot;
    private readonly string _configPath;
    private LauncherConfig _config = LauncherConfig.Default;
    private readonly List<GameEntry> _games = new();
    private GameEntry? _selected;

    // ── Colors ─────────────────────────────────────────────────────────────────
    private static readonly Color BgDark      = Color.FromArgb(18,  18,  28);
    private static readonly Color BgSidebar   = Color.FromArgb(24,  24,  38);
    private static readonly Color BgSelected  = Color.FromArgb(42,  42,  66);
    private static readonly Color BgRight     = Color.FromArgb(22,  22,  34);
    private static readonly Color TextPrimary = Color.FromArgb(230, 225, 240);
    private static readonly Color TextMuted   = Color.FromArgb(140, 135, 160);
    private static readonly Color ColGreen    = Color.FromArgb(80,  200, 110);
    private static readonly Color ColYellow   = Color.FromArgb(210, 170,  55);
    private static readonly Color ColRed      = Color.FromArgb(210,  75,  75);
    private static readonly Color ColGray     = Color.FromArgb(120, 115, 140);
    private static readonly Color ColLaunch   = Color.FromArgb(55,  145,  70);

    // ── Per-game state ─────────────────────────────────────────────────────────
    private sealed class GameEntry
    {
        public required LauncherSettings Settings;
        public LauncherStatus?           Status;
        public bool                      IsBusy;
        public string                    StatusText  = "Checking...";
        public Color                     StatusColor = Color.FromArgb(120, 115, 140);

        // Sidebar list item controls
        public Panel  ListItem   = null!;
        public Label  NameLabel  = null!;
        public Label  StatusLabel = null!;
        public Label  VersionLabel = null!;
    }

    public Form1()
    {
        _launcherRoot = AppContext.BaseDirectory;
        _configPath   = Path.Combine(_launcherRoot, "launcher.settings.json");
        InitializeComponent();
        Shown += async (_, _) => await StartupAsync();
    }

    // ── Startup ────────────────────────────────────────────────────────────────

    private async Task StartupAsync()
    {
        await LoadConfigAsync();
        BuildGameListUI();

        if (_games.Count > 0)
            SelectGame(_games[0]);

        // Check + auto-update all games in parallel — no auto-launch
        await Task.WhenAll(_games.Select(AutoUpdateGameAsync));
    }

    private async Task LoadConfigAsync()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                string json = JsonSerializer.Serialize(LauncherConfig.Default, JsonOptions.WriteIndented);
                await File.WriteAllTextAsync(_configPath, json);
                HideFile(_configPath);
            }

            string text = await File.ReadAllTextAsync(_configPath);
            LauncherConfig? cfg = JsonSerializer.Deserialize<LauncherConfig>(text, JsonOptions.Default);
            if (cfg?.Games is { Count: > 0 })
            {
                _config = cfg;
            }
            else
            {
                // Migrate old single-game format
                LauncherSettings? legacy = JsonSerializer.Deserialize<LauncherSettings>(text, JsonOptions.Default);
                if (legacy != null)
                    _config = new LauncherConfig { Games = new List<LauncherSettings> { legacy } };
            }

            HideFile(_configPath);
        }
        catch { /* use default config */ }
    }

    // ── Sidebar list ───────────────────────────────────────────────────────────

    private void BuildGameListUI()
    {
        gameListPanel.Controls.Clear();
        _games.Clear();

        int y = 0;
        foreach (LauncherSettings settings in _config.Games)
        {
            var entry = new GameEntry { Settings = settings };
            BuildListItem(entry, y);
            _games.Add(entry);
            gameListPanel.Controls.Add(entry.ListItem);
            y += entry.ListItem.Height;
        }
    }

    private void BuildListItem(GameEntry entry, int top)
    {
        var item = new Panel
        {
            Location  = new Point(0, top),
            Size      = new Size(gameListPanel.Width, 78),
            BackColor = BgSidebar,
            Cursor    = Cursors.Hand
        };

        var name = new Label
        {
            Text      = entry.Settings.Name,
            Font      = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location  = new Point(16, 14),
            Size      = new Size(item.Width - 20, 22),
            AutoEllipsis = true
        };

        var status = new Label
        {
            Text      = "Checking...",
            Font      = new Font("Segoe UI", 8.5f),
            ForeColor = ColGray,
            Location  = new Point(16, 38),
            Size      = new Size(item.Width - 20, 18)
        };

        var version = new Label
        {
            Text      = "",
            Font      = new Font("Consolas", 8f),
            ForeColor = TextMuted,
            Location  = new Point(16, 56),
            Size      = new Size(item.Width - 20, 16)
        };

        item.Controls.AddRange(new Control[] { name, status, version });

        // Make all children pass clicks through to the item
        foreach (Control c in item.Controls)
            c.Click += (_, _) => SelectGame(entry);

        item.Click += (_, _) => SelectGame(entry);

        entry.ListItem    = item;
        entry.NameLabel   = name;
        entry.StatusLabel = status;
        entry.VersionLabel = version;
    }

    // ── Game selection ─────────────────────────────────────────────────────────

    private void SelectGame(GameEntry entry)
    {
        _selected = entry;

        // Highlight selected item
        foreach (var g in _games)
            g.ListItem.BackColor = g == entry ? BgSelected : BgSidebar;

        RefreshRightPanel();
    }

    private void RefreshRightPanel()
    {
        if (_selected == null) return;

        rightTitleLabel.Text = _selected.Settings.Name;

        if (_selected.IsBusy)
        {
            rightStatusLabel.Text      = _selected.StatusText;
            rightStatusLabel.ForeColor = ColYellow;
            launchButton.Enabled       = false;
            launchButton.Text          = "Launch Game";
        }
        else if (_selected.Status == null)
        {
            rightStatusLabel.Text      = "Checking for updates...";
            rightStatusLabel.ForeColor = ColGray;
            launchButton.Enabled       = false;
        }
        else if (!_selected.Status.IsConfigured)
        {
            rightStatusLabel.Text      = "Not configured.";
            rightStatusLabel.ForeColor = ColRed;
            launchButton.Enabled       = false;
        }
        else if (!_selected.Status.CanLaunch)
        {
            rightStatusLabel.Text      = "Not installed.";
            rightStatusLabel.ForeColor = ColRed;
            launchButton.Enabled       = false;
        }
        else if (_selected.Status.UpdateAvailable)
        {
            rightStatusLabel.Text      = $"Update available  ({_selected.Status.LocalVersion} -> {_selected.Status.RemoteVersion})";
            rightStatusLabel.ForeColor = ColYellow;
            launchButton.Enabled       = true;
            launchButton.Text          = "Launch Game";
        }
        else
        {
            rightStatusLabel.Text      = $"Up to date  ({_selected.Status.LocalVersion})";
            rightStatusLabel.ForeColor = ColGreen;
            launchButton.Enabled       = true;
            launchButton.Text          = "Launch Game";
        }

        // Patch notes: prefer manifest notes, fall back to settings placeholder
        string notes = _selected.Status?.PatchNotes
            ?? _selected.Settings.PatchNotes
            ?? "- No patch notes available.";
        patchNotesBox.Text = notes;
    }

    // ── Auto-update ────────────────────────────────────────────────────────────

    private async Task AutoUpdateGameAsync(GameEntry entry)
    {
        SetBusy(entry, "Checking for updates...");

        try
        {
            LauncherStatus status = await LauncherService.CheckForUpdatesAsync(_launcherRoot, entry.Settings);
            entry.Status = status;

            if (status is { IsConfigured: true, RemoteManifestAvailable: true, UpdateAvailable: true, CanUpdateOrInstall: true })
            {
                SetBusy(entry, status.CanLaunch ? "Updating..." : "Installing...");
                await LauncherService.UpdateAsync(_launcherRoot, entry.Settings, launch: false);

                // Re-check so we have fresh version info
                entry.Status = await LauncherService.CheckForUpdatesAsync(_launcherRoot, entry.Settings);
            }
        }
        catch (Exception ex)
        {
            entry.StatusText  = $"Error: {ex.Message}";
            entry.StatusColor = ColRed;
        }
        finally
        {
            entry.IsBusy = false;
            UpdateListItem(entry);
            if (_selected == entry)
                RefreshRightPanel();
        }
    }

    private void SetBusy(GameEntry entry, string text)
    {
        entry.IsBusy       = true;
        entry.StatusText   = text;
        entry.StatusColor  = ColYellow;
        UpdateListItem(entry);
        if (_selected == entry)
            RefreshRightPanel();
    }

    private void UpdateListItem(GameEntry entry)
    {
        if (entry.IsBusy)
        {
            entry.StatusLabel.Text      = entry.StatusText;
            entry.StatusLabel.ForeColor = entry.StatusColor;
            entry.VersionLabel.Text     = "";
            return;
        }

        var s = entry.Status;
        if (s == null)                   { entry.StatusLabel.Text = "Unknown";      entry.StatusLabel.ForeColor = ColGray;   }
        else if (!s.IsConfigured)        { entry.StatusLabel.Text = "Not configured"; entry.StatusLabel.ForeColor = ColRed;  }
        else if (!s.CanLaunch)           { entry.StatusLabel.Text = "Not installed"; entry.StatusLabel.ForeColor = ColRed;   }
        else if (s.UpdateAvailable)      { entry.StatusLabel.Text = "Update available"; entry.StatusLabel.ForeColor = ColYellow; }
        else                             { entry.StatusLabel.Text = "Up to date";    entry.StatusLabel.ForeColor = ColGreen; }

        entry.VersionLabel.Text = s?.LocalVersion != null ? $"v{s.LocalVersion}" : "";
    }

    // ── Launch ─────────────────────────────────────────────────────────────────

    private void launchButton_Click(object sender, EventArgs e)
    {
        if (_selected?.Status?.LaunchPath == null) return;

        try
        {
            LauncherService.LaunchGame(_selected.Status.LaunchPath, _launcherRoot);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to launch:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void closeButton_Click(object sender, EventArgs e) => Close();

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static void HideFile(string path)
    {
        if (!File.Exists(path)) return;
        var attrs = File.GetAttributes(path);
        if ((attrs & FileAttributes.Hidden) == 0)
            File.SetAttributes(path, attrs | FileAttributes.Hidden);
    }
}

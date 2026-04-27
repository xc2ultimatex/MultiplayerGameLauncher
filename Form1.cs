using System.Text.Json;

namespace MultiplayerLauncher;

public partial class Form1 : Form
{
    private readonly string _launcherRoot;
    private readonly string _configPath;
    private LauncherConfig _config = LauncherConfig.Default;
    private readonly List<GameEntry> _games = new();
    private GameEntry? _selected;
    private int _activeTab = 0;

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

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ReleaseCapture();
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    public Form1()
    {
        _launcherRoot = AppContext.BaseDirectory;
        _configPath   = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MakeshiftStudios", "launcher.settings.json");
        InitializeComponent();
        DoubleBuffered = true;
        SetupButtonAnimations();
        SetupDrag();
        LoadAssets();
        Shown += async (_, _) => await StartupAsync();
    }

    private void SetupDrag()
    {
        void Drag(object? s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0x00A1, (IntPtr)2, IntPtr.Zero); // WM_NCLBUTTONDOWN, HTCAPTION
        }
        titleBar.MouseDown      += Drag;
        titleBarLabel.MouseDown += Drag;
    }

    private void LoadAssets()
    {
        // Taskbar + shortcut icon
        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using var iconStream = asm.GetManifestResourceStream("MultiplayerLauncher.Assets.icon.ico");
            if (iconStream != null)
                Icon = new Icon(iconStream);
        }
        catch { }
    }

    private void SetupButtonAnimations()
    {
        var launchHover = Color.FromArgb(70, 170, 90);
        UIAnimator.HoverColor(launchButton,        ColLaunch,                  launchHover);
        UIAnimator.HoverColor(installUpdateButton, Color.FromArgb(55,120,200), Color.FromArgb(75,145,225));
        UIAnimator.HoverColor(titleCloseBtn,       Color.Transparent,          Color.FromArgb(180, 50, 50), enterMs: 80, leaveMs: 200);
        UIAnimator.HoverColor(titleMinBtn,         Color.Transparent,          Color.FromArgb(50, 50, 75),  enterMs: 80, leaveMs: 200);
        UIAnimator.HoverColor(discordButton,       BgFooter,                   Color.FromArgb(22, 20, 36),  enterMs: 80, leaveMs: 180);
        UIAnimator.HoverColor(tabGamesBtn,         Color.Transparent,          Color.FromArgb(35, 32, 55),  enterMs: 80, leaveMs: 160);
        UIAnimator.HoverColor(tabSocialBtn,        Color.Transparent,          Color.FromArgb(35, 32, 55),  enterMs: 80, leaveMs: 160);
    }

    // ── Tab switching ──────────────────────────────────────────────────────────

    private void SelectTab(int index)
    {
        if (_activeTab == index) return;
        _activeTab = index;
        SoundFX.Play(SoundType.Select);

        bool games = index == 0;
        tabGamesBtn.ForeColor  = games  ? TextPrimary : TextMuted;
        tabSocialBtn.ForeColor = !games ? TextPrimary : TextMuted;

        gamesPanel.Visible       = games;
        socialPanel.Visible      = !games;
        gameListPanel.Visible    = games;
        friendsListPanel.Visible = !games;

        if (!games && UserSession.IsLoggedIn)
            _friendCodeLabel.Text = GenerateFriendCode(UserSession.Username);

        _ = AnimateTabIndicatorAsync(index * 100);
    }

    private async Task AnimateTabIndicatorAsync(int targetX)
    {
        int startX = tabIndicator.Left;
        const int steps = 8;
        for (int i = 1; i <= steps; i++)
        {
            tabIndicator.Left = (int)(startX + (targetX - startX) * UIAnimator.EaseOut((float)i / steps));
            await Task.Delay(12);
        }
        tabIndicator.Left = targetX;
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
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);

            if (!File.Exists(_configPath))
            {
                string json = JsonSerializer.Serialize(LauncherConfig.Default, JsonOptions.WriteIndented);
                await File.WriteAllTextAsync(_configPath, json);
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

            // Propagate any updated URLs from defaults for games that still have placeholder source dirs
            bool patched = false;
            for (int i = 0; i < _config.Games.Count; i++)
            {
                var game = _config.Games[i];
                bool isPlaceholder = string.IsNullOrWhiteSpace(game.UpdateSourceDirectory) ||
                                     game.UpdateSourceDirectory.Contains("DEV-MACHINE", StringComparison.OrdinalIgnoreCase);
                if (!isPlaceholder) continue;

                var defaultGame = LauncherConfig.Default.Games
                    .FirstOrDefault(g => string.Equals(g.Name, game.Name, StringComparison.OrdinalIgnoreCase));
                if (defaultGame == null) continue;

                bool defaultIsReal = !string.IsNullOrWhiteSpace(defaultGame.UpdateSourceDirectory) &&
                                     !defaultGame.UpdateSourceDirectory.Contains("DEV-MACHINE", StringComparison.OrdinalIgnoreCase);
                if (!defaultIsReal) continue;

                _config.Games[i] = defaultGame;
                patched = true;
            }

            if (patched)
            {
                string updated = JsonSerializer.Serialize(_config, JsonOptions.WriteIndented);
                await File.WriteAllTextAsync(_configPath, updated);
            }
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
        var previous = _selected;
        _selected = entry;

        SoundFX.Play(SoundType.Select);

        // Animate sidebar highlights
        if (previous != null && previous != entry)
            _ = UIAnimator.AnimateToAsync(previous.ListItem, BgSidebar, 200);
        _ = UIAnimator.AnimateToAsync(entry.ListItem, BgSelected, 140);

        // Animate right panel transition
        _ = TransitionRightPanelAsync();
    }

    private async Task TransitionRightPanelAsync()
    {
        var flash = Color.FromArgb(27, 25, 42);
        await UIAnimator.AnimateToAsync(gamesPanel, flash, 55);
        RefreshRightPanel();
        await UIAnimator.AnimateToAsync(gamesPanel, BgRight, 130);
    }

    private void RefreshRightPanel()
    {
        if (_selected == null) return;

        rightTitleLabel.Text = _selected.Settings.Name;

        // Reset button defaults
        launchButton.Visible            = true;
        launchButton.Enabled            = false;
        launchButton.Text               = "Launch Game";
        installUpdateButton.Visible     = false;
        installUpdateButton.Enabled     = true;
        uninstallButton.Visible         = false;

        if (_selected.IsBusy)
        {
            rightStatusLabel.Text      = _selected.StatusText;
            rightStatusLabel.ForeColor = ColYellow;
            launchButton.Enabled       = false;
            installUpdateButton.Visible = false;
        }
        else if (_selected.Status == null)
        {
            rightStatusLabel.Text      = "Checking for updates...";
            rightStatusLabel.ForeColor = ColGray;
        }
        else if (!_selected.Status.IsConfigured)
        {
            rightStatusLabel.Text      = "Not configured.";
            rightStatusLabel.ForeColor = ColRed;
        }
        else if (!_selected.Status.CanLaunch)
        {
            // Not installed — show Install button where Launch would be
            rightStatusLabel.Text         = $"Not installed.  (Looking in: ...\\{_selected.Settings.GameDirectoryName})";
            rightStatusLabel.ForeColor    = ColRed;
            launchButton.Visible          = false;
            installUpdateButton.Visible   = true;
            installUpdateButton.Text      = "Install";
            installUpdateButton.BackColor = Color.FromArgb(55, 120, 200);
            installUpdateButton.Size      = new Size(200, 46);
            installUpdateButton.Location  = new Point(28, 374);
        }
        else if (_selected.Status.UpdateAvailable)
        {
            // Launch + Update + Uninstall — three buttons side by side
            rightStatusLabel.Text         = $"Update available  ({_selected.Status.LocalVersion} → {_selected.Status.RemoteVersion})";
            rightStatusLabel.ForeColor    = ColYellow;
            launchButton.Size             = new Size(180, 46);
            launchButton.Enabled          = true;
            installUpdateButton.Visible   = true;
            installUpdateButton.Text      = "Update";
            installUpdateButton.BackColor = Color.FromArgb(180, 130, 30);
            installUpdateButton.Size      = new Size(140, 46);
            installUpdateButton.Location  = new Point(launchButton.Right + 10, launchButton.Top);
            uninstallButton.Location      = new Point(installUpdateButton.Right + 10, launchButton.Top);
        }
        else
        {
            // Launch + Uninstall side by side
            rightStatusLabel.Text      = $"Up to date  ({_selected.Status.LocalVersion})";
            rightStatusLabel.ForeColor = ColGreen;
            launchButton.Size          = new Size(200, 46);
            launchButton.Enabled       = true;
            uninstallButton.Location   = new Point(launchButton.Right + 12, launchButton.Top);
        }

        // Show uninstall whenever the game is actually installed
        uninstallButton.Visible = _selected.Status?.CanLaunch == true;

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

                // Verify with up to 5 retries — file system may need a moment to settle
                entry.Status = await LauncherService.CheckForUpdatesAsync(_launcherRoot, entry.Settings);
                for (int attempt = 1; attempt <= 5 && !entry.Status.CanLaunch; attempt++)
                {
                    SetBusy(entry, $"Verifying... ({attempt}/5)");
                    await Task.Delay(500);
                    entry.Status = await LauncherService.CheckForUpdatesAsync(_launcherRoot, entry.Settings);
                }

                if (entry.Status.CanLaunch) SoundFX.Play(SoundType.Success);
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
            SoundFX.Play(SoundType.Launch);
            LauncherService.LaunchGame(_selected.Status.LaunchPath, _launcherRoot,
                UserSession.IsLoggedIn ? UserSession.Username : null,
                UserSession.IsLoggedIn ? UserSession.Token    : null);
        }
        catch (Exception ex)
        {
            SoundFX.Play(SoundType.Error);
            MessageBox.Show($"Failed to launch:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void installUpdateButton_Click(object sender, EventArgs e)
    {
        if (_selected == null) return;
        SoundFX.Play(SoundType.Click);
        _ = InstallOrUpdateAsync(_selected);
    }

    private void uninstallButton_Click(object sender, EventArgs e)
    {
        if (_selected?.Status?.CanLaunch != true) return;

        string gameDir = _selected.Status.LocalGameDirectory;
        string gameName = _selected.Settings.Name;

        var result = MessageBox.Show(
            $"Uninstall {gameName}?\n\nThis will delete all game files at:\n{gameDir}",
            "Confirm Uninstall",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes) return;

        try
        {
            if (Directory.Exists(gameDir))
                Directory.Delete(gameDir, recursive: true);

            _selected.Status = null;
            UpdateListItem(_selected);
            RefreshRightPanel();
            SoundFX.Play(SoundType.Click);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Uninstall failed:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task InstallOrUpdateAsync(GameEntry entry)
    {
        string label = entry.Status?.CanLaunch == true ? "Updating" : "Installing";
        SetBusy(entry, $"{label}...");

        try
        {
            await LauncherService.UpdateAsync(_launcherRoot, entry.Settings, launch: false);

            // Verify install with up to 5 retries — file system may need a moment to settle
            entry.Status = await LauncherService.CheckForUpdatesAsync(_launcherRoot, entry.Settings);
            for (int attempt = 1; attempt <= 5 && !entry.Status.CanLaunch; attempt++)
            {
                SetBusy(entry, $"Verifying... ({attempt}/5)");
                await Task.Delay(500);
                entry.Status = await LauncherService.CheckForUpdatesAsync(_launcherRoot, entry.Settings);
            }

            SoundFX.Play(entry.Status.CanLaunch ? SoundType.Success : SoundType.Error);
        }
        catch (Exception ex)
        {
            SoundFX.Play(SoundType.Error);
            MessageBox.Show($"{label} failed:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            entry.IsBusy = false;
            UpdateListItem(entry);
            if (_selected == entry)
                RefreshRightPanel();
        }
    }

    private static readonly Color BgFooter = Color.FromArgb(14, 14, 22);

    private void discordButton_Paint(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(BgFooter);

        const int s = 20;
        int iy = (discordButton.Height - s) / 2;

        using var fill = new SolidBrush(Color.FromArgb(88, 101, 242));
        using var cut  = new SolidBrush(BgFooter);

        // Body
        g.FillEllipse(fill, 1, iy + 5, s - 2, s - 5);
        // Left ear
        g.FillEllipse(fill, 0, iy, 8, 12);
        // Right ear
        g.FillEllipse(fill, s - 8, iy, 8, 12);
        // Left eye cutout
        g.FillEllipse(cut, 3, iy + 9, 5, 5);
        // Right eye cutout
        g.FillEllipse(cut, s - 8, iy + 9, 5, 5);

        using var textBrush = new SolidBrush(Color.FromArgb(88, 101, 242));
        using var font      = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
        g.DrawString("Join Discord", font, textBrush, s + 6, (discordButton.Height - font.Height) / 2f);
    }

    private void discordLink_Click(object sender, EventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = "https://discord.gg/aXrWcWbYGE",
            UseShellExecute = true
        });
    }

    // ── Social ─────────────────────────────────────────────────────────────────

    private static string GenerateFriendCode(string username)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(username.ToLowerInvariant()));
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var sb = new System.Text.StringBuilder(9);
        for (int i = 0; i < 4; i++) sb.Append(chars[hash[i] % chars.Length]);
        sb.Append('-');
        for (int i = 4; i < 8; i++) sb.Append(chars[hash[i] % chars.Length]);
        return sb.ToString();
    }

    private void CopyCode_Click(object? sender, EventArgs e)
    {
        string code = _friendCodeLabel.Text;
        if (string.IsNullOrEmpty(code) || code == "——————") return;
        Clipboard.SetText(code);
        _copyCodeBtn.Text = "Copied!";
        var t = new System.Windows.Forms.Timer { Interval = 1500 };
        t.Tick += (_, _) => { _copyCodeBtn.Text = "Copy"; t.Stop(); t.Dispose(); };
        t.Start();
    }

    // Call this when a FRIEND_REQUEST push arrives from the broker
    internal void AddIncomingFriendRequest(string fromUsername)
    {
        if (_friendRequestsPanel.Controls.ContainsKey("noRequestsLbl"))
            _friendRequestsPanel.Controls.RemoveByKey("noRequestsLbl");

        // Avoid duplicates
        if (_friendRequestsPanel.Controls.ContainsKey("req_" + fromUsername)) return;

        int y = _friendRequestsPanel.Controls.Count * 36;

        var row = new Panel
        {
            Name      = "req_" + fromUsername,
            Location  = new Point(0, y),
            Size      = new Size(530, 32),
            BackColor = Color.FromArgb(28, 28, 44)
        };

        var nameLbl = new Label
        {
            Text      = fromUsername,
            Font      = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(210, 205, 230),
            Location  = new Point(10, 7),
            Size      = new Size(220, 18)
        };

        var acceptBtn = new Button
        {
            Text      = "Accept",
            Font      = new Font("Segoe UI", 8.5f),
            Location  = new Point(248, 4),
            Size      = new Size(66, 24),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(40, 110, 55),
            ForeColor = Color.White,
            Cursor    = Cursors.Hand
        };
        acceptBtn.FlatAppearance.BorderSize = 0;
        acceptBtn.Tag = fromUsername;
        acceptBtn.Click += (_, _) => RespondToFriendRequest(fromUsername, accept: true);

        var declineBtn = new Button
        {
            Text      = "Decline",
            Font      = new Font("Segoe UI", 8.5f),
            Location  = new Point(320, 4),
            Size      = new Size(66, 24),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(100, 38, 38),
            ForeColor = Color.White,
            Cursor    = Cursors.Hand
        };
        declineBtn.FlatAppearance.BorderSize = 0;
        declineBtn.Click += (_, _) => RespondToFriendRequest(fromUsername, accept: false);

        row.Controls.AddRange(new Control[] { nameLbl, acceptBtn, declineBtn });
        _friendRequestsPanel.Controls.Add(row);
    }

    private void RespondToFriendRequest(string fromUsername, bool accept)
    {
        _friendRequestsPanel.Controls.RemoveByKey("req_" + fromUsername);

        // Restack remaining rows
        int y = 0;
        foreach (Control c in _friendRequestsPanel.Controls)
        {
            c.Location = new Point(0, y);
            y += 36;
        }

        if (_friendRequestsPanel.Controls.Count == 0)
        {
            var lbl = new Label
            {
                Name      = "noRequestsLbl",
                Text      = "No pending friend requests.",
                Font      = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(80, 75, 110),
                Location  = new Point(0, 28),
                Size      = new Size(530, 24)
            };
            _friendRequestsPanel.Controls.Add(lbl);
        }

        // TODO: send FRIEND_ACCEPT or FRIEND_DECLINE to broker
        _ = accept; // suppress unused warning until broker is wired
    }

    private void AddFriend_Click(object? sender, EventArgs e)
    {
        string? code = ShowInputDialog("Add Friend", "Enter your friend's invite code:", "XXXX-XXXX");
        if (string.IsNullOrWhiteSpace(code)) return;
        code = code.Trim().ToUpperInvariant();
        // TODO: send FRIEND_ADD to broker with code
        MessageBox.Show($"Friend request sent for code: {code}\n\n(Broker connection coming soon)",
            "Add Friend", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void LogOut_Click(object? sender, EventArgs e)
    {
        AuthService.ClearSavedSession();
        UserSession.Clear();
        Application.Restart();
    }

    private static string? ShowInputDialog(string title, string prompt, string placeholder = "")
    {
        using var dlg = new Form
        {
            Text            = title,
            Size            = new Size(360, 160),
            MinimumSize     = new Size(360, 160),
            MaximumSize     = new Size(360, 160),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition   = FormStartPosition.CenterParent,
            MaximizeBox     = false,
            MinimizeBox     = false
        };
        var lbl    = new Label { Text = prompt, Location = new Point(12, 14), Size = new Size(328, 20) };
        var txt    = new TextBox { Text = placeholder, Location = new Point(12, 38), Size = new Size(328, 24) };
        var ok     = new Button { Text = "OK",     DialogResult = DialogResult.OK,     Location = new Point(164, 76), Size = new Size(80, 28) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(252, 76), Size = new Size(80, 28) };
        dlg.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;
        txt.SelectAll();
        txt.Focus();
        return dlg.ShowDialog() == DialogResult.OK ? txt.Text : null;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static void HideFile(string path)
    {
        if (!File.Exists(path)) return;
        var attrs = File.GetAttributes(path);
        if ((attrs & FileAttributes.Hidden) == 0)
            File.SetAttributes(path, attrs | FileAttributes.Hidden);
    }
}

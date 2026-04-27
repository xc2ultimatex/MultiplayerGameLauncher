namespace MultiplayerLauncher;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    // ── Control fields ─────────────────────────────────────────────────────────
    private Panel        titleBar;
    private Label        titleBarLabel;
    private Button       titleMinBtn;
    private Button       titleCloseBtn;
    private Panel        tabStrip;
    private Label        tabGamesBtn;
    private Label        tabSocialBtn;
    private Panel        tabIndicator;
    private Panel        mainPanel;
    private Panel        sidebarPanel;
    private Panel        gameListPanel;
    private Panel        friendsListPanel;
    private Panel        dividerPanel;
    private Panel        contentPanel;
    private Panel        gamesPanel;
    private Label        rightTitleLabel;
    private Label        rightStatusLabel;
    private RichTextBox  patchNotesBox;
    private Button       launchButton;
    private Button       installUpdateButton;
    private Button       uninstallButton;
    private Panel        socialPanel;
    private Label        _friendCodeLabel;
    private Button       _copyCodeBtn;
    private Button       _addFriendBtn;
    private Panel        _friendRequestsPanel;
    private Panel        footerPanel;
    private Panel        discordButton;
    private Button       _signOutBtn;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        // ── Palette ────────────────────────────────────────────────────────────
        var bgTitle   = Color.FromArgb(12,  12,  20);
        var bgDark    = Color.FromArgb(18,  18,  28);
        var bgSidebar = Color.FromArgb(24,  24,  38);
        var bgRight   = Color.FromArgb(22,  22,  34);
        var bgFooter  = Color.FromArgb(14,  14,  22);
        var textPri   = Color.FromArgb(230, 225, 240);
        var textMuted = Color.FromArgb(140, 135, 160);
        var accent    = Color.FromArgb(120,  80, 200);
        var colLaunch = Color.FromArgb(55,  145,  70);

        // ── Form ───────────────────────────────────────────────────────────────
        AutoScaleDimensions = new SizeF(7f, 15f);
        AutoScaleMode       = AutoScaleMode.Font;
        ClientSize          = new Size(820, 560);
        MinimumSize         = new Size(820, 560);
        MaximumSize         = new Size(820, 560);
        FormBorderStyle     = FormBorderStyle.None;
        StartPosition       = FormStartPosition.CenterScreen;
        BackColor           = bgDark;
        Font                = new Font("Segoe UI", 9f);

        // ── Title bar ──────────────────────────────────────────────────────────
        titleBar           = new Panel();
        titleBar.Dock      = DockStyle.Top;
        titleBar.Height    = 38;
        titleBar.BackColor = bgTitle;

        var accentStripe = new Panel { Location = new Point(0,0), Size = new Size(3,38), BackColor = accent };
        var logoDot      = new Panel { Location = new Point(14,11), Size = new Size(16,16), BackColor = accent };

        titleBarLabel           = new Label();
        titleBarLabel.Text      = "Makeshift Studios Launcher";
        titleBarLabel.Font      = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        titleBarLabel.ForeColor = Color.FromArgb(200, 195, 220);
        titleBarLabel.Location  = new Point(38, 0);
        titleBarLabel.Size      = new Size(600, 38);
        titleBarLabel.TextAlign = ContentAlignment.MiddleLeft;

        titleMinBtn = MakeTitleBtn("─", new Point(742, 0));
        titleMinBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 75);
        titleMinBtn.Click += (_, _) => WindowState = FormWindowState.Minimized;

        titleCloseBtn = MakeTitleBtn("✕", new Point(780, 0));
        titleCloseBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(180, 50, 50);
        titleCloseBtn.Click += (_, _) => Close();

        titleBar.Controls.AddRange(new Control[] { accentStripe, logoDot, titleBarLabel, titleMinBtn, titleCloseBtn });

        // ── Tab strip ──────────────────────────────────────────────────────────
        tabStrip           = new Panel();
        tabStrip.Dock      = DockStyle.Top;
        tabStrip.Height    = 36;
        tabStrip.BackColor = bgTitle;

        var tabBorderLine = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(35,32,55) };

        tabGamesBtn = MakeTabLabel("Games", 0);
        tabSocialBtn = MakeTabLabel("Social", 1);
        tabSocialBtn.ForeColor = textMuted;

        tabIndicator           = new Panel();
        tabIndicator.Size      = new Size(100, 2);
        tabIndicator.Location  = new Point(0, 33);
        tabIndicator.BackColor = accent;

        tabGamesBtn.Click  += (_, _) => SelectTab(0);
        tabSocialBtn.Click += (_, _) => SelectTab(1);

        tabStrip.Controls.AddRange(new Control[] { tabBorderLine, tabGamesBtn, tabSocialBtn, tabIndicator });

        // ── Footer ─────────────────────────────────────────────────────────────
        footerPanel           = new Panel();
        footerPanel.Dock      = DockStyle.Bottom;
        footerPanel.Height    = 40;
        footerPanel.BackColor = bgFooter;

        var footerLine = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(38,35,58) };

        discordButton          = new Panel();
        discordButton.Location = new Point(16, 8);
        discordButton.Size     = new Size(160, 24);
        discordButton.Cursor   = Cursors.Hand;
        discordButton.Paint   += discordButton_Paint;
        discordButton.Click   += discordLink_Click;

        var footerUserLabel           = new Label();
        footerUserLabel.Name          = "footerUserLabel";
        footerUserLabel.Text          = UserSession.IsLoggedIn ? UserSession.Username : "";
        footerUserLabel.Font          = new Font("Segoe UI", 8.5f);
        footerUserLabel.ForeColor     = Color.FromArgb(90, 85, 120);
        footerUserLabel.Anchor        = AnchorStyles.Top | AnchorStyles.Right;
        footerUserLabel.Size          = new Size(220, 40);
        footerUserLabel.Location      = new Point(820 - 240, 0);
        footerUserLabel.TextAlign     = ContentAlignment.MiddleRight;

        _signOutBtn                                          = new Button();
        _signOutBtn.Text                                     = "Sign Out";
        _signOutBtn.Font                                     = new Font("Segoe UI", 8.5f);
        _signOutBtn.ForeColor                                = Color.FromArgb(110, 100, 140);
        _signOutBtn.Location                                 = new Point(820 - 320, 8);
        _signOutBtn.Size                                     = new Size(70, 24);
        _signOutBtn.FlatStyle                                = FlatStyle.Flat;
        _signOutBtn.BackColor                                = Color.Transparent;
        _signOutBtn.FlatAppearance.BorderSize                = 0;
        _signOutBtn.FlatAppearance.MouseOverBackColor        = Color.FromArgb(30, 28, 50);
        _signOutBtn.Cursor                                   = Cursors.Hand;
        _signOutBtn.Click                                   += LogOut_Click;

        footerPanel.Controls.AddRange(new Control[] { footerLine, discordButton, _signOutBtn, footerUserLabel });

        // ── Main panel (holds sidebar + content) ───────────────────────────────
        mainPanel           = new Panel();
        mainPanel.Dock      = DockStyle.Fill;
        mainPanel.BackColor = bgDark;

        // ── Sidebar ────────────────────────────────────────────────────────────
        sidebarPanel           = new Panel();
        sidebarPanel.Dock      = DockStyle.Left;
        sidebarPanel.Width     = 210;
        sidebarPanel.BackColor = bgSidebar;

        gameListPanel            = new Panel();
        gameListPanel.Dock       = DockStyle.Fill;
        gameListPanel.BackColor  = bgSidebar;
        gameListPanel.AutoScroll = true;

        friendsListPanel           = new Panel();
        friendsListPanel.Dock      = DockStyle.Fill;
        friendsListPanel.BackColor = bgSidebar;
        friendsListPanel.Visible   = false;

        var noFriendsLbl           = new Label();
        noFriendsLbl.Text          = "No friends yet";
        noFriendsLbl.Font          = new Font("Segoe UI", 9.5f);
        noFriendsLbl.ForeColor     = Color.FromArgb(80, 75, 110);
        noFriendsLbl.Dock          = DockStyle.Fill;
        noFriendsLbl.TextAlign     = ContentAlignment.MiddleCenter;
        friendsListPanel.Controls.Add(noFriendsLbl);

        sidebarPanel.Controls.Add(friendsListPanel);
        sidebarPanel.Controls.Add(gameListPanel);

        // ── Divider ────────────────────────────────────────────────────────────
        dividerPanel           = new Panel();
        dividerPanel.Dock      = DockStyle.Left;
        dividerPanel.Width     = 1;
        dividerPanel.BackColor = Color.FromArgb(38, 35, 58);

        // ── Content panel ──────────────────────────────────────────────────────
        contentPanel           = new Panel();
        contentPanel.Dock      = DockStyle.Fill;
        contentPanel.BackColor = bgRight;

        // ── Games panel ────────────────────────────────────────────────────────
        gamesPanel           = new Panel();
        gamesPanel.Dock      = DockStyle.Fill;
        gamesPanel.BackColor = bgRight;

        rightTitleLabel              = new Label();
        rightTitleLabel.Text         = "";
        rightTitleLabel.Font         = new Font("Segoe UI Semibold", 20f, FontStyle.Bold);
        rightTitleLabel.ForeColor    = textPri;
        rightTitleLabel.Location     = new Point(28, 28);
        rightTitleLabel.Size         = new Size(530, 38);
        rightTitleLabel.Anchor       = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        rightTitleLabel.AutoEllipsis = true;

        rightStatusLabel           = new Label();
        rightStatusLabel.Text      = "";
        rightStatusLabel.Font      = new Font("Segoe UI", 9.5f);
        rightStatusLabel.ForeColor = textMuted;
        rightStatusLabel.Location  = new Point(28, 72);
        rightStatusLabel.Size      = new Size(530, 20);
        rightStatusLabel.Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        var sep       = new Panel();
        sep.Location  = new Point(28, 100);
        sep.Size      = new Size(530, 1);
        sep.Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        sep.BackColor = Color.FromArgb(38, 35, 58);

        var pnHeader     = new Label();
        pnHeader.Text    = "PATCH NOTES";
        pnHeader.Font    = new Font("Segoe UI", 8f, FontStyle.Bold);
        pnHeader.ForeColor = Color.FromArgb(100, 95, 130);
        pnHeader.Location = new Point(28, 112);
        pnHeader.Size    = new Size(530, 18);
        pnHeader.Anchor  = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        patchNotesBox             = new RichTextBox();
        patchNotesBox.Location    = new Point(28, 134);
        patchNotesBox.Size        = new Size(530, 220);
        patchNotesBox.Anchor      = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        patchNotesBox.ReadOnly    = true;
        patchNotesBox.BackColor   = Color.FromArgb(28, 28, 44);
        patchNotesBox.ForeColor   = textPri;
        patchNotesBox.Font        = new Font("Segoe UI", 9.5f);
        patchNotesBox.BorderStyle = BorderStyle.None;
        patchNotesBox.ScrollBars  = RichTextBoxScrollBars.Vertical;

        launchButton                         = new Button();
        launchButton.Text                    = "Launch Game";
        launchButton.Font                    = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
        launchButton.Location                = new Point(28, 374);
        launchButton.Size                    = new Size(200, 46);
        launchButton.Anchor                  = AnchorStyles.Top | AnchorStyles.Left;
        launchButton.BackColor               = colLaunch;
        launchButton.ForeColor               = Color.White;
        launchButton.FlatStyle               = FlatStyle.Flat;
        launchButton.FlatAppearance.BorderSize = 0;
        launchButton.Cursor                  = Cursors.Hand;
        launchButton.Enabled                 = false;
        launchButton.Click                  += launchButton_Click;

        installUpdateButton                         = new Button();
        installUpdateButton.Text                    = "Install";
        installUpdateButton.Font                    = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
        installUpdateButton.Location                = new Point(240, 374);
        installUpdateButton.Size                    = new Size(160, 46);
        installUpdateButton.Anchor                  = AnchorStyles.Top | AnchorStyles.Left;
        installUpdateButton.BackColor               = Color.FromArgb(55, 120, 200);
        installUpdateButton.ForeColor               = Color.White;
        installUpdateButton.FlatStyle               = FlatStyle.Flat;
        installUpdateButton.FlatAppearance.BorderSize = 0;
        installUpdateButton.Cursor                  = Cursors.Hand;
        installUpdateButton.Visible                 = false;
        installUpdateButton.Click                  += installUpdateButton_Click;

        uninstallButton                              = new Button();
        uninstallButton.Text                         = "Uninstall";
        uninstallButton.Font                         = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
        uninstallButton.Location                     = new Point(240, 374);
        uninstallButton.Size                         = new Size(130, 46);
        uninstallButton.Anchor                       = AnchorStyles.Top | AnchorStyles.Left;
        uninstallButton.FlatStyle                    = FlatStyle.Flat;
        uninstallButton.BackColor                    = Color.FromArgb(100, 35, 35);
        uninstallButton.ForeColor                    = Color.White;
        uninstallButton.FlatAppearance.BorderSize    = 0;
        uninstallButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(130, 45, 45);
        uninstallButton.Cursor                       = Cursors.Hand;
        uninstallButton.Visible                      = false;
        uninstallButton.Click                       += uninstallButton_Click;

        gamesPanel.Controls.AddRange(new Control[]
        {
            rightTitleLabel, rightStatusLabel, sep, pnHeader,
            patchNotesBox, launchButton, installUpdateButton, uninstallButton
        });

        // ── Social panel ───────────────────────────────────────────────────────
        socialPanel           = new Panel();
        socialPanel.Dock      = DockStyle.Fill;
        socialPanel.BackColor = bgRight;
        socialPanel.Visible   = false;

        var fcHeader           = new Label();
        fcHeader.Text          = "YOUR FRIEND CODE";
        fcHeader.Font          = new Font("Segoe UI", 8f, FontStyle.Bold);
        fcHeader.ForeColor     = Color.FromArgb(100, 95, 130);
        fcHeader.Location      = new Point(28, 28);
        fcHeader.Size          = new Size(530, 18);
        fcHeader.Anchor        = AnchorStyles.Top | AnchorStyles.Left;

        var fcBox           = new Panel();
        fcBox.Location      = new Point(28, 50);
        fcBox.Size          = new Size(440, 50);
        fcBox.BackColor     = Color.FromArgb(28, 28, 44);
        fcBox.Anchor        = AnchorStyles.Top | AnchorStyles.Left;

        _friendCodeLabel           = new Label();
        _friendCodeLabel.Text      = "——————";
        _friendCodeLabel.Font      = new Font("Consolas", 18f, FontStyle.Bold);
        _friendCodeLabel.ForeColor = Color.FromArgb(160, 130, 230);
        _friendCodeLabel.Location  = new Point(14, 7);
        _friendCodeLabel.Size      = new Size(300, 36);
        _friendCodeLabel.TextAlign = ContentAlignment.MiddleLeft;
        _friendCodeLabel.Cursor    = Cursors.Hand;

        _copyCodeBtn                              = new Button();
        _copyCodeBtn.Text                         = "Copy";
        _copyCodeBtn.Font                         = new Font("Segoe UI", 9f);
        _copyCodeBtn.Location                     = new Point(324, 12);
        _copyCodeBtn.Size                         = new Size(72, 26);
        _copyCodeBtn.FlatStyle                    = FlatStyle.Flat;
        _copyCodeBtn.BackColor                    = Color.FromArgb(55, 50, 90);
        _copyCodeBtn.ForeColor                    = Color.FromArgb(180, 170, 220);
        _copyCodeBtn.FlatAppearance.BorderSize    = 0;
        _copyCodeBtn.Cursor                       = Cursors.Hand;
        _copyCodeBtn.Click                       += CopyCode_Click;

        fcBox.Controls.Add(_friendCodeLabel);
        fcBox.Controls.Add(_copyCodeBtn);

        var fcHint           = new Label();
        fcHint.Text          = "Share this code with friends so they can add you.";
        fcHint.Font          = new Font("Segoe UI", 8.5f);
        fcHint.ForeColor     = Color.FromArgb(90, 85, 120);
        fcHint.Location      = new Point(28, 108);
        fcHint.Size          = new Size(440, 18);
        fcHint.Anchor        = AnchorStyles.Top | AnchorStyles.Left;

        _addFriendBtn                              = new Button();
        _addFriendBtn.Text                         = "+ Add Friend";
        _addFriendBtn.Font                         = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        _addFriendBtn.Location                     = new Point(28, 136);
        _addFriendBtn.Size                         = new Size(160, 34);
        _addFriendBtn.FlatStyle                    = FlatStyle.Flat;
        _addFriendBtn.BackColor                    = Color.FromArgb(80, 55, 150);
        _addFriendBtn.ForeColor                    = Color.White;
        _addFriendBtn.FlatAppearance.BorderSize    = 0;
        _addFriendBtn.Cursor                       = Cursors.Hand;
        _addFriendBtn.Click                       += AddFriend_Click;

        var sep1           = new Panel();
        sep1.Location      = new Point(28, 184);
        sep1.Size          = new Size(530, 1);
        sep1.BackColor     = Color.FromArgb(38, 35, 58);
        sep1.Anchor        = AnchorStyles.Top | AnchorStyles.Left;

        // ── Friend requests section ────────────────────────────────────────────
        var reqHeader           = new Label();
        reqHeader.Text          = "FRIEND REQUESTS";
        reqHeader.Font          = new Font("Segoe UI", 8f, FontStyle.Bold);
        reqHeader.ForeColor     = Color.FromArgb(100, 95, 130);
        reqHeader.Location      = new Point(28, 196);
        reqHeader.Size          = new Size(440, 18);
        reqHeader.Anchor        = AnchorStyles.Top | AnchorStyles.Left;

        _friendRequestsPanel             = new Panel();
        _friendRequestsPanel.Location    = new Point(28, 218);
        _friendRequestsPanel.Size        = new Size(530, 80);
        _friendRequestsPanel.BackColor   = bgRight;
        _friendRequestsPanel.AutoScroll  = true;
        _friendRequestsPanel.Anchor      = AnchorStyles.Top | AnchorStyles.Left;

        var noRequestsLbl           = new Label();
        noRequestsLbl.Name          = "noRequestsLbl";
        noRequestsLbl.Text          = "No pending friend requests.";
        noRequestsLbl.Font          = new Font("Segoe UI", 9.5f);
        noRequestsLbl.ForeColor     = Color.FromArgb(80, 75, 110);
        noRequestsLbl.Location      = new Point(0, 28);
        noRequestsLbl.Size          = new Size(530, 24);
        _friendRequestsPanel.Controls.Add(noRequestsLbl);

        var sep2           = new Panel();
        sep2.Location      = new Point(28, 310);
        sep2.Size          = new Size(530, 1);
        sep2.BackColor     = Color.FromArgb(38, 35, 58);
        sep2.Anchor        = AnchorStyles.Top | AnchorStyles.Left;

        // ── Friends list section ───────────────────────────────────────────────
        var friendsHeader           = new Label();
        friendsHeader.Text          = "FRIENDS";
        friendsHeader.Font          = new Font("Segoe UI", 8f, FontStyle.Bold);
        friendsHeader.ForeColor     = Color.FromArgb(100, 95, 130);
        friendsHeader.Location      = new Point(28, 322);
        friendsHeader.Size          = new Size(530, 18);
        friendsHeader.Anchor        = AnchorStyles.Top | AnchorStyles.Left;

        var noFriendsContent           = new Label();
        noFriendsContent.Text          = "No friends yet. Share your code to get started.";
        noFriendsContent.Font          = new Font("Segoe UI", 9.5f);
        noFriendsContent.ForeColor     = Color.FromArgb(80, 75, 110);
        noFriendsContent.Location      = new Point(28, 348);
        noFriendsContent.Size          = new Size(530, 24);
        noFriendsContent.Anchor        = AnchorStyles.Top | AnchorStyles.Left;

        socialPanel.Controls.AddRange(new Control[]
        {
            fcHeader, fcBox, fcHint, _addFriendBtn,
            sep1, reqHeader, _friendRequestsPanel,
            sep2, friendsHeader, noFriendsContent
        });

        // Fill must be added last in each container for correct docking
        contentPanel.Controls.Add(socialPanel);
        contentPanel.Controls.Add(gamesPanel);

        mainPanel.Controls.Add(contentPanel);
        mainPanel.Controls.Add(dividerPanel);
        mainPanel.Controls.Add(sidebarPanel);

        Controls.Add(mainPanel);
        Controls.Add(footerPanel);
        Controls.Add(tabStrip);
        Controls.Add(titleBar);

        ResumeLayout(false);
    }

    // ── Designer helpers ───────────────────────────────────────────────────────

    private static Button MakeTitleBtn(string text, Point loc)
    {
        var b = new Button
        {
            Text      = text,
            Font      = new Font("Segoe UI", 9f),
            Location  = loc,
            Size      = new Size(38, 38),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(140, 135, 160),
            Cursor    = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private static Label MakeTabLabel(string text, int index)
    {
        return new Label
        {
            Text      = text,
            Font      = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(230, 225, 240),
            Location  = new Point(index * 100, 0),
            Size      = new Size(100, 34),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor    = Cursors.Hand
        };
    }
}

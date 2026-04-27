namespace MultiplayerLauncher;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    private Panel       sidebarPanel;
    private Label       sidebarTitleLabel;
    private Panel       gameListPanel;
    private Panel       dividerPanel;
    private Panel       rightPanel;
    private Label       rightTitleLabel;
    private Label       rightStatusLabel;
    private Label       patchNotesHeaderLabel;
    private RichTextBox patchNotesBox;
    private Button      launchButton;
    private Button      closeButton;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        // ── Colors ─────────────────────────────────────────────────────────────
        var bgDark    = Color.FromArgb(18,  18,  28);
        var bgSidebar = Color.FromArgb(24,  24,  38);
        var bgRight   = Color.FromArgb(22,  22,  34);
        var textPri   = Color.FromArgb(230, 225, 240);
        var textMuted = Color.FromArgb(140, 135, 160);
        var colLaunch = Color.FromArgb(55,  145,  70);

        // ── Form ───────────────────────────────────────────────────────────────
        AutoScaleDimensions = new SizeF(7f, 15f);
        AutoScaleMode       = AutoScaleMode.Font;
        ClientSize          = new Size(780, 500);
        FormBorderStyle     = FormBorderStyle.FixedDialog;
        MaximizeBox         = false;
        MinimizeBox         = false;
        StartPosition       = FormStartPosition.CenterScreen;
        Text                = "Game Launcher";
        BackColor           = bgDark;
        Font                = new Font("Segoe UI", 9f);

        // ── Sidebar ────────────────────────────────────────────────────────────
        sidebarPanel           = new Panel();
        sidebarPanel.Location  = new Point(0, 0);
        sidebarPanel.Size      = new Size(220, 500);
        sidebarPanel.BackColor = bgSidebar;

        sidebarTitleLabel           = new Label();
        sidebarTitleLabel.Text      = "SELECT GAME";
        sidebarTitleLabel.Font      = new Font("Segoe UI", 8f, FontStyle.Bold);
        sidebarTitleLabel.ForeColor = Color.FromArgb(100, 95, 130);
        sidebarTitleLabel.Location  = new Point(16, 18);
        sidebarTitleLabel.Size      = new Size(188, 18);
        sidebarTitleLabel.AutoSize  = false;

        gameListPanel           = new Panel();
        gameListPanel.Location  = new Point(0, 44);
        gameListPanel.Size      = new Size(220, 456);
        gameListPanel.BackColor = bgSidebar;
        gameListPanel.AutoScroll = true;

        sidebarPanel.Controls.Add(sidebarTitleLabel);
        sidebarPanel.Controls.Add(gameListPanel);

        // ── Divider ────────────────────────────────────────────────────────────
        dividerPanel           = new Panel();
        dividerPanel.Location  = new Point(220, 0);
        dividerPanel.Size      = new Size(1, 500);
        dividerPanel.BackColor = Color.FromArgb(45, 42, 65);

        // ── Right panel ────────────────────────────────────────────────────────
        rightPanel           = new Panel();
        rightPanel.Location  = new Point(221, 0);
        rightPanel.Size      = new Size(559, 500);
        rightPanel.BackColor = bgRight;

        rightTitleLabel           = new Label();
        rightTitleLabel.Text      = "";
        rightTitleLabel.Font      = new Font("Segoe UI Semibold", 20f, FontStyle.Bold);
        rightTitleLabel.ForeColor = textPri;
        rightTitleLabel.Location  = new Point(28, 28);
        rightTitleLabel.Size      = new Size(510, 38);
        rightTitleLabel.AutoEllipsis = true;

        rightStatusLabel           = new Label();
        rightStatusLabel.Text      = "";
        rightStatusLabel.Font      = new Font("Segoe UI", 9.5f);
        rightStatusLabel.ForeColor = textMuted;
        rightStatusLabel.Location  = new Point(28, 72);
        rightStatusLabel.Size      = new Size(510, 20);

        // Separator line
        var sep           = new Panel();
        sep.Location      = new Point(28, 102);
        sep.Size          = new Size(510, 1);
        sep.BackColor     = Color.FromArgb(45, 42, 65);

        patchNotesHeaderLabel           = new Label();
        patchNotesHeaderLabel.Text      = "PATCH NOTES";
        patchNotesHeaderLabel.Font      = new Font("Segoe UI", 8f, FontStyle.Bold);
        patchNotesHeaderLabel.ForeColor = Color.FromArgb(100, 95, 130);
        patchNotesHeaderLabel.Location  = new Point(28, 114);
        patchNotesHeaderLabel.Size      = new Size(510, 18);

        patchNotesBox              = new RichTextBox();
        patchNotesBox.Location     = new Point(28, 138);
        patchNotesBox.Size         = new Size(510, 268);
        patchNotesBox.ReadOnly     = true;
        patchNotesBox.BackColor    = Color.FromArgb(28, 28, 44);
        patchNotesBox.ForeColor    = textPri;
        patchNotesBox.Font         = new Font("Segoe UI", 9.5f);
        patchNotesBox.BorderStyle  = BorderStyle.None;
        patchNotesBox.ScrollBars   = RichTextBoxScrollBars.Vertical;
        patchNotesBox.Text         = "";

        launchButton                    = new Button();
        launchButton.Text               = "Launch Game";
        launchButton.Font               = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
        launchButton.Location           = new Point(28, 420);
        launchButton.Size               = new Size(200, 46);
        launchButton.BackColor          = colLaunch;
        launchButton.ForeColor          = Color.White;
        launchButton.FlatStyle          = FlatStyle.Flat;
        launchButton.FlatAppearance.BorderSize = 0;
        launchButton.Cursor             = Cursors.Hand;
        launchButton.Enabled            = false;
        launchButton.Click             += launchButton_Click;

        closeButton                    = new Button();
        closeButton.Text               = "Close";
        closeButton.Font               = new Font("Segoe UI", 9f);
        closeButton.Location           = new Point(460, 432);
        closeButton.Size               = new Size(78, 28);
        closeButton.FlatStyle          = FlatStyle.Flat;
        closeButton.FlatAppearance.BorderColor = Color.FromArgb(60, 55, 85);
        closeButton.ForeColor          = textMuted;
        closeButton.BackColor          = Color.Transparent;
        closeButton.Cursor             = Cursors.Hand;
        closeButton.Click             += closeButton_Click;

        rightPanel.Controls.AddRange(new Control[]
        {
            rightTitleLabel, rightStatusLabel, sep,
            patchNotesHeaderLabel, patchNotesBox,
            launchButton, closeButton
        });

        Controls.AddRange(new Control[] { sidebarPanel, dividerPanel, rightPanel });

        ResumeLayout(false);
    }
}

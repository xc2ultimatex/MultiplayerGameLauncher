namespace Bootstrap;

partial class BootstrapForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        statusLabel  = new Label();
        progressBar  = new ProgressBar();
        SuspendLayout();

        // ── Form ──────────────────────────────────────────────────────────────
        ClientSize          = new Size(420, 110);
        FormBorderStyle     = FormBorderStyle.FixedSingle;
        MaximizeBox         = false;
        MinimizeBox         = false;
        StartPosition       = FormStartPosition.CenterScreen;
        Text                = "Launcher";
        BackColor           = Color.FromArgb(18, 18, 28);
        ForeColor           = Color.White;
        Font                = new Font("Segoe UI", 9f, FontStyle.Regular);
        ShowInTaskbar       = true;

        // ── statusLabel ───────────────────────────────────────────────────────
        statusLabel.AutoSize  = false;
        statusLabel.Dock      = DockStyle.None;
        statusLabel.Location  = new Point(16, 20);
        statusLabel.Size      = new Size(388, 24);
        statusLabel.Text      = "Starting...";
        statusLabel.ForeColor = Color.FromArgb(190, 190, 210);
        statusLabel.Font      = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        // ── progressBar ───────────────────────────────────────────────────────
        progressBar.Location  = new Point(16, 56);
        progressBar.Size      = new Size(388, 18);
        progressBar.Minimum   = 0;
        progressBar.Maximum   = 100;
        progressBar.Value     = 0;
        progressBar.Visible   = false;
        progressBar.Style     = ProgressBarStyle.Continuous;
        progressBar.ForeColor = Color.FromArgb(80, 200, 110);
        progressBar.BackColor = Color.FromArgb(40, 40, 60);

        Controls.Add(statusLabel);
        Controls.Add(progressBar);
        ResumeLayout(false);
    }

    private Label       statusLabel  = null!;
    private ProgressBar progressBar  = null!;
}

namespace MultiplayerLauncher;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;
    private Label titleLabel = null!;
    private Label subtitleLabel = null!;
    private Label statusValueLabel = null!;
    private Button retryButton = null!;
    private Button launchInstalledButton = null!;
    private Button closeButton = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        titleLabel = new Label();
        subtitleLabel = new Label();
        statusValueLabel = new Label();
        retryButton = new Button();
        launchInstalledButton = new Button();
        closeButton = new Button();
        SuspendLayout();
        //
        // titleLabel
        //
        titleLabel.AutoSize = true;
        titleLabel.Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold, GraphicsUnit.Point);
        titleLabel.Location = new Point(24, 20);
        titleLabel.Name = "titleLabel";
        titleLabel.Size = new Size(134, 37);
        titleLabel.TabIndex = 0;
        titleLabel.Text = "Launcher";
        //
        // subtitleLabel
        //
        subtitleLabel.AutoSize = true;
        subtitleLabel.ForeColor = Color.FromArgb(80, 80, 80);
        subtitleLabel.Location = new Point(28, 60);
        subtitleLabel.Name = "subtitleLabel";
        subtitleLabel.Size = new Size(250, 15);
        subtitleLabel.TabIndex = 1;
        subtitleLabel.Text = "Checking for updates and starting the game.";
        //
        // statusValueLabel
        //
        statusValueLabel.BorderStyle = BorderStyle.FixedSingle;
        statusValueLabel.Location = new Point(28, 98);
        statusValueLabel.Name = "statusValueLabel";
        statusValueLabel.Padding = new Padding(10);
        statusValueLabel.Size = new Size(428, 80);
        statusValueLabel.TabIndex = 2;
        statusValueLabel.Text = "Starting...";
        //
        // retryButton
        //
        retryButton.Location = new Point(28, 198);
        retryButton.Name = "retryButton";
        retryButton.Size = new Size(100, 34);
        retryButton.TabIndex = 3;
        retryButton.Text = "Retry";
        retryButton.UseVisualStyleBackColor = true;
        retryButton.Click += retryButton_Click;
        //
        // launchInstalledButton
        //
        launchInstalledButton.Location = new Point(240, 198);
        launchInstalledButton.Name = "launchInstalledButton";
        launchInstalledButton.Size = new Size(110, 34);
        launchInstalledButton.TabIndex = 4;
        launchInstalledButton.Text = "Launch Game";
        launchInstalledButton.UseVisualStyleBackColor = true;
        launchInstalledButton.Click += launchInstalledButton_Click;
        //
        // closeButton
        //
        closeButton.Location = new Point(356, 198);
        closeButton.Name = "closeButton";
        closeButton.Size = new Size(100, 34);
        closeButton.TabIndex = 5;
        closeButton.Text = "Close";
        closeButton.UseVisualStyleBackColor = true;
        closeButton.Click += closeButton_Click;
        //
        // Form1
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.WhiteSmoke;
        ClientSize = new Size(484, 261);
        Controls.Add(closeButton);
        Controls.Add(launchInstalledButton);
        Controls.Add(retryButton);
        Controls.Add(statusValueLabel);
        Controls.Add(subtitleLabel);
        Controls.Add(titleLabel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "Form1";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Game Launcher";
        ResumeLayout(false);
        PerformLayout();
    }
}

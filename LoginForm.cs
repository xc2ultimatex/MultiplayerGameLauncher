namespace MultiplayerLauncher;

internal sealed class LoginForm : Form
{
    // ── Palette ────────────────────────────────────────────────────────────────
    private static readonly Color BgTitle  = Color.FromArgb(12,  12,  20);
    private static readonly Color BgDark   = Color.FromArgb(18,  18,  28);
    private static readonly Color BgField  = Color.FromArgb(28,  28,  44);
    private static readonly Color TextPri  = Color.FromArgb(230, 225, 240);
    private static readonly Color TextMute = Color.FromArgb(140, 135, 160);
    private static readonly Color Accent   = Color.FromArgb(120,  80, 200);
    private static readonly Color ColGreen = Color.FromArgb(55,  145,  70);
    private static readonly Color ColBlue  = Color.FromArgb(55,  120, 200);
    private static readonly Color ColRed   = Color.FromArgb(210,  75,  75);

    // ── Panels ─────────────────────────────────────────────────────────────────
    private readonly Panel _loginPanel;
    private readonly Panel _createPanel;
    private readonly Panel _forgotPanel;

    // ── Login controls ─────────────────────────────────────────────────────────
    private readonly TextBox  _loginUser;
    private readonly TextBox  _loginPass;
    private readonly CheckBox _loginRemember;
    private readonly Button   _loginBtn;
    private readonly Label    _loginError;

    // ── Create account controls ────────────────────────────────────────────────
    private readonly TextBox _regUser;
    private readonly TextBox _regPass;
    private readonly TextBox _regConfirm;
    private readonly Button  _regBtn;
    private readonly Label   _regError;

    // ── Constructor ────────────────────────────────────────────────────────────
    public LoginForm()
    {
        ClientSize      = new Size(420, 520);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = BgDark;
        Font            = new Font("Segoe UI", 9f);
        MinimumSize     = MaximumSize = new Size(420, 520);

        // ── Title bar ──────────────────────────────────────────────────────────
        var bar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = BgTitle };
        bar.Controls.Add(new Panel { Location = new Point(0,0),   Size = new Size(3,38),  BackColor = Accent });
        bar.Controls.Add(new Panel { Location = new Point(14,11), Size = new Size(16,16), BackColor = Accent });
        var titleLbl = new Label
        {
            Text = "Makeshift Studios Launcher  —  Sign In",
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(200,195,220),
            Location = new Point(38, 0), Size = new Size(330, 38),
            TextAlign = ContentAlignment.MiddleLeft, Name = "titleLbl"
        };
        bar.Controls.Add(titleLbl);

        var closeX = MakeIconBtn("✕", new Point(382,0));
        closeX.FlatAppearance.MouseOverBackColor = Color.FromArgb(180,50,50);
        closeX.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        bar.Controls.Add(closeX);

        // ── Content container ─────────────────────────────────────────────────
        var content = new Panel { Dock = DockStyle.Fill, BackColor = BgDark };

        // ── Login panel ───────────────────────────────────────────────────────
        _loginPanel  = new Panel { Dock = DockStyle.Fill, BackColor = BgDark };
        _loginUser   = MakeField(new Point(40, 134), false);
        _loginPass   = MakeField(new Point(40, 212), true);
        _loginRemember = new CheckBox
        {
            Text = "Remember me", ForeColor = TextMute, Font = new Font("Segoe UI", 9f),
            Location = new Point(40, 264), Size = new Size(160, 22), Cursor = Cursors.Hand
        };
        _loginBtn   = MakeSubmitBtn("Sign In", ColGreen, new Point(40, 306));
        _loginError = MakeErrorLbl(new Point(40, 366));
        UIAnimator.HoverColor(_loginBtn, ColGreen, Color.FromArgb(70,170,90));
        _loginBtn.Click += LoginBtn_Click;

        var createLink = MakeLinkBtn("Create account", new Point(40, 412));
        var forgotLink = MakeLinkBtn("Forgot password?", new Point(248, 412));
        createLink.Click += (_, _) => ShowPanel(_createPanel!, "Create Account");
        forgotLink.Click += (_, _) => ShowPanel(_forgotPanel!,  "Reset Password");

        _loginPanel.Controls.AddRange(new Control[]
        {
            MakeHeading("Welcome back", new Point(40, 32)),
            MakeSubtitle("Sign in to your account", new Point(40, 72)),
            MakeLbl("Username", new Point(40, 114)),
            _loginUser,
            MakeLbl("Password", new Point(40, 192)),
            _loginPass,
            _loginRemember, _loginBtn, _loginError,
            createLink, forgotLink,
            MakeFooter()
        });

        // ── Create account panel ───────────────────────────────────────────────
        _createPanel = new Panel { Dock = DockStyle.Fill, BackColor = BgDark, Visible = false };
        _regUser     = MakeField(new Point(40, 114), false);
        _regPass     = MakeField(new Point(40, 192), true);
        _regConfirm  = MakeField(new Point(40, 270), true);
        _regBtn      = MakeSubmitBtn("Create Account", ColBlue, new Point(40, 320));
        _regError    = MakeErrorLbl(new Point(40, 380));
        UIAnimator.HoverColor(_regBtn, ColBlue, Color.FromArgb(70,145,225));
        _regBtn.Click += RegBtn_Click;

        var backFromReg = MakeLinkBtn("← Back to sign in", new Point(40, 426));
        backFromReg.Click += (_, _) => ShowPanel(_loginPanel, "Sign In");

        _createPanel.Controls.AddRange(new Control[]
        {
            MakeHeading("Create Account", new Point(40, 32)),
            MakeSubtitle("Join Makeshift Studios", new Point(40, 72)),
            MakeLbl("Username", new Point(40, 94)),
            _regUser,
            MakeLbl("Password", new Point(40, 172)),
            _regPass,
            MakeLbl("Confirm Password", new Point(40, 250)),
            _regConfirm,
            _regBtn, _regError,
            backFromReg,
            MakeFooter()
        });

        // ── Forgot password panel ──────────────────────────────────────────────
        _forgotPanel = new Panel { Dock = DockStyle.Fill, BackColor = BgDark, Visible = false };

        var forgotHeading  = MakeHeading("Reset Password", new Point(40, 32));
        var forgotSub      = MakeSubtitle("Password reset requires contacting support.", new Point(40, 72));

        var discordNote = new Label
        {
            Text      = "Join our Discord server and open a support ticket — an admin will reset your password for you.",
            ForeColor = TextMute,
            Font      = new Font("Segoe UI", 9.5f),
            Location  = new Point(40, 128),
            Size      = new Size(340, 60),
        };

        var discordBtn = MakeSubmitBtn("Open Discord", Color.FromArgb(88, 101, 242), new Point(40, 210));
        UIAnimator.HoverColor(discordBtn, Color.FromArgb(88,101,242), Color.FromArgb(110,120,255));
        discordBtn.Click += (_, _) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://discord.gg/makeshiftstudios",
                UseShellExecute = true
            });

        var separatorLbl = new Label
        {
            Text      = "— or —",
            ForeColor = Color.FromArgb(70,65,100),
            Font      = new Font("Segoe UI", 9f),
            Location  = new Point(40, 276),
            Size      = new Size(340, 20),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var emailNote = new Label
        {
            Text      = "Email: support@makeshiftstudios.com",
            ForeColor = Color.FromArgb(100,95,140),
            Font      = new Font("Segoe UI", 9f),
            Location  = new Point(40, 304),
            Size      = new Size(340, 20),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor    = Cursors.Hand
        };
        emailNote.Click += (_, _) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "mailto:support@makeshiftstudios.com",
                UseShellExecute = true
            });

        var backFromForgot = MakeLinkBtn("← Back to sign in", new Point(40, 426));
        backFromForgot.Click += (_, _) => ShowPanel(_loginPanel, "Sign In");

        _forgotPanel.Controls.AddRange(new Control[]
        {
            forgotHeading, forgotSub, discordNote, discordBtn,
            separatorLbl, emailNote, backFromForgot,
            MakeFooter()
        });

        // ── Assembly ──────────────────────────────────────────────────────────
        content.Controls.Add(_forgotPanel);
        content.Controls.Add(_createPanel);
        content.Controls.Add(_loginPanel);

        Controls.Add(content);
        Controls.Add(bar);

        AcceptButton = _loginBtn;
    }

    // ── Panel switching ────────────────────────────────────────────────────────
    private void ShowPanel(Panel target, string title)
    {
        _loginPanel.Visible  = false;
        _createPanel.Visible = false;
        _forgotPanel.Visible = false;
        target.Visible       = true;

        // Update title bar text
        var titleLbl = Controls.OfType<Panel>()
            .SelectMany(p => p.Controls.OfType<Label>())
            .FirstOrDefault(l => l.Name == "titleLbl");
        if (titleLbl != null)
            titleLbl.Text = $"Makeshift Studios Launcher  —  {title}";

        // Update AcceptButton
        AcceptButton = target == _createPanel ? _regBtn
                     : target == _loginPanel  ? _loginBtn
                     : null;

        // Clear errors on switch
        _loginError.Text = "";
        _regError.Text   = "";
    }

    // ── WndProc: drag only ─────────────────────────────────────────────────────
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0084)
        {
            int lp = m.LParam.ToInt32();
            var pt = PointToClient(new Point((short)lp, (short)(lp >> 16)));
            if (pt.Y >= 0 && pt.Y < 38) { m.Result = (IntPtr)2; return; }
        }
        base.WndProc(ref m);
    }

    // ── Handlers ──────────────────────────────────────────────────────────────
    private async void LoginBtn_Click(object? sender, EventArgs e)
    {
        _loginError.Text    = "";
        _loginBtn.Enabled   = false;
        _loginBtn.Text      = "Signing in…";

        var (ok, token, err) = await AuthService.LoginAsync(
            _loginUser.Text.Trim(), _loginPass.Text, _loginRemember.Checked);

        if (ok)
        {
            UserSession.SetCurrent(_loginUser.Text.Trim(), token!);
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _loginError.Text  = err ?? "Login failed.";
            _loginBtn.Enabled = true;
            _loginBtn.Text    = "Sign In";
        }
    }

    private async void RegBtn_Click(object? sender, EventArgs e)
    {
        _regError.Text   = "";
        _regBtn.Enabled  = false;
        _regBtn.Text     = "Creating account…";

        var (ok, token, err) = await AuthService.RegisterAsync(
            _regUser.Text.Trim(), _regPass.Text, _regConfirm.Text,
            rememberMe: true);

        if (ok)
        {
            UserSession.SetCurrent(_regUser.Text.Trim(), token!);
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _regError.Text  = err ?? "Registration failed.";
            _regBtn.Enabled = true;
            _regBtn.Text    = "Create Account";
        }
    }

    // ── Control factories ──────────────────────────────────────────────────────
    private static Label MakeHeading(string text, Point loc) => new Label
    {
        Text = text, Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold),
        ForeColor = TextPri, Location = loc, Size = new Size(340, 36), AutoSize = false
    };

    private static Label MakeSubtitle(string text, Point loc) => new Label
    {
        Text = text, Font = new Font("Segoe UI", 9.5f),
        ForeColor = TextMute, Location = loc, Size = new Size(340, 20), AutoSize = false
    };

    private static Label MakeLbl(string text, Point loc) => new Label
    {
        Text = text, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
        ForeColor = TextMute, Location = loc, Size = new Size(340, 18), AutoSize = false
    };

    private static TextBox MakeField(Point loc, bool password) => new TextBox
    {
        Location = loc, Size = new Size(340, 34),
        BackColor = BgField, ForeColor = TextPri,
        BorderStyle = BorderStyle.None,
        Font = new Font("Segoe UI", 10.5f),
        UseSystemPasswordChar = password
    };

    private static Button MakeSubmitBtn(string text, Color bg, Point loc)
    {
        var b = new Button
        {
            Text = text, Location = loc, Size = new Size(340, 46),
            FlatStyle = FlatStyle.Flat, BackColor = bg,
            ForeColor = Color.White, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold)
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private static Button MakeLinkBtn(string text, Point loc)
    {
        var b = new Button
        {
            Text = text, Location = loc, Size = new Size(160, 22),
            FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(110,100,170), Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9f), TextAlign = ContentAlignment.MiddleLeft
        };
        b.FlatAppearance.BorderSize      = 0;
        b.FlatAppearance.MouseOverBackColor = Color.Transparent;
        b.FlatAppearance.MouseDownBackColor = Color.Transparent;
        return b;
    }

    private static Button MakeIconBtn(string text, Point loc)
    {
        var b = new Button
        {
            Text = text, Location = loc, Size = new Size(38, 38),
            FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(140,135,160), Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9f)
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private static Label MakeErrorLbl(Point loc) => new Label
    {
        Text = "", ForeColor = ColRed, Font = new Font("Segoe UI", 9f),
        Location = loc, Size = new Size(340, 36), AutoSize = false
    };

    private static Label MakeFooter() => new Label
    {
        Text = "© Makeshift Studios", Font = new Font("Segoe UI", 8f),
        ForeColor = Color.FromArgb(60,55,90), Location = new Point(40, 490),
        Size = new Size(340, 16), TextAlign = ContentAlignment.MiddleRight
    };
}

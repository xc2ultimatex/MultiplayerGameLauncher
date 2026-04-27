namespace MultiplayerLauncher;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        RunAsync().GetAwaiter().GetResult();
    }

    static async Task RunAsync()
    {
        // Try DPAPI-saved credentials first
        var (autoOk, username, savedPassword) = AuthService.TryAutoLogin();
        if (autoOk)
        {
            var (loginOk, ticket, _) = await AuthService.LoginAsync(username!, savedPassword!, rememberMe: false);
            if (loginOk)
            {
                UserSession.SetCurrent(username!, ticket!);
            }
            else
            {
                // Saved credentials are stale — clear and force fresh login
                AuthService.ClearSavedSession();
                using var login = new LoginForm();
                if (login.ShowDialog() != DialogResult.OK)
                    return;
            }
        }
        else
        {
            using var login = new LoginForm();
            if (login.ShowDialog() != DialogResult.OK)
                return;
        }

        Application.Run(new Form1());
    }
}

using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MultiplayerLauncher;

internal static class AuthService
{
    private const string BrokerHost = "74.128.161.112";
    private const int    BrokerPort = 7777;
    private const int ConnectTimeoutMs  = 8_000;
    private const int ResponseTimeoutMs = 10_000;

    private static readonly string SessionPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "MakeshiftStudios", "launcher_session.bin");

    // ── Login ──────────────────────────────────────────────────────────────────

    public static async Task<(bool Success, string? Token, string? Error)>
        LoginAsync(string username, string password, bool rememberMe)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return (false, null, "Username and password are required.");

        try
        {
            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync(BrokerHost, BrokerPort);
            if (await Task.WhenAny(connectTask, Task.Delay(ConnectTimeoutMs)) != connectTask)
                return (false, null, "Could not connect to server. Try again.");
            await connectTask;

            using var stream = tcp.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            await writer.WriteLineAsync($"LOGIN|{EncodeValue(username.Trim())}|{EncodeValue(password)}");

            // Read until AUTH_OK or AUTH_FAIL (broker first sends ASSIGN|{id})
            string? authUser = null;
            for (int i = 0; i < 8; i++)
            {
                string? line = await ReadLineAsync(reader);
                if (line == null) return (false, null, "No response from server.");
                string[] p = line.Split('|');
                if (p[0] == "AUTH_OK")  { authUser = p.Length > 1 ? DecodeValue(p[1]) : username.Trim(); break; }
                if (p[0] == "AUTH_FAIL") { string err = p.Length > 1 ? DecodeValue(p[1]) : "Invalid credentials."; return (false, null, err); }
            }
            if (authUser == null) return (false, null, "No auth response from server.");

            if (rememberMe) SaveSession(username.Trim(), password);
            return (true, authUser, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Connection error: {ex.Message}");
        }
    }

    // ── Register ───────────────────────────────────────────────────────────────

    public static async Task<(bool Success, string? Token, string? Error)>
        RegisterAsync(string username, string password, string confirmPassword, bool rememberMe)
    {
        if (string.IsNullOrWhiteSpace(username))
            return (false, null, "Username is required.");
        if (username.Length < 3 || username.Length > 24)
            return (false, null, "Username must be 3–24 characters.");
        if (string.IsNullOrWhiteSpace(password))
            return (false, null, "Password is required.");
        if (password.Length < 6)
            return (false, null, "Password must be at least 6 characters.");
        if (password != confirmPassword)
            return (false, null, "Passwords do not match.");

        try
        {
            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync(BrokerHost, BrokerPort);
            if (await Task.WhenAny(connectTask, Task.Delay(ConnectTimeoutMs)) != connectTask)
                return (false, null, "Could not connect to server. Try again.");
            await connectTask;

            using var stream = tcp.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            await writer.WriteLineAsync($"REGISTER|{EncodeValue(username.Trim())}|{EncodeValue(password)}");

            string? authUser = null;
            for (int i = 0; i < 8; i++)
            {
                string? line = await ReadLineAsync(reader);
                if (line == null) return (false, null, "No response from server.");
                string[] p = line.Split('|');
                if (p[0] == "AUTH_OK")   { authUser = p.Length > 1 ? DecodeValue(p[1]) : username.Trim(); break; }
                if (p[0] == "AUTH_FAIL") { string err = p.Length > 1 ? DecodeValue(p[1]) : "Registration failed."; return (false, null, err); }
            }
            if (authUser == null) return (false, null, "No auth response from server.");

            if (rememberMe) SaveSession(username.Trim(), password);
            return (true, authUser, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Connection error: {ex.Message}");
        }
    }

    // ── Auto-login ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns saved credentials (username + password). The caller must call
    /// LoginAsync with these to obtain a fresh launch ticket for the current session.
    /// </summary>
    public static (bool Success, string? Username, string? Password) TryAutoLogin()
    {
        try
        {
            if (!File.Exists(SessionPath)) return (false, null, null);
            byte[] enc  = File.ReadAllBytes(SessionPath);
            byte[] dec  = ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser);
            var    sess = JsonSerializer.Deserialize<SavedSession>(Encoding.UTF8.GetString(dec));
            if (sess is null || string.IsNullOrEmpty(sess.Credential)) return (false, null, null);
            return (true, sess.Username, sess.Credential);
        }
        catch { return (false, null, null); }
    }

    public static void ClearSavedSession()
    {
        try { if (File.Exists(SessionPath)) File.Delete(SessionPath); } catch { }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static void SaveSession(string username, string password)
    {
        try
        {
            byte[] plain = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(new SavedSession(username, password)));
            byte[] enc = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(SessionPath)!);
            File.WriteAllBytes(SessionPath, enc);
        }
        catch { }
    }

    private static async Task<string?> ReadLineAsync(StreamReader reader)
    {
        using var cts = new CancellationTokenSource(ResponseTimeoutMs);
        try { return await reader.ReadLineAsync(cts.Token); }
        catch { return null; }
    }

    private static string EncodeValue(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

    private static string DecodeValue(string encoded)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(encoded)); }
        catch { return string.Empty; }
    }

    private sealed record SavedSession(string Username, string Credential);
}

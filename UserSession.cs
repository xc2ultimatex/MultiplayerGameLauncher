namespace MultiplayerLauncher;

internal static class UserSession
{
    public static string Username { get; private set; } = "";
    public static string Token    { get; private set; } = "";
    public static bool   IsLoggedIn => !string.IsNullOrEmpty(Token);

    internal static void SetCurrent(string username, string token)
    {
        Username = username;
        Token    = token;
    }

    internal static void Clear()
    {
        Username = "";
        Token    = "";
    }
}

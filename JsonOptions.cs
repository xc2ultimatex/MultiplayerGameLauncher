using System.Text.Json;

namespace MultiplayerLauncher;

internal static class JsonOptions
{
    public static JsonSerializerOptions Default { get; } = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static JsonSerializerOptions WriteIndented { get; } = new()
    {
        WriteIndented = true
    };
}

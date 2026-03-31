using System.Text.Json.Serialization;

namespace MultiplayerLauncher;

public sealed class UpdateManifest
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("packageDirectory")]
    public string? PackageDirectory { get; init; }

    [JsonPropertyName("launchExecutable")]
    public string? LaunchExecutable { get; init; }
}

using System.Text.Json.Serialization;

namespace Bootstrap;

public sealed class BootstrapSettings
{
    /// <summary>GitHub repo in "owner/repo" format.</summary>
    [JsonPropertyName("githubRepo")]
    public string GitHubRepo { get; init; } = "OWNER/REPO";

    /// <summary>Name of the exe asset in the GitHub release.</summary>
    [JsonPropertyName("assetName")]
    public string AssetName { get; init; } = "MultiplayerLauncher.exe";

    /// <summary>Local filename of the main launcher exe.</summary>
    [JsonPropertyName("launcherExeName")]
    public string LauncherExeName { get; init; } = "MultiplayerLauncher.exe";

    public static BootstrapSettings Default => new();
}

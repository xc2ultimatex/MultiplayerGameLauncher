using System.Text.Json.Serialization;

namespace MultiplayerLauncher;

public sealed class LauncherSettings
{
    [JsonPropertyName("updateSourceDirectory")]
    public string UpdateSourceDirectory { get; init; } = "http://74.128.161.157:8080/Latest";

    [JsonPropertyName("manifestFileName")]
    public string ManifestFileName { get; init; } = "manifest.json";

    [JsonPropertyName("packageDirectoryName")]
    public string PackageDirectoryName { get; init; } = "payload";

    [JsonPropertyName("gameDirectoryName")]
    public string GameDirectoryName { get; init; } = "Game";

    [JsonPropertyName("gameExecutableRelativePath")]
    public string GameExecutableRelativePath { get; init; } = "";

    [JsonPropertyName("localVersionFileName")]
    public string LocalVersionFileName { get; init; } = "version.txt";

    public static LauncherSettings Default { get; } = new();
}

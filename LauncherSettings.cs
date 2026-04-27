using System.Text.Json.Serialization;

namespace MultiplayerLauncher;

public sealed class LauncherSettings
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "Game";

    [JsonPropertyName("updateSourceDirectory")]
    public string UpdateSourceDirectory { get; init; } = "";

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

    [JsonPropertyName("patchNotes")]
    public string PatchNotes { get; init; } = "- No patch notes available yet.";
}

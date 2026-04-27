using System.Text.Json.Serialization;

namespace MultiplayerLauncher;

public sealed class LauncherConfig
{
    [JsonPropertyName("games")]
    public List<LauncherSettings> Games { get; init; } = new();

    public static LauncherConfig Default { get; } = new()
    {
        Games = new List<LauncherSettings>
        {
            new LauncherSettings
            {
                Name                     = "Shop Game",
                UpdateSourceDirectory    = "http://74.128.161.112:8080/Latest",
                ManifestFileName         = "manifest.json",
                PackageDirectoryName     = "payload",
                GameDirectoryName        = "ShopGame",
                GameExecutableRelativePath = "",
                LocalVersionFileName     = "version.txt"
            },
            new LauncherSettings
            {
                Name                     = "Camgirl Management Simulator",
                UpdateSourceDirectory    = "http://DEV-MACHINE/CamgirlSim",
                ManifestFileName         = "manifest.json",
                PackageDirectoryName     = "payload",
                GameDirectoryName        = "CamgirlSim",
                GameExecutableRelativePath = "",
                LocalVersionFileName     = "version.txt"
            }
        }
    };
}

namespace MultiplayerLauncher;

public sealed class LauncherStatus
{
    public bool IsConfigured { get; init; }
    public bool RemoteManifestAvailable { get; init; }
    public bool UpdateAvailable { get; init; }
    public bool CanLaunch { get; init; }
    public bool CanUpdateOrInstall { get; init; }
    public string SourceDirectory { get; init; } = string.Empty;
    public string? LocalVersion { get; init; }
    public string? RemoteVersion { get; init; }
    public string? LaunchPath { get; init; }
}

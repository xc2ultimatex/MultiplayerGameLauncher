namespace MultiplayerLauncher;

public sealed class UpdateResult
{
    public bool Updated { get; init; }
    public bool Launched { get; init; }
    public string Message { get; init; } = string.Empty;
}

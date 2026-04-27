using System.Text.Json.Serialization;

namespace Bootstrap;

public sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = string.Empty;

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; init; } = new();
}

public sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; init; } = string.Empty;
}

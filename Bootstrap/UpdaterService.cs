using System.Text.Json;

namespace Bootstrap;

internal static class UpdaterService
{
    private static readonly HttpClient Http = new();

    static UpdaterService()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("MultiplayerLauncher-Bootstrap/1.0");
        Http.Timeout = TimeSpan.FromSeconds(30);
    }

    public static async Task<GitHubRelease?> FetchLatestReleaseAsync(string repo)
    {
        string url = $"https://api.github.com/repos/{repo}/releases/latest";
        using HttpResponseMessage response = await Http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;

        string json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GitHubRelease>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    /// <summary>
    /// Downloads the asset to a temp file then atomically replaces targetPath.
    /// Reports download progress 0-100 via the callback.
    /// </summary>
    public static async Task DownloadAndReplaceAsync(
        string downloadUrl,
        string targetPath,
        Action<int> onProgress)
    {
        string tempPath   = targetPath + ".new";
        string backupPath = targetPath + ".old";

        // Stream download to temp file
        using HttpResponseMessage response = await Http.GetAsync(
            downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength ?? 0;

        await using (Stream src  = await response.Content.ReadAsStreamAsync())
        await using (FileStream dst = File.Create(tempPath))
        {
            byte[] buf = new byte[81_920];
            long downloaded = 0;
            int  read;

            while ((read = await src.ReadAsync(buf)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, read));
                downloaded += read;
                if (total > 0)
                    onProgress((int)(downloaded * 100L / total));
            }
        }

        // Atomic swap: old → .old, .new → target
        if (File.Exists(backupPath)) File.Delete(backupPath);
        if (File.Exists(targetPath)) File.Move(targetPath, backupPath);
        File.Move(tempPath, targetPath);
        if (File.Exists(backupPath)) try { File.Delete(backupPath); } catch { }
    }
}

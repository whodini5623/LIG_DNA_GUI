using System.IO;
using System.Net.Http;
using System.Text.Json;
using BroadcastControl.App.Models;

namespace BroadcastControl.App.Services;

public sealed class RecordedVideoCatalogService
{
    private const string DefaultVideoUrl = "http://192.168.3.143:8090/";
    private const string CacheFolderName = "LIG_DNA_GUI_recorded_videos";
    private static readonly HttpClient HttpClient = new();

    public async Task<IReadOnlyList<RecordedVideoItem>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var baseUri = GetBaseUri();
        using var stream = await HttpClient.GetStreamAsync(new Uri(baseUri, "api/videos"), cancellationToken);
        var videos = await JsonSerializer.DeserializeAsync<List<RecordedVideoItem>>(
            stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken)
            ?? new List<RecordedVideoItem>();

        foreach (var video in videos)
        {
            video.Folder = GetFolder(video.Name);
            video.DisplayName = GetFileName(video.Name);
            if (!Uri.TryCreate(video.Url, UriKind.Absolute, out _))
                video.Url = new Uri(baseUri, video.Url).ToString();
        }

        return videos;
    }

    public async Task<string> CacheAsync(RecordedVideoItem item, CancellationToken cancellationToken = default)
    {
        var cacheDirectory = Path.Combine(Path.GetTempPath(), CacheFolderName);
        Directory.CreateDirectory(cacheDirectory);
        var localPath = Path.Combine(cacheDirectory, SanitizeFileName(item.Name));
        if (File.Exists(localPath))
        {
            var localSize = new FileInfo(localPath).Length;
            if (item.SizeBytes <= 0 || localSize == item.SizeBytes) return localPath;
        }

        var bytes = await HttpClient.GetByteArrayAsync(item.Url, cancellationToken);
        await File.WriteAllBytesAsync(localPath, bytes, cancellationToken);
        return localPath;
    }

    public Uri GetLogsUri() => new(GetBaseUri(), "api/logs");

    private static Uri GetBaseUri()
    {
        var videoUrl = Environment.GetEnvironmentVariable("JETSON_VIDEO_URL");
        if (string.IsNullOrWhiteSpace(videoUrl)) videoUrl = DefaultVideoUrl;
        if (!videoUrl.EndsWith("/", StringComparison.Ordinal)) videoUrl += "/";
        return new Uri(videoUrl, UriKind.Absolute);
    }

    private static string GetFolder(string name)
    {
        var normalized = name.Replace('\\', '/');
        var separator = normalized.LastIndexOf('/');
        return separator > 0 ? normalized[..separator] : "\uAE30\uC874 \uC601\uC0C1";
    }

    private static string GetFileName(string name)
    {
        var normalized = name.Replace('\\', '/');
        var separator = normalized.LastIndexOf('/');
        return separator >= 0 && separator < normalized.Length - 1
            ? normalized[(separator + 1)..]
            : normalized;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "recorded_video" : sanitized;
    }
}

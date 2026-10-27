using System.Net.Http;
using System.Text;
using System.Text.Json;
using BroadcastControl.App.Models;

namespace BroadcastControl.App.Services;

public sealed class RecordingMetadataService
{
    private readonly RecordedVideoCatalogService _catalogService;
    private readonly LogExportService _logExportService;
    private static readonly HttpClient HttpClient = new();

    public RecordingMetadataService(
        RecordedVideoCatalogService catalogService,
        LogExportService logExportService)
    {
        _catalogService = catalogService;
        _logExportService = logExportService;
    }

    public async Task SaveAsync(
        IEnumerable<AnalysisItem> analysisItems,
        IEnumerable<SystemLogItem> systemLogs,
        DateTime windowStart,
        DateTime windowEnd,
        bool manual,
        bool includeAnalysis,
        bool includeSystemLog,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?> { ["manual"] = manual };
        if (includeAnalysis)
            payload["analysisText"] = _logExportService.BuildAnalysisSnapshot(
                analysisItems, windowStart, windowEnd, includeAll: manual);
        if (includeSystemLog)
            payload["systemLogText"] = _logExportService.BuildSystemLogSnapshot(
                systemLogs, windowStart, windowEnd, includeAll: manual);

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await HttpClient.PostAsync(
            _catalogService.GetLogsUri(), content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

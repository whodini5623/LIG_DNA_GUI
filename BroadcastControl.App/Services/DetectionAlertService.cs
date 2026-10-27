using BroadcastControl.App.Models;

namespace BroadcastControl.App.Services;

public sealed class DetectionAlertService
{
    private static readonly TimeSpan AlertCooldown = TimeSpan.FromSeconds(10);
    private readonly MobileAlertHubService _mobileAlertHubService;
    private string? _lastSignature;
    private DateTimeOffset _lastSentAt = DateTimeOffset.MinValue;

    public DetectionAlertService(MobileAlertHubService mobileAlertHubService)
    {
        _mobileAlertHubService = mobileAlertHubService;
    }

    public async Task<bool> TryPublishAsync(
        string target,
        uint frameId,
        IReadOnlyList<DetectionInfo> detections,
        string analysis,
        string detectionSummary,
        string threatLevel,
        byte[]? evidencePng)
    {
        if (detections.Count == 0) return false;

        var overlaySignature = string.Join(";", detections.Select(d =>
            $"{d.ClassName}:{d.ObjectId}:{d.X1:0.##}:{d.Y1:0.##}:{d.X2:0.##}:{d.Y2:0.##}"));
        var signature = $"{target}:{frameId}:{overlaySignature}";
        var now = DateTimeOffset.Now;
        if (string.Equals(_lastSignature, signature, StringComparison.Ordinal) || now - _lastSentAt < AlertCooldown)
            return false;

        await _mobileAlertHubService.PublishAlertAsync(
            "\uC774\uB3D9\uD615 \uD1B5\uC81C \uC704\uD5D8 \uC54C\uB9BC", analysis, detectionSummary, threatLevel, evidencePng);
        _lastSignature = signature;
        _lastSentAt = now;
        return true;
    }
}

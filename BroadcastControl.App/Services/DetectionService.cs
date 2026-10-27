using BroadcastControl.App.Models;

namespace BroadcastControl.App.Services;

/// <summary>
/// Keeps the recent camera and detection packets and applies target/score filtering.
/// UI updates, overlay drawing, and alert delivery stay with their respective owners.
/// </summary>
public sealed class DetectionService
{
    private const int CacheLimit = 48;
    private const uint FrameTolerance = 12;
    private const float DisplayScoreThreshold = 0.60f;

    private static readonly HashSet<string> NonMilitaryTargetClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chair", "dining table", "tv", "laptop", "cell phone", "bottle", "couch", "bench", "refrigerator"
    };

    private static readonly HashSet<string> CompositeTargetClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "person", "airplane", "bicycle", "car", "motorcycle", "bus", "truck", "train", "boat",
        "cell phone", "laptop", "chair", "dining table", "tv", "couch", "bench", "bottle", "refrigerator"
    };

    private readonly Dictionary<uint, ReceivedVideoFrame> _eoFrameCache = new();
    private readonly Dictionary<uint, ReceivedVideoFrame> _irFrameCache = new();
    private readonly Dictionary<uint, DetectionPacket> _eoDetectionCache = new();
    private readonly Dictionary<uint, DetectionPacket> _irDetectionCache = new();
    private bool _hasReceivedDetectionPacket;
    private bool _hasReceivedNonEmptyDetectionPacket;
    private string? _lastFilteredOutTargetSignature;
    private string? _lastDetectionAlertSignature;

    public bool HasReceivedDetectionPacket => _hasReceivedDetectionPacket;

    public bool HasReceivedNonEmptyDetectionPacket => _hasReceivedNonEmptyDetectionPacket;

    public string? LastFilteredOutTargetSignature => _lastFilteredOutTargetSignature;

    public string? LastDetectionAlertSignature => _lastDetectionAlertSignature;

    public ReceivedVideoFrame? LatestEoFrame { get; private set; }

    public ReceivedVideoFrame? LatestIrFrame { get; private set; }

    public void StoreEoFrame(ReceivedVideoFrame frame)
    {
        LatestEoFrame = frame;
        CacheFrame(frame, _eoFrameCache);
    }

    public void StoreIrFrame(ReceivedVideoFrame frame)
    {
        LatestIrFrame = frame;
        CacheFrame(frame, _irFrameCache);
    }

    public IReadOnlyList<DetectionInfo> StoreAndFilterDetections(
        bool isEo,
        DetectionPacket packet,
        string primaryTarget)
    {
        var cache = isEo ? _eoDetectionCache : _irDetectionCache;
        cache[packet.FrameId] = packet;
        TrimCache(cache);
        _hasReceivedDetectionPacket = true;

        var filtered = packet.Detections.Where(item => ShouldDisplay(item, primaryTarget)).ToArray();
        if (filtered.Length > 0)
        {
            _hasReceivedNonEmptyDetectionPacket = true;
        }

        _lastFilteredOutTargetSignature = packet.Detections.Count > 0 && filtered.Length == 0
            ? $"{primaryTarget}:{packet.FrameId}"
            : null;

        return filtered;
    }

    public void UpdateDetectionAlertSignature(uint frameId, IReadOnlyList<DetectionInfo> detections)
    {
        _lastDetectionAlertSignature = detections.Count == 0 ? null : $"{frameId}:{detections.Count}";
    }

    public bool TryGetCachedFrame(bool isEo, uint frameIndex, out ReceivedVideoFrame frame)
    {
        return (isEo ? _eoFrameCache : _irFrameCache).TryGetValue(frameIndex, out frame);
    }

    public bool TryGetRenderableFrameAndDetection(
        bool isEo,
        out ReceivedVideoFrame frame,
        out DetectionPacket detectionPacket)
    {
        var latestFrame = isEo ? LatestEoFrame : LatestIrFrame;
        var frameCache = isEo ? _eoFrameCache : _irFrameCache;
        var detectionCache = isEo ? _eoDetectionCache : _irDetectionCache;

        if (latestFrame is { } latest && detectionCache.TryGetValue(latest.FrameIndex, out detectionPacket))
        {
            frame = latest;
            return true;
        }

        foreach (var pair in detectionCache
                     .Where(pair => pair.Value.Detections.Count > 0)
                     .OrderByDescending(pair => pair.Key))
        {
            if (frameCache.TryGetValue(pair.Key, out frame))
            {
                detectionPacket = pair.Value;
                return true;
            }
        }

        if (latestFrame is { } current && TryGetRecentDetection(current.FrameIndex, detectionCache, out detectionPacket))
        {
            frame = current;
            return true;
        }

        frame = default;
        detectionPacket = default;
        return false;
    }

    public static IReadOnlyList<DetectionInfo> FilterDisplayDetections(
        IReadOnlyList<DetectionInfo> detections,
        string primaryTarget)
    {
        return detections.Where(item => ShouldDisplay(item, primaryTarget)).ToArray();
    }

    private static bool ShouldDisplay(DetectionInfo detection, string primaryTarget)
    {
        if (detection.Score < DisplayScoreThreshold)
        {
            return false;
        }

        var className = detection.ClassName.ToLowerInvariant();
        return primaryTarget switch
        {
            "\uBCF5\uD569" => true,
            "\uC0AC\uB78C" => className == "person",
            "\uBB34\uAE30\uCCB4\uACC4" => CompositeTargetClasses.Contains(className),
            "\uD1B5\uC2E0 \uC7A5\uBE44" => className is "cell phone" or "laptop",
            "\uBE44\uAD70\uC0AC \uD45C\uC801" => NonMilitaryTargetClasses.Contains(className),
            _ => true
        };
    }

    private static bool TryGetRecentDetection(
        uint currentFrameIndex,
        Dictionary<uint, DetectionPacket> cache,
        out DetectionPacket detectionPacket)
    {
        foreach (var candidate in cache
                     .Where(pair => pair.Value.Detections.Count > 0 && pair.Key <= currentFrameIndex)
                     .OrderByDescending(pair => pair.Key))
        {
            if (currentFrameIndex - candidate.Key > FrameTolerance)
            {
                break;
            }

            detectionPacket = candidate.Value;
            return true;
        }

        var fallback = cache
            .Where(pair => pair.Value.Detections.Count > 0)
            .OrderByDescending(pair => pair.Key)
            .FirstOrDefault();
        if (fallback.Value.Detections is not null)
        {
            detectionPacket = fallback.Value;
            return true;
        }

        detectionPacket = default;
        return false;
    }

    private static void CacheFrame(ReceivedVideoFrame frame, Dictionary<uint, ReceivedVideoFrame> cache)
    {
        cache[frame.FrameIndex] = frame;
        TrimCache(cache);
    }

    private static void TrimCache<T>(Dictionary<uint, T> cache)
    {
        while (cache.Count > CacheLimit)
        {
            cache.Remove(cache.Keys.Min());
        }
    }
}

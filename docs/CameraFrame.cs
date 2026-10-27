namespace BroadcastControl.App.ViewModels;
using BroadcastControl.App.Services;
using System.Windows.Media;

public sealed partial class MainViewModel
{
    private ReceivedVideoFrame? _latestEoFrame;
    private ReceivedVideoFrame? _latestIrFrame;
    internal ReceivedVideoFrame? LatestEoFrame => _latestEoFrame;
    internal ReceivedVideoFrame? LatestIrFrame => _latestIrFrame;
    private readonly Dictionary<uint, ReceivedVideoFrame> _eoFrameCache = new();
    private readonly Dictionary<uint, ReceivedVideoFrame> _irFrameCache = new();
    private const int OverlayCacheLimit = 48;
    private bool _hasReceivedEoFrame;
    private bool _hasReceivedIrFrame;
    internal void OnEoFrameReady(ReceivedVideoFrame frame) // EO 프레임 상태와 바인딩 이미지 갱신
    {
        _latestEoFrame = frame;
        CacheFrame(frame, _eoFrameCache);

        if (!_hasReceivedEoFrame)
        {
            _hasReceivedEoFrame = true;
        }

        this.UpdateEoFrame(frame.Bitmap);
    }
    internal void OnIrFrameReady(ReceivedVideoFrame frame) // IR 프레임 상태와 바인딩 이미지 갱신
    {   
        _latestIrFrame = frame;
        CacheFrame(frame, _irFrameCache);

        if (!_hasReceivedIrFrame)
        {
            _hasReceivedIrFrame = true;
        }

        this.UpdateIrFrame(frame.Bitmap);
    }

    internal void CacheFrame(ReceivedVideoFrame frame, Dictionary<uint, ReceivedVideoFrame> frameCache)
    {
        frameCache[frame.FrameIndex] = frame;
        TrimCache(frameCache);
    }
    internal bool TryGetCachedFrame(
        bool isEoPrimary,
        uint frameIndex,
        out ReceivedVideoFrame frame)
    {
        var frameCache = isEoPrimary ? _eoFrameCache : _irFrameCache;
        return frameCache.TryGetValue(frameIndex, out frame);
    }

    private static void TrimCache<T>(Dictionary<uint, T> cache)
    {
        while (cache.Count > OverlayCacheLimit)
        {
            var oldestKey = cache.Keys.Min();
            cache.Remove(oldestKey);
        }
    }
    public void UpdateEoFrame(ImageSource? frame)
    {
        _eoFrame = frame;
        OnPropertyChanged(nameof(LargeFeedImage));
        OnPropertyChanged(nameof(InsetFeedImage));
    }
    public void UpdateIrFrame(ImageSource? frame)
    {
        _irFrame = frame;
        OnPropertyChanged(nameof(LargeFeedImage));
        OnPropertyChanged(nameof(InsetFeedImage));
    }
}
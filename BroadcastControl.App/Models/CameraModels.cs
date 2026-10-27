using System.Windows.Media.Imaging;

namespace BroadcastControl.App.Models;

public readonly record struct ReceivedVideoFrame(
    ulong StampNs,
    uint FrameIndex,
    ushort Width,
    ushort Height,
    BitmapSource Bitmap);

public readonly record struct PlaybackSegmentInfo(
    uint ClipIndex,
    uint ClipCount,
    uint SegmentStartSeconds,
    uint SegmentEndSeconds,
    uint CurrentPlaybackSeconds,
    uint CycleIndex)
{
    public string ToLogMessage() =>
        $"MEVA video segment changed: clip {ClipIndex}/{ClipCount} now playing {FormatTime(SegmentStartSeconds)} ~ {FormatTime(SegmentEndSeconds)}";

    public string ToLoopRestartLogMessage() =>
        $"MEVA video segment replay restarted: clip {ClipIndex}/{ClipCount} now replaying {FormatTime(SegmentStartSeconds)} ~ {FormatTime(SegmentEndSeconds)}";

    public string GetSignature() =>
        $"{ClipIndex}:{ClipCount}:{SegmentStartSeconds}:{SegmentEndSeconds}";

    private static string FormatTime(uint totalSeconds)
    {
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;
        return $"{hours:00}:{minutes:00}:{seconds:00}";
    }
}

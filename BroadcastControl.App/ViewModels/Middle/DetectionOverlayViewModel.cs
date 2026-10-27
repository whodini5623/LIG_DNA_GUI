namespace BroadcastControl.App.ViewModels.Middle;

using System.ComponentModel;
using BroadcastControl.App.Models;

/// <summary>
/// View state and actions for the corresponding DetectionOverlay.
/// </summary>
public sealed class DetectionOverlayViewModel : INotifyPropertyChanged
{
    private bool _hasTrackedTarget;
    private IReadOnlyList<DetectionInfo> _displayDetections = Array.Empty<DetectionInfo>();
    private int _sourceWidth = 1;
    private int _sourceHeight = 1;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool HasTrackedTarget => _hasTrackedTarget;

    public IReadOnlyList<DetectionInfo> DisplayDetections => _displayDetections;

    public int SourceWidth => _sourceWidth;

    public int SourceHeight => _sourceHeight;

    public void SetDisplayDetections(IReadOnlyList<DetectionInfo> detections, int sourceWidth, int sourceHeight)
    {
        _displayDetections = detections;
        _sourceWidth = Math.Max(1, sourceWidth);
        _sourceHeight = Math.Max(1, sourceHeight);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayDetections)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SourceWidth)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SourceHeight)));
    }

    public bool UpdateDetectionState(IReadOnlyList<DetectionInfo> detections)
    {
        var hasTrackedTarget = detections.Count > 0;
        if (_hasTrackedTarget == hasTrackedTarget)
        {
            return false;
        }

        _hasTrackedTarget = hasTrackedTarget;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasTrackedTarget)));
        return true;
    }
}

namespace BroadcastControl.App.ViewModels.Right;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using BroadcastControl.App.Services;

/// <summary>
/// View state and actions for the corresponding RecordingPanel.
/// </summary>
public sealed class RecordingPanelViewModel : INotifyPropertyChanged
{
    private readonly ViewportRecordingService _viewportRecordingService;
    private Func<byte[]?>? _evidenceCapture;
    private static readonly Brush RecordingOnBrush = CreateBrush(0xFF, 0x4D, 0x4F);
    private static readonly Brush RecordingOffBrush = CreateBrush(0x41, 0x49, 0x55);
    private static readonly Brush RecordingTextOffBrush = CreateBrush(0x92, 0x9D, 0xAA);
    private bool _isSystemPoweredOn = true;
    private bool _isManualMode;
    private bool _isManualRecordingEnabled;
    private bool _isRecordingSuppressed;
    private bool _isAutoRecordingLatched;

    public RecordingPanelViewModel(ViewportRecordingService viewportRecordingService)
    {
        _viewportRecordingService = viewportRecordingService;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsManualRecordingEnabled => _isManualRecordingEnabled;

    public bool IsRecordingActive =>
        _isSystemPoweredOn &&
        !_isRecordingSuppressed &&
        (_isManualRecordingEnabled || _isAutoRecordingLatched);

    public Brush RecordingIndicatorBrush => _isSystemPoweredOn ? RecordingOnBrush : RecordingOffBrush;

    public Brush RecordingTextBrush => _isSystemPoweredOn ? RecordingOnBrush : RecordingTextOffBrush;

    public double RecordingIndicatorOpacity => _isSystemPoweredOn ? 1.0 : 0.42;

    public void SetEvidenceCaptureProvider(Func<byte[]?> captureProvider) => _evidenceCapture = captureProvider;

    public string? UpdateRecordingServiceState(bool shouldRecord)
    {
        if (_evidenceCapture is null) return null;
        if (shouldRecord)
        {
            if (_viewportRecordingService.IsRecording) return null;
            var path = _viewportRecordingService.StartRecordingToDesktop(_evidenceCapture);
            return $"Recording started: {path}";
        }

        if (!_viewportRecordingService.IsRecording) return null;
        var savedPath = _viewportRecordingService.StopRecording();
        if (string.IsNullOrWhiteSpace(savedPath)) return null;
        if (File.Exists(savedPath))
            return $"Video saved: {savedPath} ({_viewportRecordingService.RecordedFrameCount} frames)";
        if (!string.IsNullOrWhiteSpace(_viewportRecordingService.LastRecordingErrorMessage))
            return $"Video save failed: {_viewportRecordingService.LastRecordingErrorMessage}";
        return $"Video file was not created: {savedPath} ({_viewportRecordingService.RecordedFrameCount} frames)";
    }

    public void UpdateContext(bool isSystemPoweredOn, bool isManualMode, bool isAutoMode, bool isHighThreat)
    {
        _isSystemPoweredOn = isSystemPoweredOn;
        _isManualMode = isManualMode;

        if (isHighThreat && isAutoMode && !_isRecordingSuppressed)
        {
            _isAutoRecordingLatched = true;
        }

        if (!isManualMode)
        {
            _isManualRecordingEnabled = false;
        }

        NotifyRecordingState();
    }

    public void ToggleManualRecording()
    {
        if (!_isSystemPoweredOn)
        {
            return;
        }

        if (IsRecordingActive)
        {
            _isRecordingSuppressed = true;
            _isAutoRecordingLatched = false;
            _isManualRecordingEnabled = false;
        }
        else
        {
            _isRecordingSuppressed = false;
            _isManualRecordingEnabled = true;
        }

        NotifyRecordingState();
    }

    private void NotifyRecordingState()
    {
        OnPropertyChanged(nameof(IsManualRecordingEnabled));
        OnPropertyChanged(nameof(IsRecordingActive));
        OnPropertyChanged(nameof(RecordingIndicatorBrush));
        OnPropertyChanged(nameof(RecordingTextBrush));
        OnPropertyChanged(nameof(RecordingIndicatorOpacity));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static Brush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

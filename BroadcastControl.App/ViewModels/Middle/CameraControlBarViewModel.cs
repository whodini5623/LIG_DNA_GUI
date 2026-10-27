namespace BroadcastControl.App.ViewModels.Middle;

using System.ComponentModel;
using System.Runtime.CompilerServices;

/// <summary>
/// View state and actions for the corresponding CameraControlBar.
/// </summary>
public sealed class CameraControlBarViewModel : INotifyPropertyChanged
{
    private const double MiniMapWidth = 130;
    private const double MiniMapHeight = 74;
    private readonly Func<string, string> _text;
    private double _brightness = 50;
    private double _contrast = 50;
    private double _zoomLevel = 1.0;
    private double _zoomPanX;
    private double _zoomPanY;
    private double _viewportWidth = 1;
    private double _viewportHeight = 1;
    private bool _isSystemPoweredOn = true;

    public CameraControlBarViewModel(Func<string, string>? text = null)
    {
        _text = text ?? (key => key);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public double Brightness
    {
        get => _brightness;
        set
        {
            if (_brightness.Equals(value)) return;
            _brightness = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BrightnessText));
        }
    }

    public string BrightnessText => $"{_text("Brightness")} {Brightness:0}%";

    public double Contrast
    {
        get => _contrast;
        set
        {
            if (_contrast.Equals(value)) return;
            _contrast = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ContrastText));
        }
    }

    public string ContrastText => $"{_text("Contrast")} {Contrast:0}%";

    public double ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            var clamped = Math.Clamp(value, 1.0, 4.0);
            if (_zoomLevel.Equals(clamped)) return;
            _zoomLevel = clamped;
            if (_zoomLevel <= 1.0)
            {
                _zoomPanX = 0;
                _zoomPanY = 0;
                OnPropertyChanged(nameof(ZoomTransformX));
                OnPropertyChanged(nameof(ZoomTransformY));
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(ZoomLevelText));
            OnPropertyChanged(nameof(LargeFeedScale));
            OnPropertyChanged(nameof(ShowZoomMiniMap));
            UpdateMiniMapViewport();
        }
    }

    public string ZoomLevelText => $"x{ZoomLevel:0.00}";

    public double LargeFeedScale => ZoomLevel;

    public double ZoomTransformX => _zoomPanX;

    public double ZoomTransformY => _zoomPanY;

    public bool ShowZoomMiniMap => _isSystemPoweredOn && ZoomLevel > 1.0;

    public bool CanUseZoomControls => _isSystemPoweredOn;

    public double MiniMapViewportWidth => MiniMapWidth / ZoomLevel;

    public double MiniMapViewportHeight => MiniMapHeight / ZoomLevel;

    public double MiniMapViewportLeft
    {
        get
        {
            var maxPan = GetMaxPanX();
            if (maxPan <= 0) return (MiniMapWidth - MiniMapViewportWidth) / 2;
            var normalized = (_zoomPanX + maxPan) / (maxPan * 2);
            return (1.0 - normalized) * (MiniMapWidth - MiniMapViewportWidth);
        }
    }

    public double MiniMapViewportTop
    {
        get
        {
            var maxPan = GetMaxPanY();
            if (maxPan <= 0) return (MiniMapHeight - MiniMapViewportHeight) / 2;
            var normalized = (_zoomPanY + maxPan) / (maxPan * 2);
            return (1.0 - normalized) * (MiniMapHeight - MiniMapViewportHeight);
        }
    }

    public void SetSystemPoweredOn(bool isPoweredOn)
    {
        if (_isSystemPoweredOn == isPoweredOn) return;
        _isSystemPoweredOn = isPoweredOn;
        OnPropertyChanged(nameof(ShowZoomMiniMap));
        OnPropertyChanged(nameof(CanUseZoomControls));
    }

    public void UpdateViewportSize(double width, double height)
    {
        _viewportWidth = Math.Max(width, 1);
        _viewportHeight = Math.Max(height, 1);
        ClampZoomPan();
        UpdateMiniMapViewport();
    }

    public void PanZoom(double deltaX, double deltaY)
    {
        if (!ShowZoomMiniMap) return;
        _zoomPanX = Math.Clamp(_zoomPanX + deltaX, -GetMaxPanX(), GetMaxPanX());
        _zoomPanY = Math.Clamp(_zoomPanY + deltaY, -GetMaxPanY(), GetMaxPanY());
        OnPropertyChanged(nameof(ZoomTransformX));
        OnPropertyChanged(nameof(ZoomTransformY));
        UpdateMiniMapViewport();
    }

    public void AdjustZoomByWheel(double wheelSteps)
    {
        if (!CanUseZoomControls || Math.Abs(wheelSteps) < double.Epsilon) return;
        ZoomLevel += wheelSteps * 0.1;
    }

    public void RefreshText()
    {
        OnPropertyChanged(nameof(BrightnessText));
        OnPropertyChanged(nameof(ContrastText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void ClampZoomPan()
    {
        _zoomPanX = Math.Clamp(_zoomPanX, -GetMaxPanX(), GetMaxPanX());
        _zoomPanY = Math.Clamp(_zoomPanY, -GetMaxPanY(), GetMaxPanY());
        OnPropertyChanged(nameof(ZoomTransformX));
        OnPropertyChanged(nameof(ZoomTransformY));
    }

    private double GetMaxPanX() => (_viewportWidth * (ZoomLevel - 1)) / 2;

    private double GetMaxPanY() => (_viewportHeight * (ZoomLevel - 1)) / 2;

    private void UpdateMiniMapViewport()
    {
        OnPropertyChanged(nameof(MiniMapViewportWidth));
        OnPropertyChanged(nameof(MiniMapViewportHeight));
        OnPropertyChanged(nameof(MiniMapViewportLeft));
        OnPropertyChanged(nameof(MiniMapViewportTop));
    }
}

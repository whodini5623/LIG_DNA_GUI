using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace BroadcastControl.App.View;

public partial class RecordedVideosView : UserControl
{
    public event Action? RefreshRequested;
    public event Action<SelectionChangedEventArgs>? VideoSelectionRequested;
    public event Action<Exception>? PlaybackFailed;

    private const double MiniMapWidth = 120;
    private const double MiniMapHeight = 62;
    private readonly DispatcherTimer _positionTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool _isDraggingPosition;
    private bool _isDraggingPan;
    private double _zoomLevel = 1.0;
    private double _panX;
    private double _panY;
    private Point _lastPanPoint;

    public RecordedVideosView()
    {
        InitializeComponent();
        _positionTimer.Tick += (_, _) => UpdatePositionUi();
        Unloaded += (_, _) => _positionTimer.Stop();
    }

    private void RefreshRecordedVideosButton_OnClick(object sender, RoutedEventArgs e) =>
        OnRecordedVideosUiAction(nameof(RefreshRecordedVideosButton_OnClick), sender, e);

    private void CloseRecordedVideosButton_OnClick(object sender, RoutedEventArgs e) =>
        OnRecordedVideosUiAction(nameof(CloseRecordedVideosButton_OnClick), sender, e);

    private void RecordedVideoList_OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        OnRecordedVideosUiAction(nameof(RecordedVideoList_OnSelectionChanged), sender, e);

    private void OnRecordedVideosUiAction(string action, object? sender, RoutedEventArgs e)
    {
        switch (action)
        {
            case nameof(RefreshRecordedVideosButton_OnClick):
                RefreshRequested?.Invoke();
                e.Handled = true;
                break;
            case nameof(CloseRecordedVideosButton_OnClick):
                StopAndClose();
                e.Handled = true;
                break;
            case nameof(RecordedVideoList_OnSelectionChanged):
                if (e is SelectionChangedEventArgs selectionChangedEventArgs)
                    VideoSelectionRequested?.Invoke(selectionChangedEventArgs);
                break;
        }
    }

    private void RecordedVideoViewport_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        SetZoom(_zoomLevel + (e.Delta > 0 ? 0.1 : -0.1));
        e.Handled = true;
    }

    private void RecordedVideoViewport_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_zoomLevel <= 1.0) return;
        _isDraggingPan = true;
        _lastPanPoint = e.GetPosition(RecordedVideoViewport);
        RecordedVideoViewport.CaptureMouse();
        RecordedVideoViewport.Cursor = Cursors.ScrollAll;
        e.Handled = true;
    }

    private void RecordedVideoViewport_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingPan) return;
        var point = e.GetPosition(RecordedVideoViewport);
        _panX += point.X - _lastPanPoint.X;
        _panY += point.Y - _lastPanPoint.Y;
        _lastPanPoint = point;
        ClampPan();
        UpdateZoomUi();
    }

    private void RecordedVideoViewport_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => StopPanDrag();

    private void RecordedVideoPlayer_OnMediaOpened(object sender, RoutedEventArgs e)
    {
        if (RecordedVideoPlayer.NaturalDuration.HasTimeSpan)
        {
            var duration = RecordedVideoPlayer.NaturalDuration.TimeSpan;
            RecordedVideoPositionSlider.Maximum = Math.Max(duration.TotalSeconds, 1);
            RecordedVideoDurationText.Text = FormatVideoTime(duration);
        }
        UpdatePositionUi();
        _positionTimer.Start();
    }

    private void RecordedVideoPlayer_OnMediaEnded(object sender, RoutedEventArgs e)
    {
        _positionTimer.Stop();
        UpdatePositionUi();
    }

    private void RecordedVideoPlayer_OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _positionTimer.Stop();
        RecordedVideosStatusText.Text = "영상을 재생할 수 없습니다.";
        if (e.ErrorException is not null) PlaybackFailed?.Invoke(e.ErrorException);
    }

    private void RecordedVideoPositionSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isDraggingPosition)
            RecordedVideoCurrentTimeText.Text = FormatVideoTime(TimeSpan.FromSeconds(e.NewValue));
    }

    private void RecordedVideoPositionSlider_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _isDraggingPosition = true;

    private void RecordedVideoPositionSlider_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        RecordedVideoPlayer.Position = TimeSpan.FromSeconds(RecordedVideoPositionSlider.Value);
        _isDraggingPosition = false;
        UpdatePositionUi();
    }

    private void RecordedVideoPlayButton_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyPlaybackSpeed();
        RecordedVideoPlayer.Play();
        _positionTimer.Start();
    }

    private void RecordedVideoPauseButton_OnClick(object sender, RoutedEventArgs e) => RecordedVideoPlayer.Pause();

    private void RecordedVideoStopButton_OnClick(object sender, RoutedEventArgs e)
    {
        RecordedVideoPlayer.Stop();
        _positionTimer.Stop();
        ResetPositionUi();
    }

    private void RecordedVideoZoomResetButton_OnClick(object sender, RoutedEventArgs e) => ResetZoom();
    private void RecordedVideoZoomSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RecordedVideoScaleTransform is not null) SetZoom(e.NewValue);
    }
    private void PlaybackSpeedCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyPlaybackSpeed();

    public void PrepareVideoForDownload()
    {
        _positionTimer.Stop();
        RecordedVideoPlayer.Stop();
        RecordedVideoPlayer.Source = null;
        ResetPositionUi();
    }

    public void PlayCachedVideo(string localPath, string displayName)
    {
        RecordedVideoPlayer.Source = new Uri(localPath, UriKind.Absolute);
        ResetZoom();
        ApplyPlaybackSpeed();
        RecordedVideoPlayer.Play();
        _positionTimer.Start();
        RecordedVideosStatusText.Text = displayName;
    }

    public void StopAndClose()
    {
        _positionTimer.Stop();
        RecordedVideoPlayer.Stop();
        RecordedVideoPlayer.Source = null;
        RecordedVideosPanel.Visibility = Visibility.Collapsed;
    }

    public void OpenPanel() => RecordedVideosPanel.Visibility = Visibility.Visible;
    private void ApplyPlaybackSpeed()
    {
        if (PlaybackSpeedCombo?.SelectedItem is not ComboBoxItem item ||
            item.Tag is not string value ||
            !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
            speed = 1.0;
        RecordedVideoPlayer.SpeedRatio = speed;
    }

    private void UpdatePositionUi()
    {
        if (_isDraggingPosition) return;
        var position = RecordedVideoPlayer.Position;
        RecordedVideoCurrentTimeText.Text = FormatVideoTime(position);
        if (RecordedVideoPlayer.NaturalDuration.HasTimeSpan)
        {
            var duration = RecordedVideoPlayer.NaturalDuration.TimeSpan;
            RecordedVideoPositionSlider.Maximum = Math.Max(duration.TotalSeconds, 1);
            RecordedVideoDurationText.Text = FormatVideoTime(duration);
        }
        RecordedVideoPositionSlider.Value = Math.Min(position.TotalSeconds, RecordedVideoPositionSlider.Maximum);
    }

    private void ResetPositionUi()
    {
        _isDraggingPosition = false;
        RecordedVideoPositionSlider.Minimum = 0;
        RecordedVideoPositionSlider.Maximum = 1;
        RecordedVideoPositionSlider.Value = 0;
        RecordedVideoCurrentTimeText.Text = "00:00";
        RecordedVideoDurationText.Text = "00:00";
    }

    private void SetZoom(double value)
    {
        _zoomLevel = Math.Clamp(value, 1.0, 4.0);
        if (_zoomLevel <= 1.0)
        {
            _panX = 0;
            _panY = 0;
            StopPanDrag();
        }
        ClampPan();
        UpdateZoomUi();
    }

    private void ResetZoom()
    {
        _zoomLevel = 1.0;
        _panX = _panY = 0;
        StopPanDrag();
        UpdateZoomUi();
    }

    private void StopPanDrag()
    {
        if (!_isDraggingPan) return;
        _isDraggingPan = false;
        RecordedVideoViewport.ReleaseMouseCapture();
        RecordedVideoViewport.Cursor = Cursors.Arrow;
    }

    private double MaxPanX => Math.Max(0, RecordedVideoViewport.ActualWidth * (_zoomLevel - 1.0) / 2.0);
    private double MaxPanY => Math.Max(0, RecordedVideoViewport.ActualHeight * (_zoomLevel - 1.0) / 2.0);

    private void ClampPan()
    {
        _panX = Math.Clamp(_panX, -MaxPanX, MaxPanX);
        _panY = Math.Clamp(_panY, -MaxPanY, MaxPanY);
    }

    private void UpdateZoomUi()
    {
        if (RecordedVideoScaleTransform is null) return;
        RecordedVideoScaleTransform.ScaleX = _zoomLevel;
        RecordedVideoScaleTransform.ScaleY = _zoomLevel;
        RecordedVideoTranslateTransform.X = _panX;
        RecordedVideoTranslateTransform.Y = _panY;
        if (RecordedVideoZoomSlider is not null && Math.Abs(RecordedVideoZoomSlider.Value - _zoomLevel) > 0.001)
            RecordedVideoZoomSlider.Value = _zoomLevel;
        if (RecordedVideoZoomResetButton is not null)
            RecordedVideoZoomResetButton.Content = $"x{_zoomLevel:0.00}";
        UpdateMiniMap();
    }

    private void UpdateMiniMap()
    {
        if (RecordedVideoZoomMiniMap is null || RecordedVideoMiniMapViewport is null) return;
        if (_zoomLevel <= 1.0)
        {
            RecordedVideoZoomMiniMap.Visibility = Visibility.Collapsed;
            return;
        }
        RecordedVideoZoomMiniMap.Visibility = Visibility.Visible;
        var viewportWidth = MiniMapWidth / _zoomLevel;
        var viewportHeight = MiniMapHeight / _zoomLevel;
        RecordedVideoMiniMapViewport.Width = viewportWidth;
        RecordedVideoMiniMapViewport.Height = viewportHeight;
        var left = MaxPanX <= 0 ? (MiniMapWidth - viewportWidth) / 2 : (1.0 - ((_panX + MaxPanX) / (MaxPanX * 2.0))) * (MiniMapWidth - viewportWidth);
        var top = MaxPanY <= 0 ? (MiniMapHeight - viewportHeight) / 2 : (1.0 - ((_panY + MaxPanY) / (MaxPanY * 2.0))) * (MiniMapHeight - viewportHeight);
        Canvas.SetLeft(RecordedVideoMiniMapViewport, left);
        Canvas.SetTop(RecordedVideoMiniMapViewport, top);
    }

    private static string FormatVideoTime(TimeSpan value) => value.TotalHours >= 1
        ? value.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
        : value.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
}

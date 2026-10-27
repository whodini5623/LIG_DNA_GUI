using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.IO;
using BroadcastControl.App.ViewModels;

namespace BroadcastControl.App.View.Middle;

public partial class CameraWorkspaceView : UserControl
{
    private bool _isDraggingZoom;
    private Point _lastZoomDragPoint;

    public event EventHandler? OverlayRefreshRequested;
    public event MouseButtonEventHandler? MotorButtonPressed;
    public event MouseButtonEventHandler? MotorButtonReleased;
    public event MouseEventHandler? MotorButtonExited;

    public CameraWorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += CameraWorkspaceView_OnDataContextChanged;
    }

    private void CameraWorkspaceView_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldViewModel)
            oldViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        if (e.OldValue is MainViewModel oldMainViewModel)
            oldMainViewModel.DetectionOverlay.PropertyChanged -= DetectionOverlay_OnPropertyChanged;
        if (e.NewValue is INotifyPropertyChanged newViewModel)
            newViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        if (e.NewValue is MainViewModel viewModel)
        {
            viewModel.DetectionOverlay.PropertyChanged += DetectionOverlay_OnPropertyChanged;
            viewModel.SetEvidenceCaptureProvider(() => CaptureElementAsPng(CameraPanel));
            viewModel.UpdateCameraViewport(CameraViewport.ActualWidth, CameraViewport.ActualHeight);
            RefreshDetectionOverlay();
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.ZoomLevel):
            case nameof(MainViewModel.ZoomTransformX):
            case nameof(MainViewModel.ZoomTransformY):
            case nameof(MainViewModel.IsEoPrimary):
            case nameof(MainViewModel.SelectedPrimaryTarget):
                if (e.PropertyName is nameof(MainViewModel.ZoomLevel)
                    or nameof(MainViewModel.ZoomTransformX)
                    or nameof(MainViewModel.ZoomTransformY))
                    ViewModel?.UpdateCameraTransform(CameraViewport.ActualWidth, CameraViewport.ActualHeight);
                RefreshDetectionOverlay();
                break;
        }
    }

    private void DetectionOverlay_OnPropertyChanged(object? sender, PropertyChangedEventArgs e) => RefreshDetectionOverlay();

    private void RefreshDetectionOverlay()
    {
        var viewModel = ViewModel;
        if (viewModel is null || viewModel.DetectionOverlay.DisplayDetections.Count == 0)
        {
            DetectionOverlay.ClearOverlay();
            return;
        }

        DetectionOverlay.RenderDetections(
            viewModel.DetectionOverlay.DisplayDetections,
            viewModel.DetectionOverlay.SourceWidth,
            viewModel.DetectionOverlay.SourceHeight,
            forceRefresh: true);
    }


    private MainViewModel? ViewModel => DataContext as MainViewModel;
    private void CameraViewport_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ViewModel?.UpdateCameraViewport(e.NewSize.Width, e.NewSize.Height);
        RefreshDetectionOverlay();
    }
    private void CameraViewport_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel?.ShowZoomMiniMap != true) return;
        _isDraggingZoom = true; _lastZoomDragPoint = e.GetPosition(CameraViewport); CameraViewport.CaptureMouse();
    }
    private void CameraViewport_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingZoom) return;
        var point = e.GetPosition(CameraViewport); var delta = point - _lastZoomDragPoint; _lastZoomDragPoint = point;
        ViewModel?.CameraControlBar.PanZoom(delta.X, delta.Y);
    }
    private void CameraViewport_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ViewModel?.CanUseZoomControls != true) return;
        ViewModel.CameraControlBar.AdjustZoomByWheel(e.Delta / 120.0); e.Handled = true;
    }
    private void CameraViewport_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingZoom) return; _isDraggingZoom = false; CameraViewport.ReleaseMouseCapture();
    }
    private void RotateLargeFeedButton_OnClick(object sender, RoutedEventArgs e)
    { ViewModel?.CameraWorkspace.RotateLargeFeedClockwise(); OverlayRefreshRequested?.Invoke(this, EventArgs.Empty); RefreshDetectionOverlay(); e.Handled = true; }
    private void RotateInsetFeedButton_OnClick(object sender, RoutedEventArgs e)
    { ViewModel?.CameraWorkspace.RotateInsetFeedClockwise(); OverlayRefreshRequested?.Invoke(this, EventArgs.Empty); RefreshDetectionOverlay(); e.Handled = true; }
    private void MotorButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => MotorButtonPressed?.Invoke(sender, e);
    private void MotorButton_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => MotorButtonReleased?.Invoke(sender, e);
    private void MotorButton_OnMouseLeave(object sender, MouseEventArgs e) => MotorButtonExited?.Invoke(sender, e);

    private static byte[]? CaptureElementAsPng(FrameworkElement element)
    {
        var width = Math.Max(1, (int)Math.Round(element.ActualWidth));
        var height = Math.Max(1, (int)Math.Round(element.ActualHeight));
        if (width < 2 || height < 2) return null;
        try
        {
            element.UpdateLayout();
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(element);
            bitmap.Freeze();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
        catch
        {
            return null;
        }
    }
}

using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
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
        if (e.NewValue is INotifyPropertyChanged newViewModel)
            newViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
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
                OverlayRefreshRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }


    private MainViewModel? ViewModel => DataContext as MainViewModel;
    private void CameraViewport_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ViewModel?.UpdateViewportSize(e.NewSize.Width, e.NewSize.Height);
        OverlayRefreshRequested?.Invoke(this, EventArgs.Empty);
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
        ViewModel?.PanZoom(delta.X, delta.Y);
    }
    private void CameraViewport_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ViewModel?.CanUseZoomControls != true) return;
        ViewModel.AdjustZoomByWheel(e.Delta / 120.0); e.Handled = true;
    }
    private void CameraViewport_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingZoom) return; _isDraggingZoom = false; CameraViewport.ReleaseMouseCapture();
    }
    private void RotateLargeFeedButton_OnClick(object sender, RoutedEventArgs e)
    { ViewModel?.RotateLargeFeedClockwise(); OverlayRefreshRequested?.Invoke(this, EventArgs.Empty); e.Handled = true; }
    private void RotateInsetFeedButton_OnClick(object sender, RoutedEventArgs e)
    { ViewModel?.RotateInsetFeedClockwise(); OverlayRefreshRequested?.Invoke(this, EventArgs.Empty); e.Handled = true; }
    private void MotorButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => MotorButtonPressed?.Invoke(sender, e);
    private void MotorButton_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => MotorButtonReleased?.Invoke(sender, e);
    private void MotorButton_OnMouseLeave(object sender, MouseEventArgs e) => MotorButtonExited?.Invoke(sender, e);
}

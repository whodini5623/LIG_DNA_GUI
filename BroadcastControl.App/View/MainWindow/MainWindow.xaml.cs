using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BroadcastControl.App.Models;
using BroadcastControl.App.ViewModels;

namespace BroadcastControl.App;

public partial class MainWindow : Window
{
    private const double WindowedWidth = 1600;
    private const double WindowedHeight = 900;
    private readonly MainViewModel _viewModel;
    private bool _isFullscreenMode = true;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        RecordedVideosView.RefreshRequested += OnRefreshRecordedVideosRequested;
        RecordedVideosView.VideoSelectionRequested += OnRecordedVideoSelectionRequested;
        RecordedVideosView.PlaybackFailed += OnRecordedVideoPlaybackFailed;
        CameraWorkspace.OverlayRefreshRequested += _viewModel.OnMiddleOverlayRefreshRequested;
        CameraWorkspace.MotorButtonPressed += MotorButton_OnPreviewMouseLeftButtonDown;
        CameraWorkspace.MotorButtonReleased += MotorButton_OnPreviewMouseLeftButtonUp;
        CameraWorkspace.MotorButtonExited += MotorButton_OnMouseLeave;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Maximized;
        SettingsDrawerView.UpdateWindowModeButtonText(_isFullscreenMode);
    }

    private async void OpenRecordedVideosButton_OnClick(object sender, RoutedEventArgs e)
    {
        RecordedVideosView.OpenPanel();
        await LoadRecordedVideosAsync();
        e.Handled = true;
    }

    private async void OnRefreshRecordedVideosRequested() => await LoadRecordedVideosAsync();

    private async Task LoadRecordedVideosAsync()
    {
        try
        {
            await _viewModel.RecordedVideos.LoadAsync();
        }
        catch (Exception ex)
        {
            _viewModel.AppendImportantLog($"Could not load recorded videos: {ex.Message}");
        }
    }

    private void OnRecordedVideoSelectionRequested(SelectionChangedEventArgs e)
    {
        if (RecordedVideosView.RecordedVideoList.SelectedItem is not RecordedVideoItem item) return;
        if (_viewModel.RecordedVideos.Select(item))
        {
            return;
        }
        _ = PlayRecordedVideoAsync(item);
    }

    private async Task PlayRecordedVideoAsync(RecordedVideoItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Url)) return;
        try
        {
            RecordedVideosView.PrepareVideoForDownload();
            _viewModel.RecordedVideos.SetPlaybackStatus($"Downloading {item.DisplayName}...");
            var localPath = await _viewModel.RecordedVideos.CacheAsync(item);
            RecordedVideosView.PlayCachedVideo(localPath);
            _viewModel.RecordedVideos.SetPlaybackStatus(item.DisplayName);
        }
        catch (Exception ex)
        {
            _viewModel.RecordedVideos.SetPlaybackStatus("Video playback could not start.");
            _viewModel.AppendImportantLog($"Could not prepare recorded video: {ex.Message}");
        }
    }

    private void OnRecordedVideoPlaybackFailed(Exception exception)
    {
        _viewModel.RecordedVideos.SetPlaybackStatus("Video playback failed.");
        _viewModel.AppendImportantLog($"Recorded video playback failed: {exception.Message}");
    }

    private void WindowModeToggleButton_OnClick(object sender, RoutedEventArgs e) => ToggleWindowMode();

    private void ToggleWindowMode()
    {
        if (_isFullscreenMode)
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            WindowState = WindowState.Normal;
            Width = WindowedWidth;
            Height = WindowedHeight;
            Left = Math.Max(0, (SystemParameters.WorkArea.Width - Width) / 2);
            Top = Math.Max(0, (SystemParameters.WorkArea.Height - Height) / 2);
            _isFullscreenMode = false;
        }
        else
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            _isFullscreenMode = true;
        }
        SettingsDrawerView.UpdateWindowModeButtonText(_isFullscreenMode);
    }

    private void MotorButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        CameraControlBar.HandleMotorButtonPressed(sender, e);

    private void MotorButton_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        CameraControlBar.HandleMotorButtonReleased(sender, e);

    private void MotorButton_OnMouseLeave(object sender, MouseEventArgs e) =>
        CameraControlBar.HandleMotorButtonExited(sender, e);
}

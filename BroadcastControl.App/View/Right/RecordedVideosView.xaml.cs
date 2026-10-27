using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BroadcastControl.App.View;

public partial class RecordedVideosView : UserControl
{
    public event Action<string, object?, RoutedEventArgs>? UiAction;
    public RecordedVideosView() => InitializeComponent();
    private void Raise(string name, object sender, RoutedEventArgs e) => UiAction?.Invoke(name, sender, e);
    private void RefreshRecordedVideosButton_OnClick(object s, RoutedEventArgs e) => Raise(nameof(RefreshRecordedVideosButton_OnClick), s, e);
    private void CloseRecordedVideosButton_OnClick(object s, RoutedEventArgs e) => Raise(nameof(CloseRecordedVideosButton_OnClick), s, e);
    private void RecordedVideoList_OnSelectionChanged(object s, SelectionChangedEventArgs e) => Raise(nameof(RecordedVideoList_OnSelectionChanged), s, e);
    private void RecordedVideoViewport_OnMouseWheel(object s, MouseWheelEventArgs e) => Raise(nameof(RecordedVideoViewport_OnMouseWheel), s, e);
    private void RecordedVideoViewport_OnMouseLeftButtonDown(object s, MouseButtonEventArgs e) => Raise(nameof(RecordedVideoViewport_OnMouseLeftButtonDown), s, e);
    private void RecordedVideoViewport_OnMouseMove(object s, MouseEventArgs e) => Raise(nameof(RecordedVideoViewport_OnMouseMove), s, e);
    private void RecordedVideoViewport_OnMouseLeftButtonUp(object s, MouseButtonEventArgs e) => Raise(nameof(RecordedVideoViewport_OnMouseLeftButtonUp), s, e);
    private void RecordedVideoPlayer_OnMediaOpened(object s, RoutedEventArgs e) => Raise(nameof(RecordedVideoPlayer_OnMediaOpened), s, e);
    private void RecordedVideoPlayer_OnMediaEnded(object s, RoutedEventArgs e) => Raise(nameof(RecordedVideoPlayer_OnMediaEnded), s, e);
    private void RecordedVideoPlayer_OnMediaFailed(object s, ExceptionRoutedEventArgs e) => Raise(nameof(RecordedVideoPlayer_OnMediaFailed), s, e);
    private void RecordedVideoPositionSlider_OnValueChanged(object s, RoutedPropertyChangedEventArgs<double> e) => Raise(nameof(RecordedVideoPositionSlider_OnValueChanged), s, e);
    private void RecordedVideoPositionSlider_OnPreviewMouseLeftButtonDown(object s, MouseButtonEventArgs e) => Raise(nameof(RecordedVideoPositionSlider_OnPreviewMouseLeftButtonDown), s, e);
    private void RecordedVideoPositionSlider_OnPreviewMouseLeftButtonUp(object s, MouseButtonEventArgs e) => Raise(nameof(RecordedVideoPositionSlider_OnPreviewMouseLeftButtonUp), s, e);
    private void RecordedVideoPlayButton_OnClick(object s, RoutedEventArgs e) => Raise(nameof(RecordedVideoPlayButton_OnClick), s, e);
    private void RecordedVideoPauseButton_OnClick(object s, RoutedEventArgs e) => Raise(nameof(RecordedVideoPauseButton_OnClick), s, e);
    private void RecordedVideoStopButton_OnClick(object s, RoutedEventArgs e) => Raise(nameof(RecordedVideoStopButton_OnClick), s, e);
    private void RecordedVideoZoomResetButton_OnClick(object s, RoutedEventArgs e) => Raise(nameof(RecordedVideoZoomResetButton_OnClick), s, e);
    private void RecordedVideoZoomSlider_OnValueChanged(object s, RoutedPropertyChangedEventArgs<double> e) => Raise(nameof(RecordedVideoZoomSlider_OnValueChanged), s, e);
    private void PlaybackSpeedCombo_OnSelectionChanged(object s, SelectionChangedEventArgs e) => Raise(nameof(PlaybackSpeedCombo_OnSelectionChanged), s, e);
}

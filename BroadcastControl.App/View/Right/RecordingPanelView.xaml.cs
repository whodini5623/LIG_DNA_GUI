using System.Windows.Controls;

namespace BroadcastControl.App.View;

public partial class RecordingPanelView : UserControl
{
    public event System.Windows.RoutedEventHandler? OpenRecordedVideosRequested;
    public RecordingPanelView()
    {
        InitializeComponent();
    }

    private void OpenRecordedVideosButton_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        OpenRecordedVideosRequested?.Invoke(this, e);
    }
}

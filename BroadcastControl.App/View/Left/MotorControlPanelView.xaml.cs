using System.Windows.Controls;
using System.Windows;

namespace BroadcastControl.App.View;

public partial class MotorControlPanelView : UserControl
{
    public MotorControlPanelView()
    {
        InitializeComponent();
    }

    private void RotateAuxCameraButton_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void MotorAngleEntry_OnClick(object sender, RoutedEventArgs e)
    {
    }
}

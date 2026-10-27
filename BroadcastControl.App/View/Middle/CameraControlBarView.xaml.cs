using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;

namespace BroadcastControl.App.View.Middle;

public partial class CameraControlBarView : UserControl
{
    public event MouseButtonEventHandler? MotorButtonPressed;
    public event MouseButtonEventHandler? MotorButtonReleased;
    public event MouseEventHandler? MotorButtonExited;
    public CameraControlBarView() => InitializeComponent();
    private void Button_Click(object sender, RoutedEventArgs e) { }
    private void MotorButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => MotorButtonPressed?.Invoke(sender, e);
    private void MotorButton_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => MotorButtonReleased?.Invoke(sender, e);
    private void MotorButton_OnMouseLeave(object sender, MouseEventArgs e) => MotorButtonExited?.Invoke(sender, e);
}

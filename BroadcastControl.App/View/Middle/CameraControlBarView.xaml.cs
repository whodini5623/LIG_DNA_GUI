using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;

namespace BroadcastControl.App.View.Middle;

public partial class CameraControlBarView : UserControl
{
    public event MouseButtonEventHandler? MotorButtonPressed;
    public event MouseButtonEventHandler? MotorButtonReleased;
    public event MouseEventHandler? MotorButtonExited;
    public CameraControlBarView() => InitializeComponent();

    public void UpdateMotorPadButtonVisualStates(bool left, bool right, bool up, bool down, bool center)
    {
        SetMotorPadButtonActive(MotorPadLeftButton, left);
        SetMotorPadButtonActive(MotorPadRightButton, right);
        SetMotorPadButtonActive(MotorPadUpButton, up);
        SetMotorPadButtonActive(MotorPadDownButton, down);
        SetMotorPadButtonActive(MotorPadCenterButton, center);
    }

    private static void SetMotorPadButtonActive(Button? button, bool isActive)
    {
        if (button is null) return;
        if (!isActive)
        {
            button.ClearValue(Control.BackgroundProperty);
            button.ClearValue(Control.BorderBrushProperty);
            return;
        }

        button.Background = new SolidColorBrush(Color.FromRgb(0x36, 0x55, 0x64));
        button.BorderBrush = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99));
    }
    private void Button_Click(object sender, RoutedEventArgs e) { }
    private void MotorButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => MotorButtonPressed?.Invoke(sender, e);
    private void MotorButton_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => MotorButtonReleased?.Invoke(sender, e);
    private void MotorButton_OnMouseLeave(object sender, MouseEventArgs e) => MotorButtonExited?.Invoke(sender, e);
}

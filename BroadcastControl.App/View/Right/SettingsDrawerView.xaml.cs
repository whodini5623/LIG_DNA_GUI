using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BroadcastControl.App.ViewModels;

namespace BroadcastControl.App.View;

public partial class SettingsDrawerView : UserControl
{
    private const double ClosedOffset = 320;
    public event RoutedEventHandler? WindowModeToggleRequested;
    public SettingsDrawerView() => InitializeComponent();
    private void SettingsBackdrop_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.IsSettingsOpen) vm.IsSettingsOpen = false;
    }
    private void WindowModeToggleButton_OnClick(object sender, RoutedEventArgs e) => WindowModeToggleRequested?.Invoke(sender, e);

    public void UpdateWindowModeButtonText(bool isFullscreen)
    {
        if (DataContext is not MainViewModel viewModel) return;
        WindowModeToggleButton.Content = isFullscreen
            ? viewModel.Text["WindowMode"]
            : viewModel.Text["FullscreenMode"];
    }

    public void SetOpenState(bool isOpen, bool animate)
    {
        if (!animate)
        {
            SettingsBackdrop.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
            SettingsBackdrop.IsHitTestVisible = isOpen;
            SettingsBackdrop.Opacity = isOpen ? 1.0 : 0.0;
            SettingsDrawer.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
            SettingsDrawer.Opacity = isOpen ? 1.0 : 0.0;
            SettingsDrawerTransform.X = isOpen ? 0 : ClosedOffset;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(isOpen ? 220 : 170);
        var easing = new CubicEase { EasingMode = isOpen ? EasingMode.EaseOut : EasingMode.EaseIn };
        if (isOpen)
        {
            SettingsBackdrop.Visibility = Visibility.Visible;
            SettingsBackdrop.IsHitTestVisible = true;
            SettingsDrawer.Visibility = Visibility.Visible;
        }

        var backdropAnimation = new DoubleAnimation(isOpen ? 1.0 : 0.0, duration) { EasingFunction = easing };
        var drawerOpacityAnimation = new DoubleAnimation(isOpen ? 1.0 : 0.0, duration) { EasingFunction = easing };
        var drawerSlideAnimation = new DoubleAnimation(isOpen ? 0 : ClosedOffset, duration) { EasingFunction = easing };
        if (!isOpen)
        {
            drawerSlideAnimation.Completed += (_, _) =>
            {
                SettingsBackdrop.Visibility = Visibility.Collapsed;
                SettingsBackdrop.IsHitTestVisible = false;
                SettingsDrawer.Visibility = Visibility.Collapsed;
            };
        }

        SettingsBackdrop.BeginAnimation(OpacityProperty, backdropAnimation, HandoffBehavior.SnapshotAndReplace);
        SettingsDrawer.BeginAnimation(OpacityProperty, drawerOpacityAnimation, HandoffBehavior.SnapshotAndReplace);
        SettingsDrawerTransform.BeginAnimation(TranslateTransform.XProperty, drawerSlideAnimation, HandoffBehavior.SnapshotAndReplace);
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BroadcastControl.App.ViewModels;

namespace BroadcastControl.App.View;

public partial class SettingsDrawerView : UserControl
{
    public event RoutedEventHandler? WindowModeToggleRequested;
    public SettingsDrawerView() => InitializeComponent();
    private void SettingsBackdrop_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.IsSettingsOpen) vm.IsSettingsOpen = false;
    }
    private void WindowModeToggleButton_OnClick(object sender, RoutedEventArgs e) => WindowModeToggleRequested?.Invoke(sender, e);
}

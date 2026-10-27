using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using BroadcastControl.App.ViewModels;

namespace BroadcastControl.App.View;

public partial class MotorDetailsView : UserControl
{
    public MotorDetailsView()
    {
        InitializeComponent();
    }

    private void MotorDetailsBackdrop_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.IsMotorDetailsOpen)
        {
            viewModel.IsMotorDetailsOpen = false;
            e.Handled = true;
        }
    }
}

namespace BroadcastControl.App.ViewModels.Right;

using System.ComponentModel;
using System.Runtime.CompilerServices;

/// <summary>
/// View state and actions for the corresponding SettingsDrawer.
/// </summary>
public sealed class SettingsDrawerViewModel : INotifyPropertyChanged
{
    private bool _isOpen;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (_isOpen == value) return;
            _isOpen = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOpen)));
        }
    }

    public void Toggle() => IsOpen = !IsOpen;
}

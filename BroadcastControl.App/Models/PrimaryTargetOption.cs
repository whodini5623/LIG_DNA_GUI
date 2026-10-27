using System.ComponentModel;

namespace BroadcastControl.App.Models;

public sealed class PrimaryTargetOption : INotifyPropertyChanged
{
    private string _displayName;

    public PrimaryTargetOption(string value, string displayName)
    {
        Value = value;
        _displayName = displayName;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Value { get; }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (string.Equals(_displayName, value, StringComparison.Ordinal))
            {
                return;
            }

            _displayName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
        }
    }
}

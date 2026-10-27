using System.ComponentModel;

namespace BroadcastControl.App.Models;

public sealed class MotorStatusItem : INotifyPropertyChanged
{
    private string _value;

    public MotorStatusItem(string name, string value)
    {
        Name = name;
        _value = value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }

    public string Value
    {
        get => _value;
        set
        {
            if (string.Equals(_value, value, StringComparison.Ordinal))
            {
                return;
            }

            _value = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }
}

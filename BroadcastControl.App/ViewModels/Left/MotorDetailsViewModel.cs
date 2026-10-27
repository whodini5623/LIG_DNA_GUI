namespace BroadcastControl.App.ViewModels.Left;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using BroadcastControl.App.Models;

/// <summary>
/// View state and actions for the corresponding MotorDetails.
/// </summary>
public sealed class MotorDetailsViewModel : INotifyPropertyChanged
{
    private const double MotorPanLimitDegrees = 360;
    private const double MotorTiltLimitDegrees = 360;
    private double _panPositionDegrees;
    private double _tiltPositionDegrees;
    private bool _isOpen;

    public MotorDetailsViewModel()
    {
        PanMotorStatusItems = new ObservableCollection<MotorStatusItem>(CreateDefaultMotorStatusItems());
        TiltMotorStatusItems = new ObservableCollection<MotorStatusItem>(CreateDefaultMotorStatusItems());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<MotorStatusItem> PanMotorStatusItems { get; }

    public ObservableCollection<MotorStatusItem> TiltMotorStatusItems { get; }

    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (_isOpen == value) return;
            _isOpen = value;
            OnPropertyChanged();
        }
    }

    public string PanMotorPositionText => $"{_panPositionDegrees:0.0}°";

    public string TiltMotorPositionText => $"{_tiltPositionDegrees:0.0}°";

    public void SetMotorPosition(double panDegrees, double tiltDegrees)
    {
        _panPositionDegrees = NormalizeMotorDegrees(panDegrees, MotorPanLimitDegrees);
        _tiltPositionDegrees = NormalizeMotorDegrees(tiltDegrees, MotorTiltLimitDegrees);
        OnPropertyChanged(nameof(PanMotorPositionText));
        OnPropertyChanged(nameof(TiltMotorPositionText));
    }

    public void UpdateMotorStatus(MotorStatusSnapshot snapshot)
    {
        UpdateMotorStatusItems(PanMotorStatusItems, snapshot.Pan);
        _panPositionDegrees = DynamixelPositionToDegrees(snapshot.Pan.PresentPosition);
        OnPropertyChanged(nameof(PanMotorPositionText));

        if (snapshot.Tilt is { } tilt)
        {
            UpdateMotorStatusItems(TiltMotorStatusItems, tilt);
            _tiltPositionDegrees = DynamixelPositionToDegrees(tilt.PresentPosition);
            OnPropertyChanged(nameof(TiltMotorPositionText));
        }
    }

    private static void UpdateMotorStatusItems(ObservableCollection<MotorStatusItem> items, MotorStatusPacket packet)
    {
        SetMotorStatusValue(items, "Motor Value", packet.PresentPosition.ToString(CultureInfo.InvariantCulture));
        SetMotorStatusValue(items, "Actual Value", $"{DynamixelPositionToDegrees(packet.PresentPosition):0.0} deg");
        SetMotorStatusValue(items, "Motor Change Value", packet.GoalPosition.ToString(CultureInfo.InvariantCulture));
        SetMotorStatusValue(items, "Actual Change Value", $"{DynamixelPositionToDegrees(packet.GoalPosition):0.0} deg");
        SetMotorStatusValue(items, "Velocity", packet.PresentVelocity.ToString(CultureInfo.InvariantCulture));
        SetMotorStatusValue(items, "Load", packet.PresentLoad.ToString(CultureInfo.InvariantCulture));
        SetMotorStatusValue(items, "PWM", packet.PresentPwm.ToString(CultureInfo.InvariantCulture));
        SetMotorStatusValue(items, "Temperature", $"{packet.PresentTemperature} C");
        SetMotorStatusValue(items, "Voltage", $"{packet.PresentInputVoltage:0.0} V");
        SetMotorStatusValue(items, "Moving", packet.Moving == 0 ? "Stop" : "Moving");
        SetMotorStatusValue(items, "Error Status", $"0x{packet.HardwareErrorStatus:X2}");
        SetMotorStatusValue(items, "Moving Status", $"0x{packet.MovingStatus:X2}");
        SetMotorStatusValue(items, "Last Update", packet.ReceivedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
    }

    private static void SetMotorStatusValue(ObservableCollection<MotorStatusItem> items, string name, string value)
    {
        var item = items.FirstOrDefault(status => status.Name == name);
        if (item is not null) item.Value = value;
    }

    private static IEnumerable<MotorStatusItem> CreateDefaultMotorStatusItems()
    {
        string[] names =
        [
            "Motor Value", "Actual Value", "Motor Change Value", "Actual Change Value", "Velocity", "Load", "PWM",
            "Temperature", "Voltage", "Moving", "Error Status", "Moving Status", "Last Update"
        ];
        return names.Select(name => new MotorStatusItem(name, "-"));
    }

    private static double NormalizeMotorDegrees(double degrees, double limit) =>
        Math.Clamp(Math.Round(degrees, 1, MidpointRounding.AwayFromZero), 0, limit);

    private static double DynamixelPositionToDegrees(ushort position) => position * 360.0 / 4095.0;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

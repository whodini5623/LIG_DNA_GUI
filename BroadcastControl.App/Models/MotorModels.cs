namespace BroadcastControl.App.Models;

public readonly record struct MotorStatusSnapshot(
    MotorStatusPacket Pan,
    MotorStatusPacket? Tilt);

public readonly record struct MotorStatusPacket(
    byte HardwareErrorStatus,
    byte PresentTemperature,
    ushort PresentInputVoltageRaw,
    ushort PresentPosition,
    ushort PresentVelocity,
    short PresentLoad,
    short PresentPwm,
    ushort GoalPosition,
    ushort GoalVelocity,
    byte Moving,
    byte MovingStatus,
    DateTime ReceivedAt)
{
    public double PresentInputVoltage => PresentInputVoltageRaw / 10.0;
}

public enum MotorPacketMode : byte
{
    Scan = 0,
    Tracking = 1,
    Manual = 2
}

[Flags]
public enum MotorButtonMask : byte
{
    None = 0,
    Left = 0x01,
    Right = 0x02,
    Up = 0x04,
    Down = 0x08,
    Center = 0x10
}

public enum MotorStepAxis
{
    Both,
    Pan,
    Tilt
}

public enum MotorStepMode
{
    Auto,
    Manual
}

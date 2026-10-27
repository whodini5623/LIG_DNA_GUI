namespace BroadcastControl.App.ViewModels.Left;

using BroadcastControl.App.Models;
using BroadcastControl.App.Services;

/// <summary>
/// View state and actions for the corresponding MotorControlPanel.
/// </summary>
public sealed class MotorControlPanelViewModel
{
    private readonly UdpMotorControlService _motorControlService;

    public MotorControlPanelViewModel(UdpMotorControlService motorControlService)
    {
        _motorControlService = motorControlService;
    }

    public bool TrySendModePacket(MotorPacketMode mode, out string? error) =>
        _motorControlService.TrySendModePacket(mode, out error);

    public bool TrySendButtonPacket(MotorButtonMask buttons, out string? error) =>
        _motorControlService.TrySendButtonPacket(buttons, out error);

    public bool TrySendStepSizePacket(int panStepSize, int tiltStepSize, out string? error) =>
        _motorControlService.TrySendStepSizePacket(panStepSize, tiltStepSize, out error);

    public bool TrySendTargetAnglePacket(double panDegrees, double tiltDegrees, out string? error) =>
        _motorControlService.TrySendTargetAnglePacket(panDegrees, tiltDegrees, out error);

}

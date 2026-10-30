using DualSenseBridge.Core.DualSense;

namespace DualSenseBridge.Controllers;

public enum SessionState
{
    Connecting,
    Active,
    Error,
    Stopped,
}

/// <summary>Thread-safe, immutable view of a controller session for the UI and tray.</summary>
public sealed record ControllerSnapshot(
    string Id,
    string Name,
    ConnectionType Connection,
    SessionState State,
    string? Message,
    int BatteryPercent,
    ChargingState Charging,
    int PlayerNumber,
    int ReportRate,
    DualSenseInputState Input,
    bool IsHidden,
    ulong BluetoothAddress);

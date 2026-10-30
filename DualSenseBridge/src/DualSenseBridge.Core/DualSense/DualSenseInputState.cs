namespace DualSenseBridge.Core.DualSense;

[Flags]
public enum DualSenseButtons : uint
{
    None = 0,
    Square = 1 << 0,
    Cross = 1 << 1,
    Circle = 1 << 2,
    Triangle = 1 << 3,
    L1 = 1 << 4,
    R1 = 1 << 5,
    L2 = 1 << 6,
    R2 = 1 << 7,
    Create = 1 << 8,
    Options = 1 << 9,
    L3 = 1 << 10,
    R3 = 1 << 11,
    PS = 1 << 12,
    Touchpad = 1 << 13,
    Mute = 1 << 14,
    DPadUp = 1 << 15,
    DPadDown = 1 << 16,
    DPadLeft = 1 << 17,
    DPadRight = 1 << 18,
}

public enum ChargingState
{
    Unknown,
    Discharging,
    Charging,
    Full,
    Error,
}

/// <summary>
/// Decoded snapshot of one DualSense input report.
/// Sticks use the raw HID convention: 0 = left/up, 128 = centre, 255 = right/down.
/// </summary>
public struct DualSenseInputState
{
    public byte LeftX;
    public byte LeftY;
    public byte RightX;
    public byte RightY;
    public byte L2;
    public byte R2;
    public DualSenseButtons Buttons;

    /// <summary>True when the report carried battery information (only the full report does).</summary>
    public bool HasBattery;
    /// <summary>Battery level in percent (0..100), valid when <see cref="HasBattery"/> is set.</summary>
    public int BatteryPercent;
    public ChargingState Charging;

    public static DualSenseInputState Neutral => new()
    {
        LeftX = 128,
        LeftY = 128,
        RightX = 128,
        RightY = 128,
    };

    public readonly bool IsPressed(DualSenseButtons button) => (Buttons & button) == button;
}

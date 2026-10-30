namespace DualSenseBridge.Core.Mapping;

/// <summary>Xbox 360 button bits (identical to XINPUT_GAMEPAD and the ViGEm report).</summary>
[Flags]
public enum XButtons : ushort
{
    None = 0,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    Guide = 0x0400,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
}

/// <summary>Xbox 360 controller report. Thumb Y axes are positive when pushed up.</summary>
public struct XInputState : IEquatable<XInputState>
{
    public XButtons Buttons;
    public byte LeftTrigger;
    public byte RightTrigger;
    public short LeftThumbX;
    public short LeftThumbY;
    public short RightThumbX;
    public short RightThumbY;

    public readonly bool Equals(XInputState other) =>
        Buttons == other.Buttons && LeftTrigger == other.LeftTrigger && RightTrigger == other.RightTrigger &&
        LeftThumbX == other.LeftThumbX && LeftThumbY == other.LeftThumbY &&
        RightThumbX == other.RightThumbX && RightThumbY == other.RightThumbY;

    public override readonly bool Equals(object? obj) => obj is XInputState other && Equals(other);

    public override readonly int GetHashCode() =>
        HashCode.Combine(Buttons, LeftTrigger, RightTrigger, LeftThumbX, LeftThumbY, RightThumbX, RightThumbY);
}

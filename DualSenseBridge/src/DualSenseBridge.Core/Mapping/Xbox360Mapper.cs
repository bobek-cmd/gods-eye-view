using DualSenseBridge.Core.Configuration;
using DualSenseBridge.Core.DualSense;

namespace DualSenseBridge.Core.Mapping;

/// <summary>Translates a DualSense state into an Xbox 360 report.</summary>
public static class Xbox360Mapper
{
    private static readonly (DualSenseButtons From, XButtons To)[] ButtonMap =
    {
        (DualSenseButtons.Cross, XButtons.A),
        (DualSenseButtons.Circle, XButtons.B),
        (DualSenseButtons.Square, XButtons.X),
        (DualSenseButtons.Triangle, XButtons.Y),
        (DualSenseButtons.L1, XButtons.LeftShoulder),
        (DualSenseButtons.R1, XButtons.RightShoulder),
        (DualSenseButtons.L3, XButtons.LeftThumb),
        (DualSenseButtons.R3, XButtons.RightThumb),
        (DualSenseButtons.Create, XButtons.Back),
        (DualSenseButtons.Options, XButtons.Start),
        (DualSenseButtons.PS, XButtons.Guide),
        (DualSenseButtons.DPadUp, XButtons.DPadUp),
        (DualSenseButtons.DPadDown, XButtons.DPadDown),
        (DualSenseButtons.DPadLeft, XButtons.DPadLeft),
        (DualSenseButtons.DPadRight, XButtons.DPadRight),
    };

    public static XInputState Map(in DualSenseInputState input, MappingProfile profile)
    {
        var output = new XInputState();

        foreach (var (from, to) in ButtonMap)
        {
            if ((input.Buttons & from) != 0)
            {
                output.Buttons |= to;
            }
        }

        if ((input.Buttons & DualSenseButtons.Touchpad) != 0)
        {
            output.Buttons |= profile.TouchpadClick switch
            {
                TouchpadClickAction.Back => XButtons.Back,
                TouchpadClickAction.Start => XButtons.Start,
                TouchpadClickAction.Guide => XButtons.Guide,
                _ => XButtons.None,
            };
        }

        (output.LeftThumbX, output.LeftThumbY) = MapStick(input.LeftX, input.LeftY, profile.LeftDeadzone);
        (output.RightThumbX, output.RightThumbY) = MapStick(input.RightX, input.RightY, profile.RightDeadzone);
        output.LeftTrigger = MapTrigger(input.L2, profile.TriggerDeadzone);
        output.RightTrigger = MapTrigger(input.R2, profile.TriggerDeadzone);

        return output;
    }

    /// <summary>
    /// Converts raw stick bytes to signed Xbox axes, applying a scaled radial dead zone.
    /// The Y axis is inverted because HID reports "down" as positive while XInput reports "up" as positive.
    /// </summary>
    public static (short X, short Y) MapStick(byte rawX, byte rawY, double deadzone)
    {
        double x = Normalize(rawX);
        double y = -Normalize(rawY);

        double magnitude = Math.Sqrt(x * x + y * y);
        if (magnitude <= deadzone || magnitude == 0)
        {
            return (0, 0);
        }

        if (deadzone > 0)
        {
            double scaled = Math.Min((magnitude - deadzone) / (1.0 - deadzone), 1.0);
            double factor = scaled / magnitude;
            x *= factor;
            y *= factor;
        }

        return (ToAxis(x), ToAxis(y));
    }

    public static byte MapTrigger(byte raw, double deadzone)
    {
        double threshold = deadzone * 255.0;
        if (raw <= threshold)
        {
            return 0;
        }

        if (deadzone <= 0)
        {
            return raw;
        }

        double scaled = (raw - threshold) / (255.0 - threshold) * 255.0;
        return (byte)Math.Clamp(Math.Round(scaled), 0, 255);
    }

    /// <summary>Maps 0..255 (centre 128) to -1..1 with 128 exactly at zero.</summary>
    private static double Normalize(byte raw) => raw >= 128 ? (raw - 128) / 127.0 : (raw - 128) / 128.0;

    private static short ToAxis(double value)
    {
        value = Math.Clamp(value, -1.0, 1.0);
        return value >= 0 ? (short)Math.Round(value * 32767.0) : (short)Math.Round(value * 32768.0);
    }
}

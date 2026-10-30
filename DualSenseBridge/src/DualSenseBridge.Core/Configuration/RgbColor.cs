using System.Globalization;

namespace DualSenseBridge.Core.Configuration;

public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static RgbColor Black => new(0, 0, 0);

    public static bool TryParse(string? text, out RgbColor color)
    {
        color = Black;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string hex = text.Trim().TrimStart('#');
        if (hex.Length != 6 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
        {
            return false;
        }

        color = new RgbColor((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return true;
    }

    public RgbColor Scale(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        return new RgbColor((byte)(R * percent / 100), (byte)(G * percent / 100), (byte)(B * percent / 100));
    }

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}

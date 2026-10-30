using DualSenseBridge.Core.Configuration;

namespace DualSenseBridge.Core.Mapping;

public static class LightbarPolicy
{
    private static readonly RgbColor[] PlayerColors =
    {
        new(0x00, 0x70, 0xD1), // 1: PlayStation blue
        new(0xE0, 0x1E, 0x2D), // 2: red
        new(0x1F, 0xC2, 0x4A), // 3: green
        new(0xE8, 0x3E, 0xC8), // 4: pink
    };

    /// <param name="playerNumber">1-based Xbox player slot, 0 if unknown.</param>
    /// <param name="batteryPercent">Battery level, negative if unknown.</param>
    public static RgbColor Resolve(AppSettings settings, int playerNumber, int batteryPercent)
    {
        RgbColor color = settings.LightbarMode switch
        {
            LightbarMode.Off => RgbColor.Black,
            LightbarMode.Custom => RgbColor.TryParse(settings.LightbarColor, out var c) ? c : PlayerColors[0],
            LightbarMode.Battery => BatteryColor(batteryPercent),
            _ => playerNumber >= 1 && playerNumber <= PlayerColors.Length ? PlayerColors[playerNumber - 1] : PlayerColors[0],
        };

        return color.Scale(settings.LightbarBrightness);
    }

    public static RgbColor BatteryColor(int batteryPercent)
    {
        if (batteryPercent < 0)
        {
            return PlayerColors[0];
        }

        // Linear red -> yellow -> green.
        double t = Math.Clamp(batteryPercent / 100.0, 0, 1);
        byte r = (byte)(t < 0.5 ? 255 : Math.Round(255 * (1 - t) * 2));
        byte g = (byte)(t > 0.5 ? 255 : Math.Round(255 * t * 2));
        return new RgbColor(r, g, 0);
    }
}

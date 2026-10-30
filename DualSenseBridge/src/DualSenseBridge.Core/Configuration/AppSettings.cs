namespace DualSenseBridge.Core.Configuration;

public enum TouchpadClickAction
{
    None,
    Back,
    Start,
    Guide,
}

public enum LightbarMode
{
    /// <summary>Colour per Xbox player slot (blue, red, green, pink).</summary>
    PlayerColor,
    /// <summary>Green to red depending on the battery level.</summary>
    Battery,
    /// <summary>User selected colour.</summary>
    Custom,
    Off,
}

/// <summary>User settings, persisted as JSON in %AppData%\DualSenseBridge\settings.json.</summary>
public sealed class AppSettings
{
    public bool StartWithWindows { get; set; } = true;
    public bool StartMinimized { get; set; } = true;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;

    /// <summary>Hide the physical DualSense from games through HidHide so only the virtual Xbox 360 pad is seen.</summary>
    public bool HideOriginalController { get; set; } = true;

    public bool RumbleEnabled { get; set; } = true;
    /// <summary>Rumble strength in percent (0..100).</summary>
    public int RumbleStrength { get; set; } = 100;

    /// <summary>Radial dead zone in percent (0..50).</summary>
    public int LeftStickDeadzone { get; set; } = 6;
    public int RightStickDeadzone { get; set; } = 6;
    /// <summary>Trigger dead zone in percent (0..50).</summary>
    public int TriggerDeadzone { get; set; } = 2;

    public TouchpadClickAction TouchpadClick { get; set; } = TouchpadClickAction.Back;

    public LightbarMode LightbarMode { get; set; } = LightbarMode.PlayerColor;
    /// <summary>Custom lightbar colour as #RRGGBB.</summary>
    public string LightbarColor { get; set; } = "#0070D1";
    /// <summary>Lightbar brightness in percent (0..100).</summary>
    public int LightbarBrightness { get; set; } = 60;

    /// <summary>Show a notification when the battery drops to this level (percent, 0 = off).</summary>
    public int LowBatteryWarning { get; set; } = 20;

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    /// <summary>Clamps every numeric value into its valid range.</summary>
    public void Normalize()
    {
        RumbleStrength = Math.Clamp(RumbleStrength, 0, 100);
        LeftStickDeadzone = Math.Clamp(LeftStickDeadzone, 0, 50);
        RightStickDeadzone = Math.Clamp(RightStickDeadzone, 0, 50);
        TriggerDeadzone = Math.Clamp(TriggerDeadzone, 0, 50);
        LightbarBrightness = Math.Clamp(LightbarBrightness, 0, 100);
        LowBatteryWarning = Math.Clamp(LowBatteryWarning, 0, 50);
        if (!RgbColor.TryParse(LightbarColor, out _))
        {
            LightbarColor = "#0070D1";
        }
    }
}

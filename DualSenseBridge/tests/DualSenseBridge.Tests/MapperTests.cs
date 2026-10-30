using DualSenseBridge.Core.Configuration;
using DualSenseBridge.Core.DualSense;
using DualSenseBridge.Core.Mapping;

namespace DualSenseBridge.Tests;

public class MapperTests
{
    private static readonly MappingProfile NoDeadzone = new(0, 0, 0, TouchpadClickAction.Back);

    [Theory]
    [InlineData(0, -32768)]
    [InlineData(128, 0)]
    [InlineData(255, 32767)]
    public void MapsStickXWithoutDeadzone(byte raw, short expected)
    {
        Assert.Equal(expected, Xbox360Mapper.MapStick(raw, 128, 0).X);
    }

    [Fact]
    public void InvertsStickY()
    {
        Assert.Equal(32767, Xbox360Mapper.MapStick(128, 0, 0).Y);     // pushed up
        Assert.Equal(-32768, Xbox360Mapper.MapStick(128, 255, 0).Y);  // pushed down
    }

    [Fact]
    public void AppliesRadialDeadzone()
    {
        Assert.Equal((0, 0), Xbox360Mapper.MapStick(134, 122, 0.1));
        var (x, _) = Xbox360Mapper.MapStick(255, 128, 0.1);
        Assert.Equal(32767, x);
        var (half, _) = Xbox360Mapper.MapStick(192, 128, 0.1);
        Assert.InRange(half, 14000, 16500);
    }

    [Fact]
    public void AppliesTriggerDeadzone()
    {
        Assert.Equal(0, Xbox360Mapper.MapTrigger(5, 0.05));
        Assert.Equal(255, Xbox360Mapper.MapTrigger(255, 0.05));
        Assert.Equal(77, Xbox360Mapper.MapTrigger(77, 0));
    }

    [Fact]
    public void MapsButtons()
    {
        var input = DualSenseInputState.Neutral;
        input.Buttons = DualSenseButtons.Cross | DualSenseButtons.Circle | DualSenseButtons.Square | DualSenseButtons.Triangle
                        | DualSenseButtons.Options | DualSenseButtons.Create | DualSenseButtons.PS
                        | DualSenseButtons.L1 | DualSenseButtons.R3 | DualSenseButtons.DPadUp | DualSenseButtons.Mute;

        var x = Xbox360Mapper.Map(input, NoDeadzone);

        var expected = XButtons.A | XButtons.B | XButtons.X | XButtons.Y | XButtons.Start | XButtons.Back
                       | XButtons.Guide | XButtons.LeftShoulder | XButtons.RightThumb | XButtons.DPadUp;
        Assert.Equal(expected, x.Buttons);
        Assert.Equal(0, x.LeftThumbX);
        Assert.Equal(0, x.RightThumbY);
    }

    [Theory]
    [InlineData(TouchpadClickAction.None, XButtons.None)]
    [InlineData(TouchpadClickAction.Back, XButtons.Back)]
    [InlineData(TouchpadClickAction.Start, XButtons.Start)]
    [InlineData(TouchpadClickAction.Guide, XButtons.Guide)]
    public void MapsTouchpadClick(TouchpadClickAction action, XButtons expected)
    {
        var input = DualSenseInputState.Neutral;
        input.Buttons = DualSenseButtons.Touchpad;
        Assert.Equal(expected, Xbox360Mapper.Map(input, NoDeadzone with { TouchpadClick = action }).Buttons);
    }

    [Fact]
    public void ResolvesLightbarColours()
    {
        var settings = new AppSettings { LightbarBrightness = 100, LightbarMode = LightbarMode.Custom, LightbarColor = "#112233" };
        Assert.Equal(new RgbColor(0x11, 0x22, 0x33), LightbarPolicy.Resolve(settings, 1, 50));

        settings.LightbarMode = LightbarMode.Off;
        Assert.Equal(RgbColor.Black, LightbarPolicy.Resolve(settings, 1, 50));

        settings.LightbarMode = LightbarMode.Battery;
        Assert.Equal(new RgbColor(0, 255, 0), LightbarPolicy.Resolve(settings, 1, 100));
        Assert.Equal(new RgbColor(255, 0, 0), LightbarPolicy.Resolve(settings, 1, 0));
    }

    [Fact]
    public void ConvertsDevicePathToInstanceId()
    {
        const string path = @"\\?\hid#{00001124-0000-1000-8000-00805f9b34fb}_vid&0002054c_pid&0ce6#8&2c1a3b9f&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
        Assert.True(DevicePathHelper.IsBluetooth(path));
        Assert.Equal(@"HID\{00001124-0000-1000-8000-00805F9B34FB}_VID&0002054C_PID&0CE6\8&2C1A3B9F&0&0000", DevicePathHelper.ToInstanceId(path));
        Assert.False(DevicePathHelper.IsBluetooth(@"\\?\hid#vid_054c&pid_0ce6&mi_03#7&1&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}"));
    }

    [Fact]
    public void NormalizesSettings()
    {
        var s = new AppSettings { RumbleStrength = 400, LeftStickDeadzone = -3, LightbarColor = "nope" };
        s.Normalize();
        Assert.Equal(100, s.RumbleStrength);
        Assert.Equal(0, s.LeftStickDeadzone);
        Assert.Equal("#0070D1", s.LightbarColor);
    }
}

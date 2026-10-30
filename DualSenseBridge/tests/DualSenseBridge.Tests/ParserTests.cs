using DualSenseBridge.Core.DualSense;

namespace DualSenseBridge.Tests;

public class ParserTests
{
    [Fact]
    public void ParsesBluetoothFullReport()
    {
        byte[] report = ReportBuilder.Bluetooth(c =>
        {
            c[0] = 0;     // LX full left
            c[1] = 255;   // LY full down
            c[2] = 200;
            c[3] = 50;
            c[4] = 10;    // L2
            c[5] = 250;   // R2
            c[7] = 0x20 | 0x02;   // Cross + d-pad right
            c[8] = 0x01 | 0x20;   // L1 + Options
            c[9] = 0x01;          // PS
            c[52] = 0x07;         // discharging, level 7
        });

        var result = DualSenseReportParser.Parse(report, ConnectionType.Bluetooth, out var state);

        Assert.Equal(ParseResult.Full, result);
        Assert.Equal(0, state.LeftX);
        Assert.Equal(255, state.LeftY);
        Assert.Equal(200, state.RightX);
        Assert.Equal(50, state.RightY);
        Assert.Equal(10, state.L2);
        Assert.Equal(250, state.R2);
        Assert.True(state.IsPressed(DualSenseButtons.Cross));
        Assert.True(state.IsPressed(DualSenseButtons.DPadRight));
        Assert.False(state.IsPressed(DualSenseButtons.DPadUp));
        Assert.True(state.IsPressed(DualSenseButtons.L1));
        Assert.True(state.IsPressed(DualSenseButtons.Options));
        Assert.True(state.IsPressed(DualSenseButtons.PS));
        Assert.False(state.IsPressed(DualSenseButtons.Square));
        Assert.True(state.HasBattery);
        Assert.Equal(75, state.BatteryPercent);
        Assert.Equal(ChargingState.Discharging, state.Charging);
    }

    [Fact]
    public void RejectsBluetoothReportWithBadCrc()
    {
        byte[] report = ReportBuilder.Bluetooth(_ => { }, validCrc: false);
        Assert.Equal(ParseResult.CrcMismatch, DualSenseReportParser.Parse(report, ConnectionType.Bluetooth, out _));
    }

    [Fact]
    public void ParsesUsbReport()
    {
        byte[] report = ReportBuilder.Usb(c =>
        {
            c[7] = 0x80 | 0x05; // Triangle + d-pad down-left
            c[52] = 0x23;       // full
        });

        Assert.Equal(ParseResult.Full, DualSenseReportParser.Parse(report, ConnectionType.Usb, out var state));
        Assert.True(state.IsPressed(DualSenseButtons.Triangle));
        Assert.True(state.IsPressed(DualSenseButtons.DPadDown | DualSenseButtons.DPadLeft));
        Assert.Equal(100, state.BatteryPercent);
        Assert.Equal(ChargingState.Full, state.Charging);
    }

    [Theory]
    [InlineData(0x10, 5, ChargingState.Charging)]
    [InlineData(0x1A, 100, ChargingState.Charging)]
    [InlineData(0x00, 5, ChargingState.Discharging)]
    [InlineData(0xF3, 35, ChargingState.Error)]
    public void DecodesBatteryStatus(byte status, int percent, ChargingState charging)
    {
        byte[] report = ReportBuilder.Bluetooth(c => c[52] = status);
        DualSenseReportParser.Parse(report, ConnectionType.Bluetooth, out var state);
        Assert.Equal(percent, state.BatteryPercent);
        Assert.Equal(charging, state.Charging);
    }

    [Fact]
    public void ParsesSimpleBluetoothReport()
    {
        var report = new byte[DualSenseConstants.BluetoothInputReportLength];
        report[0] = 0x01;
        report[1] = 10;
        report[2] = 20;
        report[3] = 30;
        report[4] = 40;
        report[5] = 0x40 | 0x08; // Circle, d-pad released
        report[6] = 0x02;        // R1
        report[7] = 0x01 | 0xFC; // PS + counter bits that must be ignored
        report[8] = 100;
        report[9] = 200;

        Assert.Equal(ParseResult.Simple, DualSenseReportParser.Parse(report, ConnectionType.Bluetooth, out var state));
        Assert.Equal(10, state.LeftX);
        Assert.Equal(40, state.RightY);
        Assert.True(state.IsPressed(DualSenseButtons.Circle));
        Assert.True(state.IsPressed(DualSenseButtons.R1));
        Assert.True(state.IsPressed(DualSenseButtons.PS));
        Assert.False(state.IsPressed(DualSenseButtons.Mute));
        Assert.Equal(100, state.L2);
        Assert.Equal(200, state.R2);
        Assert.False(state.HasBattery);
    }

    [Fact]
    public void IgnoresUnknownReportIds()
    {
        var report = new byte[78];
        report[0] = 0x05;
        Assert.Equal(ParseResult.Ignored, DualSenseReportParser.Parse(report, ConnectionType.Bluetooth, out _));
        Assert.Equal(ParseResult.Ignored, DualSenseReportParser.Parse(report, ConnectionType.Usb, out _));
    }

    [Fact]
    public void RejectsTruncatedReports()
    {
        Assert.Equal(ParseResult.TooShort, DualSenseReportParser.Parse(new byte[] { 0x31, 0, 0 }, ConnectionType.Bluetooth, out _));
        Assert.Equal(ParseResult.TooShort, DualSenseReportParser.Parse(new byte[] { 0x01, 0, 0 }, ConnectionType.Usb, out _));
        Assert.Equal(ParseResult.TooShort, DualSenseReportParser.Parse(Array.Empty<byte>(), ConnectionType.Usb, out _));
    }
}

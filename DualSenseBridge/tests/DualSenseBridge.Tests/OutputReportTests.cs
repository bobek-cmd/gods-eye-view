using DualSenseBridge.Core.DualSense;

namespace DualSenseBridge.Tests;

public class OutputReportTests
{
    private static readonly DualSenseOutputState SampleState = new()
    {
        StrongMotor = 200,
        WeakMotor = 50,
        LightbarRed = 1,
        LightbarGreen = 2,
        LightbarBlue = 3,
        PlayerLeds = 0x04,
    };

    [Fact]
    public void BuildsBluetoothReportWithValidCrc()
    {
        byte[] report = DualSenseOutputReport.Build(ConnectionType.Bluetooth, SampleState, sequence: 5, useVibrationV2: true, releaseLightbar: false);

        Assert.Equal(78, report.Length);
        Assert.Equal(0x31, report[0]);
        Assert.Equal(0x50, report[1]);
        Assert.Equal(0x10, report[2]);
        Assert.Equal(0x02, report[3]);       // haptics select, legacy vibration flag off
        Assert.Equal(0x14, report[4]);       // lightbar + player LEDs
        Assert.Equal(50, report[5]);         // right / weak
        Assert.Equal(200, report[6]);        // left / strong
        Assert.Equal(0x04, report[3 + 38]);  // vibration v2
        Assert.Equal(0x04, report[3 + 43]);
        Assert.Equal(1, report[3 + 44]);
        Assert.Equal(2, report[3 + 45]);
        Assert.Equal(3, report[3 + 46]);
        Assert.True(DualSenseReportParser.HasValidCrc(report, DualSenseConstants.OutputCrcSeed));
    }

    [Fact]
    public void UsesLegacyVibrationFlagOnOldFirmware()
    {
        byte[] report = DualSenseOutputReport.Build(ConnectionType.Usb, SampleState, 0, useVibrationV2: false, releaseLightbar: false);

        Assert.Equal(0x02, report[0]);
        Assert.Equal(0x03, report[1]);
        Assert.Equal(0x00, report[1 + 38]);
        Assert.Equal(200, report[1 + 3]);
    }

    [Fact]
    public void ReleaseLightbarSendsLightOut()
    {
        byte[] report = DualSenseOutputReport.Build(ConnectionType.Bluetooth, SampleState, 0, true, releaseLightbar: true);
        Assert.Equal(0x02 | 0x04, report[3 + 38]);
        Assert.Equal(0x02, report[3 + 41]);
        Assert.Equal(0x00, report[4]); // no colour update in the same report
    }

    [Fact]
    public void PadsToDeviceReportLength()
    {
        byte[] report = DualSenseOutputReport.Build(ConnectionType.Usb, SampleState, 0, true, false, length: 48);
        Assert.Equal(48, report.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DualSenseOutputReport.Build(ConnectionType.Bluetooth, SampleState, 0, true, false, length: 64));
    }

    [Fact]
    public void ReadsFirmwareVersionAndMac()
    {
        var firmware = new byte[64];
        firmware[0] = 0x20;
        firmware[44] = 0x24;
        firmware[45] = 0x02;
        Assert.True(DualSenseOutputReport.TryGetUpdateVersion(firmware, out ushort version));
        Assert.Equal(0x0224, version);

        var pairing = new byte[20];
        pairing[0] = 0x09;
        byte[] mac = { 0x66, 0x55, 0x44, 0x33, 0x22, 0x11 };
        mac.CopyTo(pairing, 1);
        Assert.True(DualSenseOutputReport.TryGetMacAddress(pairing, out ulong address));
        Assert.Equal(0x112233445566UL, address);
    }

    [Fact]
    public void PlayerLedPatterns()
    {
        Assert.Equal(0x04, DualSenseOutputReport.PlayerLedsFor(1));
        Assert.Equal(0x0A, DualSenseOutputReport.PlayerLedsFor(2));
        Assert.Equal(0, DualSenseOutputReport.PlayerLedsFor(0));
    }
}

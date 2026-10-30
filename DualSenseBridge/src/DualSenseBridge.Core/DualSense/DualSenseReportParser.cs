using System.Buffers.Binary;

namespace DualSenseBridge.Core.DualSense;

public enum ConnectionType
{
    Usb,
    Bluetooth,
}

public enum ParseResult
{
    /// <summary>Full report decoded (sticks, buttons, triggers and battery).</summary>
    Full,
    /// <summary>Reduced Bluetooth report decoded (no battery). Controller still needs the switch to full mode.</summary>
    Simple,
    /// <summary>Report id not relevant for us.</summary>
    Ignored,
    TooShort,
    CrcMismatch,
}

/// <summary>
/// Decodes DualSense HID input reports. Layout follows the Linux hid-playstation driver.
/// </summary>
public static class DualSenseReportParser
{
    // Offsets inside the common input structure (USB report minus report id,
    // Bluetooth 0x31 report minus report id and sequence byte).
    private const int OffLeftX = 0;
    private const int OffLeftY = 1;
    private const int OffRightX = 2;
    private const int OffRightY = 3;
    private const int OffL2 = 4;
    private const int OffR2 = 5;
    private const int OffButtons0 = 7;
    private const int OffButtons1 = 8;
    private const int OffButtons2 = 9;
    private const int OffStatus = 52;
    private const int CommonLength = OffStatus + 1;

    public static ParseResult Parse(ReadOnlySpan<byte> report, ConnectionType connection, out DualSenseInputState state)
    {
        state = DualSenseInputState.Neutral;
        if (report.Length < 1)
        {
            return ParseResult.TooShort;
        }

        if (connection == ConnectionType.Bluetooth)
        {
            if (report[0] == DualSenseConstants.BluetoothInputReportId)
            {
                if (report.Length < DualSenseConstants.BluetoothInputReportLength)
                {
                    return ParseResult.TooShort;
                }

                if (!HasValidCrc(report[..DualSenseConstants.BluetoothInputReportLength], DualSenseConstants.InputCrcSeed))
                {
                    return ParseResult.CrcMismatch;
                }

                DecodeCommon(report.Slice(2, CommonLength), ref state);
                return ParseResult.Full;
            }

            if (report[0] == DualSenseConstants.BluetoothSimpleInputReportId)
            {
                if (report.Length < 10)
                {
                    return ParseResult.TooShort;
                }

                DecodeSimple(report, ref state);
                return ParseResult.Simple;
            }

            return ParseResult.Ignored;
        }

        if (report[0] != DualSenseConstants.UsbInputReportId)
        {
            return ParseResult.Ignored;
        }

        if (report.Length < DualSenseConstants.UsbInputReportLength)
        {
            return ParseResult.TooShort;
        }

        DecodeCommon(report.Slice(1, CommonLength), ref state);
        return ParseResult.Full;
    }

    /// <summary>Verifies the trailing little-endian CRC32 of a Bluetooth report.</summary>
    public static bool HasValidCrc(ReadOnlySpan<byte> report, byte seed)
    {
        if (report.Length < 5)
        {
            return false;
        }

        int payloadLength = report.Length - 4;
        uint expected = BinaryPrimitives.ReadUInt32LittleEndian(report[payloadLength..]);
        return Crc32.ComputeWithSeed(seed, report[..payloadLength]) == expected;
    }

    private static void DecodeCommon(ReadOnlySpan<byte> d, ref DualSenseInputState state)
    {
        state.LeftX = d[OffLeftX];
        state.LeftY = d[OffLeftY];
        state.RightX = d[OffRightX];
        state.RightY = d[OffRightY];
        state.L2 = d[OffL2];
        state.R2 = d[OffR2];
        state.Buttons = DecodeButtons(d[OffButtons0], d[OffButtons1], d[OffButtons2]);

        byte status = d[OffStatus];
        int level = status & 0x0F;
        int chargingNibble = (status & 0xF0) >> 4;
        state.HasBattery = true;
        switch (chargingNibble)
        {
            case 0x0:
                state.Charging = ChargingState.Discharging;
                state.BatteryPercent = Math.Min(level * 10 + 5, 100);
                break;
            case 0x1:
                state.Charging = ChargingState.Charging;
                state.BatteryPercent = Math.Min(level * 10 + 5, 100);
                break;
            case 0x2:
                state.Charging = ChargingState.Full;
                state.BatteryPercent = 100;
                break;
            default:
                // 0xA / 0xB: voltage or temperature out of range, 0xF: charging error.
                state.Charging = ChargingState.Error;
                state.BatteryPercent = Math.Min(level * 10 + 5, 100);
                break;
        }
    }

    /// <summary>
    /// Reduced Bluetooth report (id 0x01) sent right after pairing, before the
    /// host has read the calibration feature report. DualShock 4 compatible layout.
    /// </summary>
    private static void DecodeSimple(ReadOnlySpan<byte> r, ref DualSenseInputState state)
    {
        state.LeftX = r[1];
        state.LeftY = r[2];
        state.RightX = r[3];
        state.RightY = r[4];
        state.Buttons = DecodeButtons(r[5], r[6], (byte)(r[7] & 0x03));
        state.L2 = r[8];
        state.R2 = r[9];
        state.HasBattery = false;
        state.Charging = ChargingState.Unknown;
    }

    internal static DualSenseButtons DecodeButtons(byte b0, byte b1, byte b2)
    {
        var buttons = DualSenseButtons.None;

        buttons |= (b0 & 0x0F) switch
        {
            0 => DualSenseButtons.DPadUp,
            1 => DualSenseButtons.DPadUp | DualSenseButtons.DPadRight,
            2 => DualSenseButtons.DPadRight,
            3 => DualSenseButtons.DPadDown | DualSenseButtons.DPadRight,
            4 => DualSenseButtons.DPadDown,
            5 => DualSenseButtons.DPadDown | DualSenseButtons.DPadLeft,
            6 => DualSenseButtons.DPadLeft,
            7 => DualSenseButtons.DPadUp | DualSenseButtons.DPadLeft,
            _ => DualSenseButtons.None, // 8 = released
        };

        if ((b0 & 0x10) != 0) buttons |= DualSenseButtons.Square;
        if ((b0 & 0x20) != 0) buttons |= DualSenseButtons.Cross;
        if ((b0 & 0x40) != 0) buttons |= DualSenseButtons.Circle;
        if ((b0 & 0x80) != 0) buttons |= DualSenseButtons.Triangle;

        if ((b1 & 0x01) != 0) buttons |= DualSenseButtons.L1;
        if ((b1 & 0x02) != 0) buttons |= DualSenseButtons.R1;
        if ((b1 & 0x04) != 0) buttons |= DualSenseButtons.L2;
        if ((b1 & 0x08) != 0) buttons |= DualSenseButtons.R2;
        if ((b1 & 0x10) != 0) buttons |= DualSenseButtons.Create;
        if ((b1 & 0x20) != 0) buttons |= DualSenseButtons.Options;
        if ((b1 & 0x40) != 0) buttons |= DualSenseButtons.L3;
        if ((b1 & 0x80) != 0) buttons |= DualSenseButtons.R3;

        if ((b2 & 0x01) != 0) buttons |= DualSenseButtons.PS;
        if ((b2 & 0x02) != 0) buttons |= DualSenseButtons.Touchpad;
        if ((b2 & 0x04) != 0) buttons |= DualSenseButtons.Mute;

        return buttons;
    }
}

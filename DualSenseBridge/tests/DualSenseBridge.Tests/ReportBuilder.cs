using System.Buffers.Binary;
using DualSenseBridge.Core.DualSense;

namespace DualSenseBridge.Tests;

internal delegate void CommonFiller(Span<byte> common);

/// <summary>Builds synthetic input reports in the DualSense wire format.</summary>
internal static class ReportBuilder
{
    public static byte[] Bluetooth(CommonFiller fillCommon, bool validCrc = true)
    {
        var report = new byte[DualSenseConstants.BluetoothInputReportLength];
        report[0] = DualSenseConstants.BluetoothInputReportId;
        report[1] = 0x00;
        FillDefaults(report.AsSpan(2));
        fillCommon(report.AsSpan(2));
        uint crc = Crc32.ComputeWithSeed(DualSenseConstants.InputCrcSeed, report.AsSpan(0, 74));
        if (!validCrc)
        {
            crc ^= 0xDEADBEEF;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(report.AsSpan(74), crc);
        return report;
    }

    public static byte[] Usb(CommonFiller fillCommon)
    {
        var report = new byte[DualSenseConstants.UsbInputReportLength];
        report[0] = DualSenseConstants.UsbInputReportId;
        FillDefaults(report.AsSpan(1));
        fillCommon(report.AsSpan(1));
        return report;
    }

    private static void FillDefaults(Span<byte> common)
    {
        common[0] = 128;
        common[1] = 128;
        common[2] = 128;
        common[3] = 128;
        common[7] = 0x08; // d-pad released
    }
}

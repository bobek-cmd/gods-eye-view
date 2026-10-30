using System.Buffers.Binary;

namespace DualSenseBridge.Core.DualSense;

/// <summary>Desired state of the controller's actuators and LEDs.</summary>
public struct DualSenseOutputState
{
    /// <summary>Strong / low frequency motor (Xbox "large motor").</summary>
    public byte StrongMotor;
    /// <summary>Weak / high frequency motor (Xbox "small motor").</summary>
    public byte WeakMotor;
    public byte LightbarRed;
    public byte LightbarGreen;
    public byte LightbarBlue;
    /// <summary>Bit mask of the five white player LEDs under the touchpad.</summary>
    public byte PlayerLeds;

    public readonly bool Equals(in DualSenseOutputState other) =>
        StrongMotor == other.StrongMotor && WeakMotor == other.WeakMotor &&
        LightbarRed == other.LightbarRed && LightbarGreen == other.LightbarGreen &&
        LightbarBlue == other.LightbarBlue && PlayerLeds == other.PlayerLeds;
}

/// <summary>
/// Builds DualSense output reports (rumble, lightbar, player LEDs).
/// Layout follows the Linux hid-playstation driver.
/// </summary>
public static class DualSenseOutputReport
{
    private const byte Flag0CompatibleVibration = 0x01;
    private const byte Flag0HapticsSelect = 0x02;
    private const byte Flag1LightbarControl = 0x04;
    private const byte Flag1PlayerIndicatorControl = 0x10;
    private const byte Flag2LightbarSetupControl = 0x02;
    private const byte Flag2CompatibleVibration2 = 0x04;
    private const byte LightbarSetupLightOut = 0x02;
    private const byte BluetoothOutputTag = 0x10;

    // Offsets inside the common output structure.
    private const int OffFlag0 = 0;
    private const int OffFlag1 = 1;
    private const int OffMotorRight = 2;
    private const int OffMotorLeft = 3;
    private const int OffFlag2 = 38;
    private const int OffLightbarSetup = 41;
    private const int OffLedBrightness = 42;
    private const int OffPlayerLeds = 43;
    private const int OffRed = 44;
    private const int OffGreen = 45;
    private const int OffBlue = 46;

    /// <summary>Player LED patterns used by the PS5 for players 1..5.</summary>
    private static readonly byte[] PlayerPatterns = { 0x04, 0x0A, 0x15, 0x1B, 0x1F };

    public static byte PlayerLedsFor(int playerNumber) =>
        playerNumber >= 1 && playerNumber <= PlayerPatterns.Length ? PlayerPatterns[playerNumber - 1] : (byte)0;

    /// <summary>
    /// Builds a full output report.
    /// </summary>
    /// <param name="connection">Transport the report will be written to.</param>
    /// <param name="state">Rumble and LED state.</param>
    /// <param name="sequence">Bluetooth sequence number (0..15), ignored on USB.</param>
    /// <param name="useVibrationV2">True for firmware 2.21 and newer.</param>
    /// <param name="releaseLightbar">Sends the "light out" setup command that releases the
    /// pulsing blue Bluetooth pairing animation so the host can set the colour.</param>
    /// <param name="length">Total buffer length (the device's maximum output report length);
    /// 0 picks the default length for the transport.</param>
    public static byte[] Build(
        ConnectionType connection,
        in DualSenseOutputState state,
        byte sequence,
        bool useVibrationV2,
        bool releaseLightbar,
        int length = 0)
    {
        int defaultLength = connection == ConnectionType.Bluetooth
            ? DualSenseConstants.BluetoothOutputReportLength
            : DualSenseConstants.UsbOutputReportLength;
        if (length <= 0)
        {
            length = defaultLength;
        }

        if (connection == ConnectionType.Bluetooth && length < DualSenseConstants.BluetoothOutputReportLength)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Bluetooth output reports must be at least 78 bytes.");
        }

        if (connection == ConnectionType.Usb && length < 48)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "USB output reports must be at least 48 bytes.");
        }

        var report = new byte[length];
        int commonOffset;
        if (connection == ConnectionType.Bluetooth)
        {
            report[0] = DualSenseConstants.BluetoothOutputReportId;
            report[1] = (byte)((sequence & 0x0F) << 4);
            report[2] = BluetoothOutputTag;
            commonOffset = 3;
        }
        else
        {
            report[0] = DualSenseConstants.UsbOutputReportId;
            commonOffset = 1;
        }

        Span<byte> c = report.AsSpan(commonOffset, 47);

        c[OffFlag0] = Flag0HapticsSelect;
        if (useVibrationV2)
        {
            c[OffFlag2] |= Flag2CompatibleVibration2;
        }
        else
        {
            c[OffFlag0] |= Flag0CompatibleVibration;
        }

        c[OffMotorRight] = state.WeakMotor;
        c[OffMotorLeft] = state.StrongMotor;

        if (releaseLightbar)
        {
            c[OffFlag2] |= Flag2LightbarSetupControl;
            c[OffLightbarSetup] = LightbarSetupLightOut;
        }
        else
        {
            c[OffFlag1] = Flag1LightbarControl | Flag1PlayerIndicatorControl;
            c[OffLedBrightness] = 0x00; // 0 = brightest player LEDs
            c[OffPlayerLeds] = state.PlayerLeds;
            c[OffRed] = state.LightbarRed;
            c[OffGreen] = state.LightbarGreen;
            c[OffBlue] = state.LightbarBlue;
        }

        if (connection == ConnectionType.Bluetooth)
        {
            int crcOffset = DualSenseConstants.BluetoothOutputReportLength - 4;
            uint crc = Crc32.ComputeWithSeed(DualSenseConstants.OutputCrcSeed, report.AsSpan(0, crcOffset));
            BinaryPrimitives.WriteUInt32LittleEndian(report.AsSpan(crcOffset, 4), crc);
        }

        return report;
    }

    /// <summary>Reads the firmware "update version" from a 0x20 feature report.</summary>
    public static bool TryGetUpdateVersion(ReadOnlySpan<byte> firmwareFeatureReport, out ushort version)
    {
        version = 0;
        if (firmwareFeatureReport.Length < 46 || firmwareFeatureReport[0] != DualSenseConstants.FirmwareInfoFeatureReportId)
        {
            return false;
        }

        version = BinaryPrimitives.ReadUInt16LittleEndian(firmwareFeatureReport.Slice(44, 2));
        return true;
    }

    /// <summary>Reads the controller Bluetooth MAC address from a 0x09 feature report.</summary>
    public static bool TryGetMacAddress(ReadOnlySpan<byte> pairingFeatureReport, out ulong address)
    {
        address = 0;
        if (pairingFeatureReport.Length < 7 || pairingFeatureReport[0] != DualSenseConstants.PairingInfoFeatureReportId)
        {
            return false;
        }

        for (int i = 6; i >= 1; i--)
        {
            address = (address << 8) | pairingFeatureReport[i];
        }

        return address != 0;
    }
}

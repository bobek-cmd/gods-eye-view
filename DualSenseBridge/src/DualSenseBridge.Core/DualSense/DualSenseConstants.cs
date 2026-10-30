namespace DualSenseBridge.Core.DualSense;

public static class DualSenseConstants
{
    public const int SonyVendorId = 0x054C;
    public const int DualSenseProductId = 0x0CE6;
    public const int DualSenseEdgeProductId = 0x0DF2;

    public const byte UsbInputReportId = 0x01;
    public const byte BluetoothSimpleInputReportId = 0x01;
    public const byte BluetoothInputReportId = 0x31;
    public const byte UsbOutputReportId = 0x02;
    public const byte BluetoothOutputReportId = 0x31;

    public const int UsbInputReportLength = 64;
    public const int BluetoothInputReportLength = 78;
    public const int UsbOutputReportLength = 63;
    public const int BluetoothOutputReportLength = 78;

    /// <summary>Calibration feature report. Reading it over Bluetooth switches the
    /// controller from the reduced 0x01 report to the full 0x31 report.</summary>
    public const byte CalibrationFeatureReportId = 0x05;
    /// <summary>Pairing info feature report; bytes 1..6 hold the controller MAC (little endian).</summary>
    public const byte PairingInfoFeatureReportId = 0x09;
    /// <summary>Firmware info feature report; bytes 44..45 hold the "update version".</summary>
    public const byte FirmwareInfoFeatureReportId = 0x20;

    public const byte InputCrcSeed = 0xA1;
    public const byte OutputCrcSeed = 0xA2;
    public const byte FeatureCrcSeed = 0xA3;

    /// <summary>Bluetooth HID service class GUID, present in device paths of Bluetooth HID devices.</summary>
    public const string BluetoothHidServiceGuid = "{00001124-0000-1000-8000-00805f9b34fb}";

    /// <summary>Firmware update version from which the "vibration v2" output flag must be used (2.21).</summary>
    public const ushort VibrationV2MinimumUpdateVersion = 0x0215;

    public static bool IsSupportedProduct(int vendorId, int productId) =>
        vendorId == SonyVendorId && (productId == DualSenseProductId || productId == DualSenseEdgeProductId);

    public static string GetProductName(int productId) =>
        productId == DualSenseEdgeProductId ? "DualSense Edge" : "DualSense";
}

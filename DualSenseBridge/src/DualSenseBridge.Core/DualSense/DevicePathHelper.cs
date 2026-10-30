namespace DualSenseBridge.Core.DualSense;

public static class DevicePathHelper
{
    /// <summary>True when the HID device path belongs to a Bluetooth Classic HID device.</summary>
    public static bool IsBluetooth(string devicePath) =>
        devicePath.Contains(DualSenseConstants.BluetoothHidServiceGuid, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Converts a device interface path such as
    /// <c>\\?\hid#{00001124-...}_vid&amp;0002054c_pid&amp;0ce6#8&amp;1a2b3c&amp;0&amp;0000#{4d1e55b2-...}</c>
    /// into a device instance ID (<c>HID\{00001124-...}_VID&amp;0002054C_PID&amp;0CE6\8&amp;1A2B3C&amp;0&amp;0000</c>).
    /// Used as a fallback when the Configuration Manager lookup is unavailable.
    /// </summary>
    public static string ToInstanceId(string devicePath)
    {
        string path = devicePath;
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            path = path[4..];
        }

        int lastHash = path.LastIndexOf('#');
        if (lastHash > 0 && path.Length > lastHash + 1 && path[lastHash + 1] == '{')
        {
            path = path[..lastHash];
        }

        return path.Replace('#', '\\').ToUpperInvariant();
    }
}

using System.Runtime.InteropServices;
using DualSenseBridge.Interop;

namespace DualSenseBridge.Services;

public static class BluetoothControl
{
    /// <summary>
    /// Drops the Bluetooth link to a device, which makes the DualSense power itself off.
    /// Tries every local radio until one accepts the request.
    /// </summary>
    public static bool Disconnect(ulong address)
    {
        if (address == 0)
        {
            return false;
        }

        var parameters = new NativeMethods.BLUETOOTH_FIND_RADIO_PARAMS
        {
            dwSize = (uint)Marshal.SizeOf<NativeMethods.BLUETOOTH_FIND_RADIO_PARAMS>(),
        };

        IntPtr find = NativeMethods.BluetoothFindFirstRadio(ref parameters, out IntPtr radio);
        if (find == IntPtr.Zero)
        {
            Log.Warn($"No Bluetooth radio found (error {Marshal.GetLastWin32Error()})");
            return false;
        }

        bool success = false;
        try
        {
            while (radio != IntPtr.Zero && !success)
            {
                ulong addr = address;
                success = NativeMethods.DeviceIoControl(radio, NativeMethods.IOCTL_BTH_DISCONNECT_DEVICE,
                    ref addr, sizeof(ulong), IntPtr.Zero, 0, out _, IntPtr.Zero);
                if (!success)
                {
                    Log.Warn($"Bluetooth disconnect failed on a radio (error {Marshal.GetLastWin32Error()})");
                }

                NativeMethods.CloseHandle(radio);
                radio = IntPtr.Zero;

                if (!success && !NativeMethods.BluetoothFindNextRadio(find, out radio))
                {
                    radio = IntPtr.Zero;
                }
            }
        }
        finally
        {
            if (radio != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(radio);
            }

            NativeMethods.BluetoothFindRadioClose(find);
        }

        return success;
    }

    public static string FormatAddress(ulong address) =>
        string.Join(":", Enumerable.Range(0, 6).Select(i => ((address >> (8 * (5 - i))) & 0xFF).ToString("X2")));
}

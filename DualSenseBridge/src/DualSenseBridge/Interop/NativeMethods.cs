using System.Runtime.InteropServices;

namespace DualSenseBridge.Interop;

internal static partial class NativeMethods
{
    public const uint IOCTL_BTH_DISCONNECT_DEVICE = 0x41000C;

    [StructLayout(LayoutKind.Sequential)]
    public struct BLUETOOTH_FIND_RADIO_PARAMS
    {
        public uint dwSize;
    }

    [LibraryImport("bthprops.cpl", SetLastError = true)]
    public static partial IntPtr BluetoothFindFirstRadio(ref BLUETOOTH_FIND_RADIO_PARAMS pbtfrp, out IntPtr phRadio);

    [LibraryImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool BluetoothFindNextRadio(IntPtr hFind, out IntPtr phRadio);

    [LibraryImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool BluetoothFindRadioClose(IntPtr hFind);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        ref ulong lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(IntPtr hObject);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(IntPtr hIcon);

    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWCP_ROUND = 2;

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}

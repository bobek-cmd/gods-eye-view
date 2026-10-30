using System.Diagnostics;
using System.IO;

namespace DualSenseBridge.Services;

public static class DriverInfo
{
    public const string ViGEmBusDownloadUrl = "https://github.com/nefarius/ViGEmBus/releases/latest";
    public const string HidHideDownloadUrl = "https://github.com/nefarius/HidHide/releases/latest";

    public static string? GetViGEmBusVersion()
    {
        try
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "ViGEmBus.sys");
            if (!File.Exists(path))
            {
                return null;
            }

            return FileVersionInfo.GetVersionInfo(path).FileVersion;
        }
        catch
        {
            return null;
        }
    }

    public static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not open {url}", ex);
        }
    }
}

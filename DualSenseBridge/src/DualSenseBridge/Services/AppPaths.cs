using System.IO;

namespace DualSenseBridge.Services;

public static class AppPaths
{
    public const string AppName = "DualSenseBridge";

    public static string RoamingDirectory { get; } = Ensure(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName));

    public static string LocalDirectory { get; } = Ensure(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName));

    public static string LogDirectory { get; } = Ensure(Path.Combine(LocalDirectory, "logs"));

    public static string SettingsFile => Path.Combine(RoamingDirectory, "settings.json");

    public static string StateFile => Path.Combine(LocalDirectory, "state.json");

    public static string ExecutablePath => Environment.ProcessPath
        ?? Path.Combine(AppContext.BaseDirectory, AppName + ".exe");

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}

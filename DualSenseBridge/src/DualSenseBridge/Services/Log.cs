using System.IO;
using System.Text;

namespace DualSenseBridge.Services;

/// <summary>Minimal thread-safe daily file logger (%LocalAppData%\DualSenseBridge\logs).</summary>
public static class Log
{
    private static readonly object Gate = new();
    private const int RetentionDays = 7;

    static Log()
    {
        try
        {
            foreach (var file in new DirectoryInfo(AppPaths.LogDirectory).GetFiles("app-*.log"))
            {
                if (file.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-RetentionDays))
                {
                    file.Delete();
                }
            }
        }
        catch
        {
            // Log housekeeping must never break the app.
        }
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message, Exception? ex = null) => Write("WARN", message, ex);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        var line = new StringBuilder()
            .Append(DateTime.Now.ToString("HH:mm:ss.fff"))
            .Append(" [").Append(level).Append("] ")
            .Append(message);
        if (ex != null)
        {
            line.Append(" | ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
        }

        try
        {
            lock (Gate)
            {
                string path = Path.Combine(AppPaths.LogDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(path, line.AppendLine().ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Ignore logging failures (disk full, file locked, ...).
        }

        System.Diagnostics.Debug.WriteLine(line.ToString());
    }
}

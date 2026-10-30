using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DualSenseBridge.Core.Configuration;

namespace DualSenseBridge.Services;

/// <summary>Bookkeeping that must survive crashes so HidHide changes can always be undone.</summary>
public sealed class RuntimeState
{
    /// <summary>Instance IDs of every DualSense this app has seen; hidden as soon as the app starts.</summary>
    public List<string> KnownInstanceIds { get; set; } = new();

    /// <summary>Instance IDs this app has currently added to the HidHide block list.</summary>
    public List<string> HiddenByApp { get; set; } = new();

    /// <summary>True when this app enabled HidHide's global cloaking (so it is disabled again on exit).</summary>
    public bool CloakingEnabledByApp { get; set; }

    /// <summary>True when this app added its own executable to the HidHide allow list.</summary>
    public bool ApplicationRegistered { get; set; }

    public bool FirstRunCompleted { get; set; }
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly object Gate = new();

    public static AppSettings LoadSettings()
    {
        var settings = Load<AppSettings>(AppPaths.SettingsFile) ?? new AppSettings();
        settings.Normalize();
        return settings;
    }

    public static void SaveSettings(AppSettings settings) => Save(AppPaths.SettingsFile, settings);

    public static RuntimeState LoadState() => Load<RuntimeState>(AppPaths.StateFile) ?? new RuntimeState();

    public static void SaveState(RuntimeState state) => Save(AppPaths.StateFile, state);

    private static T? Load<T>(string path) where T : class
    {
        try
        {
            lock (Gate)
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read {Path.GetFileName(path)}, using defaults", ex);
            return null;
        }
    }

    private static void Save<T>(string path, T value)
    {
        try
        {
            lock (Gate)
            {
                // Write to a temp file first so a crash never leaves a truncated file behind.
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
                File.Move(temp, path, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Could not write {Path.GetFileName(path)}", ex);
        }
    }
}

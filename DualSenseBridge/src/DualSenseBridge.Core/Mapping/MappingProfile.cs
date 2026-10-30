using DualSenseBridge.Core.Configuration;

namespace DualSenseBridge.Core.Mapping;

/// <summary>Immutable snapshot of the settings the input path needs; swapped atomically when settings change.</summary>
public sealed record MappingProfile(
    double LeftDeadzone,
    double RightDeadzone,
    double TriggerDeadzone,
    TouchpadClickAction TouchpadClick)
{
    public static MappingProfile Default { get; } = FromSettings(new AppSettings());

    public static MappingProfile FromSettings(AppSettings settings) => new(
        settings.LeftStickDeadzone / 100.0,
        settings.RightStickDeadzone / 100.0,
        settings.TriggerDeadzone / 100.0,
        settings.TouchpadClick);
}

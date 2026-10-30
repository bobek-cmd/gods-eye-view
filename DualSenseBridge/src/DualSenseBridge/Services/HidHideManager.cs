using DualSenseBridge.Core.DualSense;
using Nefarius.Drivers.HidHide;
using Nefarius.Utilities.DeviceManagement.PnP;

namespace DualSenseBridge.Services;

public enum HidHideStatus
{
    NotInstalled,
    Disabled,
    Ready,
    Error,
}

/// <summary>
/// Hides the physical DualSense from every process except this app (and whatever the user allowed
/// in HidHide), so games only see the virtual Xbox 360 controller and never get double input.
/// Every change is tracked in <see cref="RuntimeState"/> and reverted on exit or by <c>--cleanup</c>.
/// </summary>
public sealed class HidHideManager
{
    private readonly object _gate = new();
    private readonly RuntimeState _state;
    private readonly HidHideControlService _service = new();
    private bool _enabled;

    public HidHideManager(RuntimeState state)
    {
        _state = state;
    }

    public HidHideStatus Status { get; private set; } = HidHideStatus.NotInstalled;

    public string StatusMessage { get; private set; } = "";

    public string? DriverVersion { get; private set; }

    public event Action? StatusChanged;

    /// <summary>Resolves the device instance ID HidHide expects from a HID device interface path.</summary>
    public static string ResolveInstanceId(string devicePath)
    {
        try
        {
            return PnPDevice.GetInstanceIdFromInterfaceId(devicePath) ?? DevicePathHelper.ToInstanceId(devicePath);
        }
        catch (Exception ex)
        {
            Log.Warn("Configuration Manager lookup failed, deriving instance ID from path", ex);
            return DevicePathHelper.ToInstanceId(devicePath);
        }
    }

    /// <summary>Detects the driver and, when enabled, immediately hides every controller seen before.</summary>
    public void Initialize(bool enabled)
    {
        lock (_gate)
        {
            _enabled = enabled;
            Refresh();
        }
    }

    public void SetEnabled(bool enabled)
    {
        lock (_gate)
        {
            if (_enabled == enabled)
            {
                return;
            }

            _enabled = enabled;
            if (!enabled)
            {
                RestoreCore();
            }

            Refresh();
        }
    }

    /// <summary>Re-checks the driver (e.g. after the user installed it) and re-applies hiding.</summary>
    public void Refresh()
    {
        lock (_gate)
        {
            try
            {
                if (!_service.IsInstalled)
                {
                    SetStatus(HidHideStatus.NotInstalled, "HidHide nije instaliran – igre mogu vidjeti i originalni DualSense.");
                    return;
                }

                DriverVersion = TryGetVersion();

                if (!_enabled)
                {
                    SetStatus(HidHideStatus.Disabled, "Skrivanje originalnog kontrolera je isključeno u postavkama.");
                    return;
                }

                EnsureApplicationAllowed();
                foreach (string id in _state.KnownInstanceIds.ToList())
                {
                    HideCore(id);
                }

                SetStatus(HidHideStatus.Ready, "Originalni DualSense je skriven od igara – vide samo Xbox 360 kontroler.");
            }
            catch (Exception ex)
            {
                Log.Error("HidHide configuration failed", ex);
                SetStatus(HidHideStatus.Error,
                    "HidHide trenutno nije dostupan (zatvorite HidHide Configuration Client ako je otvoren).");
            }
        }
    }

    /// <summary>Registers a controller and hides it if hiding is enabled. Returns true when the device is hidden.</summary>
    public bool RegisterDevice(string instanceId)
    {
        lock (_gate)
        {
            if (!_state.KnownInstanceIds.Contains(instanceId, StringComparer.OrdinalIgnoreCase))
            {
                _state.KnownInstanceIds.Add(instanceId);
                SettingsStore.SaveState(_state);
            }

            if (!_enabled || Status is HidHideStatus.NotInstalled)
            {
                return false;
            }

            try
            {
                EnsureApplicationAllowed();
                HideCore(instanceId);
                if (Status != HidHideStatus.Ready)
                {
                    SetStatus(HidHideStatus.Ready, "Originalni DualSense je skriven od igara – vide samo Xbox 360 kontroler.");
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Could not hide {instanceId}", ex);
                SetStatus(HidHideStatus.Error,
                    "Kontroler nije moguće sakriti (zatvorite HidHide Configuration Client ako je otvoren).");
                return false;
            }
        }
    }

    public bool IsHiddenByApp(string instanceId)
    {
        lock (_gate)
        {
            return _state.HiddenByApp.Contains(instanceId, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Makes every controller visible again and restores HidHide's previous global state.</summary>
    public void RestoreAll()
    {
        lock (_gate)
        {
            RestoreCore();
        }
    }

    /// <summary>Adds this executable to the HidHide allow list (run by the installer, elevated).</summary>
    public static void RegisterApplication()
    {
        var state = SettingsStore.LoadState();
        var manager = new HidHideManager(state);
        lock (manager._gate)
        {
            try
            {
                if (manager._service.IsInstalled)
                {
                    manager.EnsureApplicationAllowed();
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not register application with HidHide", ex);
            }
        }
    }

    /// <summary>Removes every trace of this app from the HidHide configuration (used by the uninstaller).</summary>
    public static void Cleanup()
    {
        var state = SettingsStore.LoadState();
        var manager = new HidHideManager(state);
        lock (manager._gate)
        {
            manager.RestoreCore();
            try
            {
                if (state.ApplicationRegistered && manager._service.IsInstalled)
                {
                    manager._service.RemoveApplicationPath(AppPaths.ExecutablePath);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not unregister application from HidHide", ex);
            }

            state.ApplicationRegistered = false;
            state.KnownInstanceIds.Clear();
            SettingsStore.SaveState(state);
        }
    }

    private void RestoreCore()
    {
        if (_state.HiddenByApp.Count == 0 && !_state.CloakingEnabledByApp)
        {
            return;
        }

        try
        {
            if (!_service.IsInstalled)
            {
                _state.HiddenByApp.Clear();
                _state.CloakingEnabledByApp = false;
                SettingsStore.SaveState(_state);
                return;
            }

            var blocked = _service.BlockedInstanceIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string id in _state.HiddenByApp.ToList())
            {
                if (blocked.Contains(id))
                {
                    _service.RemoveBlockedInstanceId(id);
                }

                _state.HiddenByApp.Remove(id);
            }

            if (_state.CloakingEnabledByApp)
            {
                _service.IsActive = false;
                _state.CloakingEnabledByApp = false;
            }

            Log.Info("HidHide configuration restored");
        }
        catch (Exception ex)
        {
            Log.Error("Could not restore HidHide configuration", ex);
        }
        finally
        {
            SettingsStore.SaveState(_state);
        }
    }

    private void HideCore(string instanceId)
    {
        var blocked = _service.BlockedInstanceIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!blocked.Contains(instanceId))
        {
            _service.AddBlockedInstanceId(instanceId);
            Log.Info($"HidHide: hiding {instanceId}");
        }

        if (!_state.HiddenByApp.Contains(instanceId, StringComparer.OrdinalIgnoreCase))
        {
            _state.HiddenByApp.Add(instanceId);
        }

        if (!_service.IsActive)
        {
            _service.IsActive = true;
            _state.CloakingEnabledByApp = true;
            Log.Info("HidHide: global cloaking enabled");
        }

        SettingsStore.SaveState(_state);
    }

    private void EnsureApplicationAllowed()
    {
        string exe = AppPaths.ExecutablePath;

        if (_service.IsAppListInverted)
        {
            // Inverted list = "allow all, block listed": we are allowed unless explicitly listed.
            if (_service.ApplicationPaths.Contains(exe, StringComparer.OrdinalIgnoreCase))
            {
                _service.RemoveApplicationPath(exe);
            }

            return;
        }

        if (!_service.ApplicationPaths.Contains(exe, StringComparer.OrdinalIgnoreCase))
        {
            _service.AddApplicationPath(exe, false);
            _state.ApplicationRegistered = true;
            SettingsStore.SaveState(_state);
            Log.Info($"HidHide: allowed {exe}");
        }
    }

    private string? TryGetVersion()
    {
        try
        {
            return _service.LocalDriverVersion.ToString();
        }
        catch
        {
            return null;
        }
    }

    private void SetStatus(HidHideStatus status, string message)
    {
        bool changed = Status != status || StatusMessage != message;
        Status = status;
        StatusMessage = message;
        if (changed)
        {
            StatusChanged?.Invoke();
        }
    }
}

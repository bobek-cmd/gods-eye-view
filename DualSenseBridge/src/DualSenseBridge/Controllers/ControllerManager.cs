using DualSenseBridge.Core.Configuration;
using DualSenseBridge.Core.DualSense;
using DualSenseBridge.Services;
using HidSharp;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;

namespace DualSenseBridge.Controllers;

public enum ViGEmStatus
{
    Unknown,
    Ready,
    NotInstalled,
    Error,
}

/// <summary>
/// Watches for DualSense controllers (Bluetooth and USB) and keeps one
/// <see cref="ControllerSession"/> per connected controller.
/// </summary>
public sealed class ControllerManager : IDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private readonly Dictionary<string, ControllerSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HidHideManager _hidHide;
    private readonly Timer _scanTimer;
    private AppSettings _settings;
    private ViGEmClient? _client;
    private bool _disposed;
    private int _scanQueued;

    public ControllerManager(HidHideManager hidHide, AppSettings settings)
    {
        _hidHide = hidHide;
        _settings = settings;
        _scanTimer = new Timer(_ => Scan(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public ViGEmStatus ViGEmStatus { get; private set; } = ViGEmStatus.Unknown;

    public string ViGEmMessage { get; private set; } = "";

    /// <summary>Raised from a background thread whenever the set of sessions or a session state changes.</summary>
    public event Action? Changed;

    public event Action<ControllerSnapshot, int>? LowBattery;

    public event Action<ControllerSnapshot>? ControllerConnected;

    public event Action<ControllerSnapshot>? ControllerDisconnected;

    public void Start()
    {
        DeviceList.Local.Changed += OnDeviceListChanged;
        _scanTimer.Change(TimeSpan.Zero, ScanInterval);
    }

    public IReadOnlyList<ControllerSnapshot> GetSnapshots()
    {
        lock (_gate)
        {
            return _sessions.Values.Select(s => s.Snapshot).ToList();
        }
    }

    public ControllerSession? Find(string id)
    {
        lock (_gate)
        {
            return _sessions.TryGetValue(id, out var session) ? session : null;
        }
    }

    public void UpdateSettings(AppSettings settings)
    {
        lock (_gate)
        {
            _settings = settings;
            foreach (var session in _sessions.Values)
            {
                session.UpdateSettings(settings);
            }
        }
    }

    /// <summary>Re-checks the drivers immediately (e.g. after the user installed ViGEmBus).</summary>
    public void Rescan() => ThreadPool.QueueUserWorkItem(_ => Scan());

    private void OnDeviceListChanged(object? sender, DeviceListChangedEventArgs e)
    {
        // Device notifications come in bursts; coalesce them into one scan.
        if (Interlocked.Exchange(ref _scanQueued, 1) == 0)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(250);
                Interlocked.Exchange(ref _scanQueued, 0);
                Scan();
            });
        }
    }

    private void Scan()
    {
        if (!Monitor.TryEnter(_gate, 0))
        {
            return; // A scan is already running.
        }

        bool changed = false;
        var finished = new List<ControllerSession>();
        try
        {
            if (_disposed)
            {
                return;
            }

            var devices = DeviceList.Local.GetHidDevices(DualSenseConstants.SonyVendorId)
                .Where(d => DualSenseConstants.IsSupportedProduct(d.VendorID, d.ProductID))
                .ToList();
            var present = devices.Select(d => d.DevicePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Drop sessions that ended. A controller that is still present is retried after a
            // short delay; a failed session stays visible (with its error) until then.
            foreach (var (id, session) in _sessions.ToList())
            {
                if (session.IsStopped && (!present.Contains(id) || DateTime.UtcNow - session.StoppedAtUtc >= RetryDelay))
                {
                    _sessions.Remove(id);
                    session.StateChanged -= OnSessionStateChanged;
                    finished.Add(session);
                    changed = true;
                }
            }

            if (!EnsureClient())
            {
                changed = true;
                return;
            }

            foreach (var device in devices)
            {
                if (_sessions.ContainsKey(device.DevicePath))
                {
                    continue;
                }

                var session = new ControllerSession(device, _client!, _hidHide, _settings);
                session.StateChanged += OnSessionStateChanged;
                session.LowBattery += (s, percent) => LowBattery?.Invoke(s.Snapshot, percent);
                _sessions[device.DevicePath] = session;
                session.Start();
                changed = true;
                Log.Info($"Found {session.Name} at {device.DevicePath}");
            }
        }
        catch (Exception ex)
        {
            Log.Error("Controller scan failed", ex);
        }
        finally
        {
            Monitor.Exit(_gate);
            foreach (var session in finished)
            {
                session.Dispose();
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }
    }

    private bool EnsureClient()
    {
        if (_client != null)
        {
            return true;
        }

        try
        {
            _client = new ViGEmClient();
            SetViGEmStatus(ViGEmStatus.Ready, "ViGEmBus driver je spreman.");
            Log.Info("Connected to ViGEmBus");
            return true;
        }
        catch (VigemBusNotFoundException)
        {
            SetViGEmStatus(ViGEmStatus.NotInstalled, "ViGEmBus driver nije instaliran – Xbox 360 kontroler se ne može emulirati.");
        }
        catch (VigemBusVersionMismatchException)
        {
            SetViGEmStatus(ViGEmStatus.Error, "Instalirana verzija ViGEmBus drivera nije podržana – instalirajte najnoviju.");
        }
        catch (Exception ex)
        {
            Log.Error("ViGEmBus initialisation failed", ex);
            SetViGEmStatus(ViGEmStatus.Error, $"ViGEmBus nije dostupan: {ex.Message}");
        }

        return false;
    }

    private void SetViGEmStatus(ViGEmStatus status, string message)
    {
        if (ViGEmStatus != status)
        {
            Log.Info($"ViGEm status: {status}");
        }

        ViGEmStatus = status;
        ViGEmMessage = message;
    }

    private void OnSessionStateChanged(ControllerSession session, SessionState state)
    {
        var snapshot = session.Snapshot;
        if (state == SessionState.Active)
        {
            ControllerConnected?.Invoke(snapshot);
        }
        else if (state is SessionState.Stopped or SessionState.Error)
        {
            if (session.WasActive)
            {
                ControllerDisconnected?.Invoke(snapshot);
            }

            // Pick up a reconnect quickly instead of waiting for the next timer tick.
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(500);
                Scan();
            });
        }

        Changed?.Invoke();
    }

    public void Dispose()
    {
        DeviceList.Local.Changed -= OnDeviceListChanged;
        _scanTimer.Dispose();

        List<ControllerSession> sessions;
        lock (_gate)
        {
            _disposed = true;
            sessions = _sessions.Values.ToList();
            _sessions.Clear();
        }

        foreach (var session in sessions)
        {
            session.StateChanged -= OnSessionStateChanged;
            session.Dispose();
        }

        _client?.Dispose();
        _client = null;
    }
}

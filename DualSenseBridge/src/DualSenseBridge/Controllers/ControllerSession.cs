using System.Diagnostics;
using System.IO;
using DualSenseBridge.Core.Configuration;
using DualSenseBridge.Core.DualSense;
using DualSenseBridge.Core.Mapping;
using DualSenseBridge.Services;
using HidSharp;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace DualSenseBridge.Controllers;

/// <summary>
/// One physical DualSense bridged to one virtual Xbox 360 controller.
/// A dedicated high priority thread reads HID reports and forwards them to ViGEm;
/// rumble and LED changes are written back from the same thread.
/// </summary>
public sealed class ControllerSession : IDisposable
{
    private const int ReadTimeoutMs = 1000;
    private const int MaxConsecutiveTimeouts = 4;
    private const int MinOutputIntervalMs = 8;

    private readonly HidDevice _device;
    private readonly ViGEmClient _client;
    private readonly HidHideManager _hidHide;
    private readonly object _snapshotGate = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread _thread;

    private volatile AppSettings _settings;
    private volatile MappingProfile _profile;
    private volatile bool _outputDirty = true;
    private volatile int _testRumbleUntil;

    private ControllerSnapshot _snapshot;
    private string _instanceId = "";
    private HidStream? _stream;
    private IXbox360Controller? _xbox;
    private ConnectionType _connection;
    private bool _useVibrationV2 = true;
    private byte _outputSequence;
    private int _outputLength;

    // Written by the ViGEm feedback callback, read by the I/O thread.
    private volatile byte _gameStrongMotor;
    private volatile byte _gameWeakMotor;
    private volatile int _playerNumber;

    private bool _testRumbleActive;
    private int _batteryPercent = -1;
    private ChargingState _charging = ChargingState.Unknown;
    private bool _lowBatteryNotified;

    public ControllerSession(HidDevice device, ViGEmClient client, HidHideManager hidHide, AppSettings settings)
    {
        _device = device;
        _client = client;
        _hidHide = hidHide;
        _settings = settings;
        _profile = MappingProfile.FromSettings(settings);
        Id = device.DevicePath;
        Name = DualSenseConstants.GetProductName(device.ProductID);
        _connection = DevicePathHelper.IsBluetooth(device.DevicePath) ? ConnectionType.Bluetooth : ConnectionType.Usb;
        _snapshot = new ControllerSnapshot(Id, Name, _connection, SessionState.Connecting, "Povezivanje…",
            -1, ChargingState.Unknown, 0, 0, DualSenseInputState.Neutral, false, 0);

        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = $"DualSense I/O {Name}",
            Priority = ThreadPriority.AboveNormal,
        };
    }

    public string Id { get; }

    public string Name { get; }

    public DateTime StoppedAtUtc { get; private set; }

    /// <summary>True once the session has delivered input (used to avoid "disconnected" noise for failed attempts).</summary>
    public bool WasActive { get; private set; }

    public bool IsStopped => Snapshot.State is SessionState.Stopped or SessionState.Error;

    public ControllerSnapshot Snapshot
    {
        get { lock (_snapshotGate) { return _snapshot; } }
    }

    /// <summary>Raised (on the I/O thread) when the battery reaches the warning threshold.</summary>
    public event Action<ControllerSession, int>? LowBattery;

    /// <summary>Raised (on the I/O thread) when the session reaches a new state.</summary>
    public event Action<ControllerSession, SessionState>? StateChanged;

    public void Start() => _thread.Start();

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        _profile = MappingProfile.FromSettings(settings);
        _outputDirty = true;
    }

    /// <summary>Short rumble so the user can identify the controller.</summary>
    public void TestRumble()
    {
        _testRumbleUntil = Environment.TickCount + 400;
        _outputDirty = true;
    }

    /// <summary>Turns the controller off by dropping its Bluetooth link.</summary>
    public bool PowerOff()
    {
        ulong address = Snapshot.BluetoothAddress;
        if (_connection != ConnectionType.Bluetooth || address == 0)
        {
            return false;
        }

        return BluetoothControl.Disconnect(address);
    }

    public void Stop()
    {
        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }

        // Unblocks a pending Read() immediately.
        try { _stream?.Dispose(); } catch { /* already gone */ }

        if (_thread.IsAlive && Thread.CurrentThread != _thread)
        {
            _thread.Join(2000);
        }
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
    }

    private void Run()
    {
        string? failure = null;
        try
        {
            RunCore(_cts.Token);
        }
        catch (Exception ex) when (_cts.IsCancellationRequested)
        {
            Log.Info($"{Name}: session closed ({ex.GetType().Name})");
        }
        catch (IOException ex)
        {
            Log.Info($"{Name}: device disconnected ({ex.Message})");
        }
        catch (Exception ex)
        {
            Log.Error($"{Name}: session failed", ex);
            failure = ex.Message;
        }
        finally
        {
            Teardown();
            StoppedAtUtc = DateTime.UtcNow;
            Publish(failure == null ? SessionState.Stopped : SessionState.Error, failure ?? "Odspojen",
                DualSenseInputState.Neutral, 0);
        }
    }

    private void RunCore(CancellationToken token)
    {
        // 1. Hide the physical controller from games before anything else can grab it.
        _instanceId = HidHideManager.ResolveInstanceId(_device.DevicePath);
        bool hidden = _hidHide.RegisterDevice(_instanceId);
        Log.Info($"{Name}: {_connection} device {_instanceId} (hidden: {hidden})");

        // 2. Open the HID device.
        if (!_device.TryOpen(out HidStream stream))
        {
            throw new InvalidOperationException(hidden
                ? "Kontroler nije moguće otvoriti. Ako je HidHide upravo instaliran, ponovno pokrenite aplikaciju."
                : "Kontroler je zauzet ili nedostupan.");
        }

        _stream = stream;
        stream.ReadTimeout = ReadTimeoutMs;
        stream.WriteTimeout = 1000;

        int inputLength = _device.GetMaxInputReportLength();
        _outputLength = _device.GetMaxOutputReportLength();
        if (_connection == ConnectionType.Usb && inputLength >= DualSenseConstants.BluetoothInputReportLength)
        {
            _connection = ConnectionType.Bluetooth;
        }

        // 3. Query controller information. Reading the calibration report switches a
        //    Bluetooth DualSense into the full 0x31 report mode (with battery).
        ulong address = 0;
        TryGetFeature(stream, DualSenseConstants.CalibrationFeatureReportId);
        byte[]? firmware = TryGetFeature(stream, DualSenseConstants.FirmwareInfoFeatureReportId);
        if (firmware != null && DualSenseOutputReport.TryGetUpdateVersion(firmware, out ushort updateVersion))
        {
            _useVibrationV2 = updateVersion >= DualSenseConstants.VibrationV2MinimumUpdateVersion;
            Log.Info($"{Name}: firmware update version 0x{updateVersion:X4} (vibration v2: {_useVibrationV2})");
        }

        byte[]? pairing = TryGetFeature(stream, DualSenseConstants.PairingInfoFeatureReportId);
        if (pairing != null && DualSenseOutputReport.TryGetMacAddress(pairing, out address))
        {
            Log.Info($"{Name}: address {BluetoothControl.FormatAddress(address)}");
        }

        lock (_snapshotGate)
        {
            _snapshot = _snapshot with { Connection = _connection, IsHidden = hidden, BluetoothAddress = address };
        }

        // 4. Main loop. The virtual controller is only plugged in once the first valid report
        //    arrives, so a paired-but-switched-off controller never flickers into games.
        var buffer = new byte[Math.Max(inputLength, DualSenseConstants.BluetoothInputReportLength)];
        var lastSent = new XInputState();
        bool firstReport = true;
        int timeouts = 0;
        int reportsInWindow = 0;
        int reportRate = 0;
        long rateWindowStart = NowMs();
        long lastFullModeRequest = 0;
        long lastOutputWrite = 0;
        long lastPublish = 0;

        while (!token.IsCancellationRequested)
        {
            int read;
            try
            {
                read = stream.Read(buffer, 0, buffer.Length);
            }
            catch (TimeoutException)
            {
                // A connected DualSense streams reports continuously; silence means it is gone.
                if (++timeouts >= MaxConsecutiveTimeouts)
                {
                    throw new IOException("Kontroler ne šalje podatke.");
                }

                continue;
            }

            timeouts = 0;
            if (read <= 0)
            {
                continue;
            }

            var result = DualSenseReportParser.Parse(buffer.AsSpan(0, read), _connection, out var input);
            if (result is ParseResult.Ignored or ParseResult.TooShort or ParseResult.CrcMismatch)
            {
                continue;
            }

            long now = NowMs();
            if (result == ParseResult.Simple && now - lastFullModeRequest > 1000)
            {
                // Still in reduced Bluetooth mode; ask again for the full report.
                lastFullModeRequest = now;
                TryGetFeature(stream, DualSenseConstants.CalibrationFeatureReportId);
            }

            if (_xbox == null)
            {
                ConnectVirtualController(stream);
            }

            var mapped = Xbox360Mapper.Map(input, _profile);
            if (firstReport || !mapped.Equals(lastSent))
            {
                Submit(mapped);
                lastSent = mapped;
                firstReport = false;
            }

            if (input.HasBattery)
            {
                UpdateBattery(input.BatteryPercent, input.Charging);
            }

            if (TestRumbleChanged())
            {
                _outputDirty = true;
            }

            // Rate-limited so games that spam rumble updates cannot flood the Bluetooth link.
            if (_outputDirty && now - lastOutputWrite >= MinOutputIntervalMs)
            {
                _outputDirty = false;
                WriteOutput(stream, releaseLightbar: false);
                lastOutputWrite = now;
            }

            reportsInWindow++;
            if (now - rateWindowStart >= 1000)
            {
                reportRate = (int)(reportsInWindow * 1000 / (now - rateWindowStart));
                reportsInWindow = 0;
                rateWindowStart = now;
            }

            // The UI polls the snapshot; refresh it at ~60 Hz rather than on every report.
            if (now - lastPublish >= 16)
            {
                lastPublish = now;
                Publish(SessionState.Active, null, input, reportRate);
            }
        }
    }

    private void ConnectVirtualController(HidStream stream)
    {
        var xbox = _client.CreateXbox360Controller();
        xbox.AutoSubmitReport = false;
        xbox.FeedbackReceived += OnFeedbackReceived;
        xbox.Connect();
        _xbox = xbox;
        Log.Info($"{Name}: virtual Xbox 360 controller connected");

        // Release the Bluetooth pairing light animation so the lightbar can be controlled.
        WriteOutput(stream, releaseLightbar: true);
        _outputDirty = true;

        Publish(SessionState.Active, null, DualSenseInputState.Neutral, 0);
    }

    private static long NowMs() => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;

    private bool TestRumbleChanged()
    {
        bool active = Environment.TickCount - _testRumbleUntil < 0;
        if (active != _testRumbleActive)
        {
            _testRumbleActive = active;
            return true;
        }

        return false;
    }

    private void Submit(in XInputState state)
    {
        var xbox = _xbox!;
        xbox.SetButtonsFull((ushort)state.Buttons);
        xbox.SetAxisValue(Xbox360Axis.LeftThumbX, state.LeftThumbX);
        xbox.SetAxisValue(Xbox360Axis.LeftThumbY, state.LeftThumbY);
        xbox.SetAxisValue(Xbox360Axis.RightThumbX, state.RightThumbX);
        xbox.SetAxisValue(Xbox360Axis.RightThumbY, state.RightThumbY);
        xbox.SetSliderValue(Xbox360Slider.LeftTrigger, state.LeftTrigger);
        xbox.SetSliderValue(Xbox360Slider.RightTrigger, state.RightTrigger);
        xbox.SubmitReport();
    }

    private void OnFeedbackReceived(object sender, Xbox360FeedbackReceivedEventArgs e)
    {
        int player = e.LedNumber + 1;
        if (_gameStrongMotor != e.LargeMotor || _gameWeakMotor != e.SmallMotor || _playerNumber != player)
        {
            _gameStrongMotor = e.LargeMotor;
            _gameWeakMotor = e.SmallMotor;
            _playerNumber = player;
            _outputDirty = true;
        }
    }

    private void WriteOutput(HidStream stream, bool releaseLightbar)
    {
        var settings = _settings;
        byte strong = 0, weak = 0;
        if (_testRumbleActive)
        {
            strong = 180;
            weak = 180;
        }
        else if (settings.RumbleEnabled)
        {
            strong = (byte)(_gameStrongMotor * settings.RumbleStrength / 100);
            weak = (byte)(_gameWeakMotor * settings.RumbleStrength / 100);
        }

        int player = _playerNumber;
        var color = LightbarPolicy.Resolve(settings, player, _batteryPercent);
        var state = new DualSenseOutputState
        {
            StrongMotor = strong,
            WeakMotor = weak,
            LightbarRed = color.R,
            LightbarGreen = color.G,
            LightbarBlue = color.B,
            PlayerLeds = DualSenseOutputReport.PlayerLedsFor(player),
        };

        // Like hidapi, pad to the device's longest output report: Windows requires it for
        // WriteFile, and the DualSense only evaluates the first 78 bytes (CRC at offset 74).
        int length = _connection == ConnectionType.Bluetooth
            ? Math.Max(_outputLength, DualSenseConstants.BluetoothOutputReportLength)
            : Math.Max(_outputLength, 48);
        byte[] report = DualSenseOutputReport.Build(_connection, state, _outputSequence, _useVibrationV2, releaseLightbar, length);
        _outputSequence = (byte)((_outputSequence + 1) & 0x0F);

        try
        {
            stream.Write(report);
        }
        catch (TimeoutException)
        {
            Log.Warn($"{Name}: output report timed out");
        }
    }

    private void UpdateBattery(int percent, ChargingState charging)
    {
        if (percent == _batteryPercent && charging == _charging)
        {
            return;
        }

        bool batteryLightbar = _settings.LightbarMode == LightbarMode.Battery && percent != _batteryPercent;
        _batteryPercent = percent;
        _charging = charging;
        if (batteryLightbar)
        {
            _outputDirty = true;
        }

        int threshold = _settings.LowBatteryWarning;
        if (charging is ChargingState.Charging or ChargingState.Full || percent > threshold + 5)
        {
            _lowBatteryNotified = false;
        }
        else if (threshold > 0 && percent <= threshold && !_lowBatteryNotified)
        {
            _lowBatteryNotified = true;
            LowBattery?.Invoke(this, percent);
        }
    }

    private byte[]? TryGetFeature(HidStream stream, byte reportId)
    {
        try
        {
            // Windows expects the buffer to be as long as the longest feature report.
            int length = _device.GetMaxFeatureReportLength();
            var buffer = new byte[length];
            buffer[0] = reportId;
            stream.GetFeature(buffer);
            return buffer;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            Log.Warn($"{Name}: feature report 0x{reportId:X2} failed", ex);
            return null;
        }
    }

    private void Teardown()
    {
        var xbox = _xbox;
        _xbox = null;
        if (xbox != null)
        {
            try
            {
                xbox.FeedbackReceived -= OnFeedbackReceived;
                // Neutral report first so no button stays "held" in a running game.
                xbox.ResetReport();
                xbox.SubmitReport();
                xbox.Disconnect();
                Log.Info($"{Name}: virtual Xbox 360 controller removed");
            }
            catch (Exception ex)
            {
                Log.Warn($"{Name}: error while removing virtual controller", ex);
            }
        }

        var stream = _stream;
        _stream = null;
        if (stream != null)
        {
            try
            {
                // Stop any rumble that is still running.
                _gameStrongMotor = 0;
                _gameWeakMotor = 0;
                _testRumbleActive = false;
                WriteOutput(stream, releaseLightbar: false);
            }
            catch
            {
                // Device already gone.
            }

            try { stream.Dispose(); } catch { /* ignore */ }
        }
    }

    private void Publish(SessionState state, string? message, DualSenseInputState input, int reportRate)
    {
        if (state == SessionState.Active)
        {
            WasActive = true;
        }

        bool hidden = _instanceId.Length > 0 && _hidHide.IsHiddenByApp(_instanceId);
        SessionState previous;
        lock (_snapshotGate)
        {
            previous = _snapshot.State;
            _snapshot = _snapshot with
            {
                State = state,
                Message = message,
                Input = input,
                ReportRate = reportRate,
                BatteryPercent = _batteryPercent,
                Charging = _charging,
                PlayerNumber = _playerNumber,
                Connection = _connection,
                IsHidden = hidden,
            };
        }

        if (previous != state)
        {
            StateChanged?.Invoke(this, state);
        }
    }
}

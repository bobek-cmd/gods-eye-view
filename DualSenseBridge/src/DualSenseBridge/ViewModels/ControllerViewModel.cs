using DualSenseBridge.Controllers;
using DualSenseBridge.Core.DualSense;

namespace DualSenseBridge.ViewModels;

public sealed class ControllerViewModel : ObservableObject
{
    /// <summary>Radius in pixels the stick dot may travel inside the preview circle.</summary>
    private const double StickTravel = 20;

    private string _name = "";
    private string _connectionText = "";
    private bool _isBluetooth;
    private string _playerText = "";
    private string _statusText = "";
    private bool _isError;
    private bool _isActive;
    private int _batteryPercent = -1;
    private string _batteryText = "—";
    private string _chargingText = "";
    private bool _isCharging;
    private string _batteryLevel = "Unknown";
    private bool _isHidden;
    private string? _reportRateText;
    private bool _canPowerOff;
    private double _leftStickX;
    private double _leftStickY;
    private double _rightStickX;
    private double _rightStickY;
    private double _l2;
    private double _r2;
    private string _pressedButtons = "";

    public ControllerViewModel(string id)
    {
        Id = id;
    }

    public string Id { get; }

    public string Name { get => _name; private set => SetProperty(ref _name, value); }
    public string ConnectionText { get => _connectionText; private set => SetProperty(ref _connectionText, value); }
    public bool IsBluetooth { get => _isBluetooth; private set => SetProperty(ref _isBluetooth, value); }
    public string PlayerText { get => _playerText; private set => SetProperty(ref _playerText, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool IsError { get => _isError; private set => SetProperty(ref _isError, value); }
    public bool IsActive { get => _isActive; private set => SetProperty(ref _isActive, value); }
    public int BatteryPercent { get => _batteryPercent; private set => SetProperty(ref _batteryPercent, value); }
    public string BatteryText { get => _batteryText; private set => SetProperty(ref _batteryText, value); }
    public string ChargingText { get => _chargingText; private set => SetProperty(ref _chargingText, value); }
    public bool IsCharging { get => _isCharging; private set => SetProperty(ref _isCharging, value); }
    /// <summary>"Low", "Medium", "High", "Charging" or "Unknown" – drives the battery colour.</summary>
    public string BatteryLevel { get => _batteryLevel; private set => SetProperty(ref _batteryLevel, value); }
    public bool IsHidden { get => _isHidden; private set => SetProperty(ref _isHidden, value); }
    public string? ReportRateText { get => _reportRateText; private set => SetProperty(ref _reportRateText, value); }
    public bool CanPowerOff { get => _canPowerOff; private set => SetProperty(ref _canPowerOff, value); }
    public double LeftStickX { get => _leftStickX; private set => SetProperty(ref _leftStickX, value); }
    public double LeftStickY { get => _leftStickY; private set => SetProperty(ref _leftStickY, value); }
    public double RightStickX { get => _rightStickX; private set => SetProperty(ref _rightStickX, value); }
    public double RightStickY { get => _rightStickY; private set => SetProperty(ref _rightStickY, value); }
    /// <summary>L2 pressure 0..1.</summary>
    public double L2 { get => _l2; private set => SetProperty(ref _l2, value); }
    public double R2 { get => _r2; private set => SetProperty(ref _r2, value); }
    public string PressedButtons { get => _pressedButtons; private set => SetProperty(ref _pressedButtons, value); }

    public void Update(ControllerSnapshot s)
    {
        Name = s.Name;
        IsBluetooth = s.Connection == ConnectionType.Bluetooth;
        ConnectionText = IsBluetooth ? "Bluetooth" : "USB";
        PlayerText = s.PlayerNumber > 0 ? $"Xbox 360 · igrač {s.PlayerNumber}" : "Xbox 360";
        IsActive = s.State == SessionState.Active;
        IsError = s.State == SessionState.Error;
        StatusText = s.State switch
        {
            SessionState.Active => "Aktivan",
            SessionState.Connecting => "Povezivanje…",
            SessionState.Error => s.Message ?? "Greška",
            _ => s.Message ?? "Odspojen",
        };
        IsHidden = s.IsHidden;
        CanPowerOff = IsActive && IsBluetooth && s.BluetoothAddress != 0;
        ReportRateText = IsActive && s.ReportRate > 0 ? $"{s.ReportRate} Hz" : null;

        BatteryPercent = s.BatteryPercent;
        BatteryText = s.BatteryPercent >= 0 ? $"{s.BatteryPercent} %" : "—";
        IsCharging = s.Charging == ChargingState.Charging;
        ChargingText = s.Charging switch
        {
            ChargingState.Charging => "Puni se",
            ChargingState.Full => "Napunjen",
            ChargingState.Discharging => "Na bateriji",
            ChargingState.Error => "Greška punjenja",
            _ => "Čeka podatke",
        };
        BatteryLevel = s.BatteryPercent < 0 ? "Unknown"
            : IsCharging ? "Charging"
            : s.BatteryPercent <= 20 ? "Low"
            : s.BatteryPercent <= 50 ? "Medium"
            : "High";

        var input = s.Input;
        LeftStickX = Offset(input.LeftX);
        LeftStickY = Offset(input.LeftY);
        RightStickX = Offset(input.RightX);
        RightStickY = Offset(input.RightY);
        L2 = input.L2 / 255.0;
        R2 = input.R2 / 255.0;
        PressedButtons = DescribeButtons(input.Buttons);
    }

    private static double Offset(byte raw) => Math.Round((raw - 128) / 128.0 * StickTravel, 1);

    private static string DescribeButtons(DualSenseButtons b)
    {
        if (b == DualSenseButtons.None)
        {
            return "";
        }

        var names = new List<string>(8);
        void Add(DualSenseButtons flag, string label)
        {
            if ((b & flag) != 0) names.Add(label);
        }

        Add(DualSenseButtons.Cross, "✕ A");
        Add(DualSenseButtons.Circle, "○ B");
        Add(DualSenseButtons.Square, "□ X");
        Add(DualSenseButtons.Triangle, "△ Y");
        Add(DualSenseButtons.L1, "L1");
        Add(DualSenseButtons.R1, "R1");
        Add(DualSenseButtons.L3, "L3");
        Add(DualSenseButtons.R3, "R3");
        Add(DualSenseButtons.DPadUp, "↑");
        Add(DualSenseButtons.DPadDown, "↓");
        Add(DualSenseButtons.DPadLeft, "←");
        Add(DualSenseButtons.DPadRight, "→");
        Add(DualSenseButtons.Create, "Create");
        Add(DualSenseButtons.Options, "Options");
        Add(DualSenseButtons.PS, "PS");
        Add(DualSenseButtons.Touchpad, "Touchpad");
        Add(DualSenseButtons.Mute, "Mute");
        return string.Join("  ", names);
    }
}

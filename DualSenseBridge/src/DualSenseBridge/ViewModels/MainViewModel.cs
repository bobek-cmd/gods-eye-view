using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Threading;
using DualSenseBridge.Controllers;
using DualSenseBridge.Core.Configuration;
using DualSenseBridge.Services;

namespace DualSenseBridge.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ControllerManager _manager;
    private readonly HidHideManager _hidHide;
    private readonly AppSettings _settings;
    private readonly Action<AppSettings> _applySettings;
    private readonly DispatcherTimer _saveTimer;

    private int _selectedPage;
    private bool _hasControllers;
    private bool _viGEmReady;
    private string _viGEmText = "Provjera…";
    private string _viGEmVersion = "";
    private bool _hidHideInstalled;
    private bool _hidHideActive;
    private string _hidHideText = "Provjera…";
    private string _hidHideVersion = "";
    private ViGEmStatus? _versionCheckedFor;

    public MainViewModel(ControllerManager manager, HidHideManager hidHide, AppSettings settings, Action<AppSettings> applySettings, Action exit)
    {
        _manager = manager;
        _hidHide = hidHide;
        _settings = settings;
        _applySettings = applySettings;

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SettingsStore.SaveSettings(_settings);
        };

        NavigateCommand = new RelayCommand(p => SelectedPage = int.Parse((string)p!));
        OpenUrlCommand = new RelayCommand(p => DriverInfo.OpenUrl((string)p!));
        OpenLogsCommand = new RelayCommand(() => Process.Start(new ProcessStartInfo(AppPaths.LogDirectory) { UseShellExecute = true }));
        OpenBluetoothSettingsCommand = new RelayCommand(() => DriverInfo.OpenUrl("ms-settings:bluetooth"));
        RefreshDriversCommand = new RelayCommand(RefreshDrivers);
        PowerOffCommand = new RelayCommand(p => PowerOff(p as ControllerViewModel));
        TestRumbleCommand = new RelayCommand(p => _manager.Find((p as ControllerViewModel)?.Id ?? "")?.TestRumble());
        SetColorCommand = new RelayCommand(p =>
        {
            LightbarColor = (string)p!;
            LightbarMode = LightbarMode.Custom;
        });
        ExitCommand = new RelayCommand(exit);

        RefreshDriverStatus();
    }

    public ObservableCollection<ControllerViewModel> Controllers { get; } = new();

    public IReadOnlyList<Option<TouchpadClickAction>> TouchpadOptions { get; } = new[]
    {
        new Option<TouchpadClickAction>(TouchpadClickAction.None, "Ništa"),
        new Option<TouchpadClickAction>(TouchpadClickAction.Back, "Back"),
        new Option<TouchpadClickAction>(TouchpadClickAction.Start, "Start"),
        new Option<TouchpadClickAction>(TouchpadClickAction.Guide, "Guide"),
    };

    public IReadOnlyList<Option<LightbarMode>> LightbarOptions { get; } = new[]
    {
        new Option<LightbarMode>(LightbarMode.PlayerColor, "Boja igrača"),
        new Option<LightbarMode>(LightbarMode.Battery, "Baterija"),
        new Option<LightbarMode>(LightbarMode.Custom, "Vlastita"),
        new Option<LightbarMode>(LightbarMode.Off, "Isključeno"),
    };

    public IReadOnlyList<string> ColorPresets { get; } = new[]
    {
        "#0070D1", "#00B7FF", "#7C3AED", "#E83EC8", "#E01E2D", "#FF7A00", "#FFD000", "#1FC24A", "#FFFFFF",
    };

    public string AppVersion { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public ICommand NavigateCommand { get; }
    public ICommand OpenUrlCommand { get; }
    public ICommand OpenLogsCommand { get; }
    public ICommand OpenBluetoothSettingsCommand { get; }
    public ICommand RefreshDriversCommand { get; }
    public ICommand PowerOffCommand { get; }
    public ICommand TestRumbleCommand { get; }
    public ICommand SetColorCommand { get; }
    public ICommand ExitCommand { get; }

    public int SelectedPage { get => _selectedPage; set => SetProperty(ref _selectedPage, value); }
    public bool HasControllers { get => _hasControllers; private set => SetProperty(ref _hasControllers, value); }

    public bool ViGEmReady { get => _viGEmReady; private set => SetProperty(ref _viGEmReady, value); }
    public string ViGEmText { get => _viGEmText; private set => SetProperty(ref _viGEmText, value); }
    public string ViGEmVersion { get => _viGEmVersion; private set => SetProperty(ref _viGEmVersion, value); }
    public bool HidHideInstalled { get => _hidHideInstalled; private set => SetProperty(ref _hidHideInstalled, value); }
    public bool HidHideActive { get => _hidHideActive; private set => SetProperty(ref _hidHideActive, value); }
    public string HidHideText { get => _hidHideText; private set => SetProperty(ref _hidHideText, value); }
    public string HidHideVersion { get => _hidHideVersion; private set => SetProperty(ref _hidHideVersion, value); }

    // ---- Settings -------------------------------------------------------------------------

    public bool StartWithWindows { get => _settings.StartWithWindows; set => Set(() => _settings.StartWithWindows = value); }
    public bool StartMinimized { get => _settings.StartMinimized; set => Set(() => _settings.StartMinimized = value); }
    public bool MinimizeToTrayOnClose { get => _settings.MinimizeToTrayOnClose; set => Set(() => _settings.MinimizeToTrayOnClose = value); }
    public bool ShowNotifications { get => _settings.ShowNotifications; set => Set(() => _settings.ShowNotifications = value); }
    public bool HideOriginalController { get => _settings.HideOriginalController; set => Set(() => _settings.HideOriginalController = value); }
    public bool RumbleEnabled { get => _settings.RumbleEnabled; set => Set(() => _settings.RumbleEnabled = value); }
    public int RumbleStrength { get => _settings.RumbleStrength; set => Set(() => _settings.RumbleStrength = value); }
    public int LeftStickDeadzone { get => _settings.LeftStickDeadzone; set => Set(() => _settings.LeftStickDeadzone = value); }
    public int RightStickDeadzone { get => _settings.RightStickDeadzone; set => Set(() => _settings.RightStickDeadzone = value); }
    public int TriggerDeadzone { get => _settings.TriggerDeadzone; set => Set(() => _settings.TriggerDeadzone = value); }
    public TouchpadClickAction TouchpadClick { get => _settings.TouchpadClick; set => Set(() => _settings.TouchpadClick = value); }
    public LightbarMode LightbarMode { get => _settings.LightbarMode; set => Set(() => _settings.LightbarMode = value); }
    public string LightbarColor { get => _settings.LightbarColor; set => Set(() => _settings.LightbarColor = value); }
    public int LightbarBrightness { get => _settings.LightbarBrightness; set => Set(() => _settings.LightbarBrightness = value); }
    public int LowBatteryWarning { get => _settings.LowBatteryWarning; set => Set(() => _settings.LowBatteryWarning = value); }

    /// <summary>Updates the autostart toggle without re-applying it (e.g. after the tray menu changed it).</summary>
    public void SyncStartWithWindows(bool value)
    {
        _settings.StartWithWindows = value;
        OnPropertyChanged(nameof(StartWithWindows));
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void Set(Action assign, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        assign();
        _settings.Normalize();
        OnPropertyChanged(name);
        _applySettings(_settings.Clone());
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void FlushSettings()
    {
        if (_saveTimer.IsEnabled)
        {
            _saveTimer.Stop();
            SettingsStore.SaveSettings(_settings);
        }
    }

    // ---- Live state -----------------------------------------------------------------------

    /// <summary>Pulls the latest controller snapshots into the view models (UI thread).</summary>
    public void RefreshControllers()
    {
        var snapshots = _manager.GetSnapshots()
            .Where(s => s.State is SessionState.Active or SessionState.Error)
            .ToList();

        for (int i = Controllers.Count - 1; i >= 0; i--)
        {
            if (!snapshots.Any(s => s.Id == Controllers[i].Id))
            {
                Controllers.RemoveAt(i);
            }
        }

        foreach (var snapshot in snapshots)
        {
            var vm = Controllers.FirstOrDefault(c => c.Id == snapshot.Id);
            if (vm == null)
            {
                vm = new ControllerViewModel(snapshot.Id);
                Controllers.Add(vm);
            }

            vm.Update(snapshot);
        }

        HasControllers = Controllers.Count > 0;
    }

    public void RefreshDriverStatus()
    {
        ViGEmReady = _manager.ViGEmStatus == ViGEmStatus.Ready;
        ViGEmText = _manager.ViGEmStatus switch
        {
            ViGEmStatus.Unknown => "Provjera…",
            _ => _manager.ViGEmMessage,
        };
        if (_versionCheckedFor != _manager.ViGEmStatus)
        {
            _versionCheckedFor = _manager.ViGEmStatus;
            ViGEmVersion = DriverInfo.GetViGEmBusVersion() is { } v ? $"Verzija {v}" : "";
        }

        HidHideInstalled = _hidHide.Status != HidHideStatus.NotInstalled;
        HidHideActive = _hidHide.Status == HidHideStatus.Ready;
        HidHideText = _hidHide.StatusMessage;
        HidHideVersion = _hidHide.DriverVersion is { } hv ? $"Verzija {hv}" : "";
    }

    private void RefreshDrivers()
    {
        _manager.Rescan();
        ThreadPool.QueueUserWorkItem(_ => _hidHide.Refresh());
    }

    private void PowerOff(ControllerViewModel? vm)
    {
        var session = vm == null ? null : _manager.Find(vm.Id);
        if (session == null)
        {
            return;
        }

        ThreadPool.QueueUserWorkItem(_ =>
        {
            if (!session.PowerOff())
            {
                Log.Warn($"Power off failed for {session.Name}");
            }
        });
    }
}

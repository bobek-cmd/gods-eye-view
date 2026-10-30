using System.Windows;
using System.Windows.Threading;
using DualSenseBridge.Controllers;
using DualSenseBridge.Core.Configuration;
using DualSenseBridge.Core.DualSense;
using DualSenseBridge.Services;
using DualSenseBridge.ViewModels;

namespace DualSenseBridge;

public partial class App : Application
{
    private SingleInstance? _instance;
    private AppSettings _settings = new();
    private AppSettings _applied = new();
    private RuntimeState _state = new();
    private HidHideManager? _hidHide;
    private ControllerManager? _manager;
    private TrayIconService? _tray;
    private MainViewModel? _viewModel;
    private MainWindow? _window;
    private DispatcherTimer? _uiTimer;
    private DateTime _lastTrayUpdate;
    private bool _shutdownDone;

    public bool IsExiting { get; private set; }

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var args = e.Args.Select(a => a.ToLowerInvariant()).ToHashSet();

        if (args.Contains("--register-hidhide"))
        {
            // Called by the installer so the app is allowed by HidHide before its first start.
            HidHideManager.RegisterApplication();
            Shutdown(0);
            return;
        }

        if (args.Contains("--cleanup"))
        {
            // Called by the uninstaller: undo every system change and exit.
            Log.Info("Cleanup requested");
            HidHideManager.Cleanup();
            AutoStartService.Disable();
            Shutdown(0);
            return;
        }

        _instance = SingleInstance.TryAcquire(() => Dispatcher.BeginInvoke(ShowMainWindow));
        if (_instance == null)
        {
            Shutdown(0);
            return;
        }

        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Error("Unhandled UI exception", ex.Exception);
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            Log.Error("Unhandled exception", ex.ExceptionObject as Exception);

        Log.Info($"Starting DualSense Bridge {typeof(App).Assembly.GetName().Version} ({string.Join(' ', e.Args)})");
        System.Windows.Forms.Application.EnableVisualStyles();

        _settings = SettingsStore.LoadSettings();
        _applied = _settings.Clone();
        _state = SettingsStore.LoadState();
        bool firstRun = !_state.FirstRunCompleted;
        if (firstRun)
        {
            _state.FirstRunCompleted = true;
            SettingsStore.SaveState(_state);
            SettingsStore.SaveSettings(_settings);
        }

        _hidHide = new HidHideManager(_state);
        _manager = new ControllerManager(_hidHide, _settings.Clone());
        _viewModel = new MainViewModel(_manager, _hidHide, _settings, ApplySettings, ExitApplication);
        _tray = new TrayIconService(ShowMainWindow, PowerOffAll, SetAutostartFromTray, ExitApplication);

        _manager.Changed += () => Dispatcher.BeginInvoke(OnControllersChanged);
        _manager.ControllerConnected += s => Dispatcher.BeginInvoke(() => Notify(
            $"{s.Name} povezan",
            $"{(s.Connection == ConnectionType.Bluetooth ? "Bluetooth" : "USB")} – radi kao Xbox 360 kontroler."));
        _manager.ControllerDisconnected += s => Dispatcher.BeginInvoke(() => Notify(
            $"{s.Name} odspojen", "Virtualni Xbox 360 kontroler je uklonjen."));
        _manager.LowBattery += (s, percent) => Dispatcher.BeginInvoke(() => Notify(
            "Slaba baterija", $"{s.Name}: {percent} % – spojite punjač.", warning: true));
        _hidHide.StatusChanged += () => Dispatcher.BeginInvoke(() => _viewModel.RefreshDriverStatus());

        _uiTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(33) };
        _uiTimer.Tick += (_, _) => OnUiTick();
        _uiTimer.Start();

        // Hide controllers we already know before they can connect, then start watching for them.
        bool hide = _settings.HideOriginalController;
        Task.Run(() =>
        {
            _hidHide.Initialize(hide);
            _manager.Start();
        });

        SyncAutostart(_settings.StartWithWindows);

        bool startInTray = args.Contains("--tray") && _settings.StartMinimized && !firstRun;
        if (!startInTray)
        {
            ShowMainWindow();
        }
    }

    public void ShowMainWindow()
    {
        if (IsExiting || _viewModel == null)
        {
            return;
        }

        if (_window == null)
        {
            _window = new MainWindow(_viewModel);
            _window.Closed += (_, _) => _window = null;
        }

        _viewModel.RefreshDriverStatus();
        _viewModel.RefreshControllers();
        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
        _window.Topmost = true;
        _window.Topmost = false;
        _window.Focus();
    }

    /// <summary>Called by the window when the user closes it.</summary>
    public bool ShouldHideOnClose => !IsExiting && _settings.MinimizeToTrayOnClose;

    public void ExitApplication()
    {
        if (IsExiting)
        {
            return;
        }

        IsExiting = true;
        _window?.Close();
        ShutdownServices();
        Shutdown(0);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        Log.Info($"Windows session ending ({e.ReasonSessionEnding})");
        IsExiting = true;
        ShutdownServices();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ShutdownServices();
        base.OnExit(e);
    }

    private void ShutdownServices()
    {
        if (_shutdownDone || _manager == null)
        {
            return;
        }

        _shutdownDone = true;
        _uiTimer?.Stop();
        _viewModel?.FlushSettings();

        try
        {
            _manager.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error("Error while stopping controllers", ex);
        }

        // Make the physical controller visible again for everything else.
        _hidHide?.RestoreAll();
        _tray?.Dispose();
        _instance?.Dispose();
        Log.Info("Stopped");
    }

    private void OnUiTick()
    {
        if (_viewModel == null || _manager == null)
        {
            return;
        }

        bool visible = _window is { IsVisible: true, WindowState: not WindowState.Minimized };
        if (visible)
        {
            _viewModel.RefreshControllers();
        }

        // Live preview at ~30 fps while the window is shown, otherwise only a slow tray refresh.
        var interval = TimeSpan.FromMilliseconds(visible ? 33 : 1000);
        if (_uiTimer != null && _uiTimer.Interval != interval)
        {
            _uiTimer.Interval = interval;
        }

        if (DateTime.UtcNow - _lastTrayUpdate > TimeSpan.FromSeconds(1))
        {
            _lastTrayUpdate = DateTime.UtcNow;
            _tray?.Update(_manager.GetSnapshots());
            _viewModel.RefreshDriverStatus();
        }
    }

    private void OnControllersChanged()
    {
        if (_viewModel == null || _manager == null)
        {
            return;
        }

        _viewModel.RefreshDriverStatus();
        _viewModel.RefreshControllers();
        _tray?.Update(_manager.GetSnapshots());
    }

    private void ApplySettings(AppSettings settings)
    {
        var previous = _applied;
        _applied = settings;
        _manager?.UpdateSettings(settings);

        if (previous.HideOriginalController != settings.HideOriginalController && _hidHide != null)
        {
            bool enabled = settings.HideOriginalController;
            Task.Run(() => _hidHide.SetEnabled(enabled));
        }

        if (previous.StartWithWindows != settings.StartWithWindows)
        {
            SyncAutostart(settings.StartWithWindows);
        }
    }

    private void SetAutostartFromTray(bool enabled)
    {
        _viewModel?.SyncStartWithWindows(enabled);
        _applied.StartWithWindows = enabled;
        SyncAutostart(enabled);
    }

    private void SyncAutostart(bool enabled)
    {
        _tray?.SetAutostartChecked(enabled);
        Task.Run(() =>
        {
            bool ok = enabled ? AutoStartService.Enable() : AutoStartService.Disable();
            if (!ok)
            {
                Dispatcher.BeginInvoke(() => Notify("Automatsko pokretanje",
                    "Postavku nije bilo moguće primijeniti – pogledajte logove.", warning: true, force: true));
            }
        });
    }

    private void PowerOffAll()
    {
        if (_manager == null)
        {
            return;
        }

        foreach (var snapshot in _manager.GetSnapshots().Where(s => s.State == SessionState.Active))
        {
            var session = _manager.Find(snapshot.Id);
            if (session != null)
            {
                Task.Run(session.PowerOff);
            }
        }
    }

    private void Notify(string title, string text, bool warning = false, bool force = false)
    {
        if (IsExiting || (!force && !_settings.ShowNotifications))
        {
            return;
        }

        _tray?.ShowNotification(title, text, warning);
    }
}

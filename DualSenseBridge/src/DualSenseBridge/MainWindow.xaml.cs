using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using DualSenseBridge.Interop;
using DualSenseBridge.ViewModels;

namespace DualSenseBridge;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += OnSourceInitialized;
        StateChanged += (_, _) =>
        {
            // A chrome-less window overhangs the screen when maximized; compensate for it.
            RootBorder.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        };
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        int round = NativeMethods.DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (App.Current.ShouldHideOnClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
        if (!App.Current.IsExiting)
        {
            // Exit once the window has finished closing (closing it again from here would throw).
            Dispatcher.BeginInvoke(App.Current.ExitApplication);
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

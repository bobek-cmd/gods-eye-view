using System.IO;
using DualSenseBridge.Controllers;
using DualSenseBridge.Core.DualSense;
using DualSenseBridge.Interop;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace DualSenseBridge.Services;

/// <summary>Notification area icon with a battery indicator dot, tooltip and context menu.</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Drawing.Icon _baseIcon;
    private readonly Forms.ToolStripMenuItem _autostartItem;
    private readonly Forms.ToolStripMenuItem _powerOffItem;
    private Drawing.Icon? _currentIcon;
    private string _iconKey = "";

    public TrayIconService(Action open, Action powerOffAll, Action<bool> setAutostart, Action exit)
    {
        using (Stream stream = System.Windows.Application.GetResourceStream(
                   new Uri("pack://application:,,,/Assets/app.ico")).Stream)
        {
            _baseIcon = new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
        }

        var menu = new Forms.ContextMenuStrip();
        var openItem = new Forms.ToolStripMenuItem("Otvori DualSense Bridge", null, (_, _) => open())
        {
            Font = new Drawing.Font(menu.Font, Drawing.FontStyle.Bold),
        };
        _powerOffItem = new Forms.ToolStripMenuItem("Isključi kontroler(e)", null, (_, _) => powerOffAll());
        _autostartItem = new Forms.ToolStripMenuItem("Pokreni s Windowsima", null, (_, _) =>
        {
            _autostartItem!.Checked = !_autostartItem.Checked;
            setAutostart(_autostartItem.Checked);
        });
        menu.Items.Add(openItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_powerOffItem);
        menu.Items.Add(_autostartItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Izlaz", null, (_, _) => exit()));

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _baseIcon,
            Text = "DualSense Bridge",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                open();
            }
        };

        Update(Array.Empty<ControllerSnapshot>());
    }

    public void SetAutostartChecked(bool value) => _autostartItem.Checked = value;

    public void ShowNotification(string title, string text, bool warning = false)
    {
        _notifyIcon.ShowBalloonTip(4000, title, text, warning ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info);
    }

    public void Update(IReadOnlyList<ControllerSnapshot> controllers)
    {
        var active = controllers.Where(c => c.State == SessionState.Active).ToList();
        _powerOffItem.Enabled = active.Any(c => c.Connection == ConnectionType.Bluetooth && c.BluetoothAddress != 0);

        string tooltip;
        if (active.Count == 0)
        {
            tooltip = "DualSense Bridge – čeka kontroler";
        }
        else
        {
            var parts = active.Select(c =>
            {
                string battery = c.BatteryPercent >= 0 ? $"{c.BatteryPercent}%" : "?";
                if (c.Charging is ChargingState.Charging) battery += " ⚡";
                return $"{c.Name} ({(c.Connection == ConnectionType.Bluetooth ? "BT" : "USB")}) {battery}";
            });
            tooltip = "DualSense Bridge\n" + string.Join("\n", parts);
        }

        // NotifyIcon.Text is limited to 127 characters.
        _notifyIcon.Text = tooltip.Length > 127 ? tooltip[..127] : tooltip;

        int lowest = active.Where(c => c.BatteryPercent >= 0).Select(c => c.BatteryPercent).DefaultIfEmpty(-1).Min();
        bool charging = active.Any(c => c.Charging is ChargingState.Charging);
        string key = active.Count == 0 ? "idle" : $"{BatteryBucket(lowest)}-{charging}";
        if (key != _iconKey)
        {
            _iconKey = key;
            SetIcon(active.Count == 0 ? null : DotColor(lowest, charging));
        }
    }

    private static int BatteryBucket(int percent) => percent < 0 ? -1 : percent <= 20 ? 0 : percent <= 50 ? 1 : 2;

    private static Drawing.Color DotColor(int percent, bool charging)
    {
        if (charging) return Drawing.Color.FromArgb(0x3B, 0x82, 0xF6);
        return BatteryBucket(percent) switch
        {
            0 => Drawing.Color.FromArgb(0xEF, 0x44, 0x44),
            1 => Drawing.Color.FromArgb(0xF5, 0x9E, 0x0B),
            2 => Drawing.Color.FromArgb(0x22, 0xC5, 0x5E),
            _ => Drawing.Color.FromArgb(0x9A, 0xA0, 0xAA),
        };
    }

    private void SetIcon(Drawing.Color? dot)
    {
        var size = Forms.SystemInformation.SmallIconSize;
        using var bitmap = new Drawing.Bitmap(size.Width, size.Height);
        using (var g = Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.DrawIcon(_baseIcon, new Drawing.Rectangle(0, 0, size.Width, size.Height));
            if (dot is { } color)
            {
                float d = size.Width * 0.5f;
                var rect = new Drawing.RectangleF(size.Width - d, size.Height - d, d - 0.5f, d - 0.5f);
                using var outline = new Drawing.SolidBrush(Drawing.Color.FromArgb(0x0F, 0x11, 0x15));
                using var fill = new Drawing.SolidBrush(color);
                g.FillEllipse(outline, rect);
                rect.Inflate(-size.Width * 0.08f, -size.Height * 0.08f);
                g.FillEllipse(fill, rect);
            }
        }

        IntPtr handle = bitmap.GetHicon();
        var icon = (Drawing.Icon)Drawing.Icon.FromHandle(handle).Clone();
        NativeMethods.DestroyIcon(handle);

        _notifyIcon.Icon = icon;
        _currentIcon?.Dispose();
        _currentIcon = icon;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
        _baseIcon.Dispose();
    }
}

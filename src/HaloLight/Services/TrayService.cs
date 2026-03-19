using System.Drawing;
using Forms = System.Windows.Forms;

namespace HaloLight.Services;

public sealed class TrayService : IDisposable
{
    private readonly Icon _icon;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _toggleItem;

    public TrayService(Action toggleOverlay, Action showSettings, Action exitApplication)
    {
        _icon = LoadTrayIcon();
        _toggleItem = new Forms.ToolStripMenuItem("Turn Off", null, (_, _) => toggleOverlay());

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new Forms.ToolStripMenuItem("Settings", null, (_, _) => showSettings()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Exit", null, (_, _) => exitApplication()));

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "HaloLight",
            Icon = _icon,
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => showSettings();
    }

    public void UpdateEnabledState(bool isEnabled)
    {
        _toggleItem.Text = isEnabled ? "Turn Off" : "Turn On";
        _notifyIcon.Text = isEnabled ? "HaloLight - On" : "HaloLight - Off";
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon.Dispose();
    }

    private static Icon LoadTrayIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/AppIcon.ico"));
        if (resource is null)
        {
            return SystemIcons.Application;
        }

        using var iconStream = resource.Stream;
        using var icon = new Icon(iconStream);
        return (Icon)icon.Clone();
    }
}

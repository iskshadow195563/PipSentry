using System;
using System.Drawing;
using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;

namespace PipSentry.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _pauseItem;
    private bool _disposed;

    public event Action? ShowShellRequested;
    public event Action? ShowMonitorRequested;
    public event Action? PauseToggleRequested;
    public event Action? ExitRequested;

    public TrayService()
    {
        _icon = new Forms.NotifyIcon
        {
            Text = "PipSentry 視窗監控",
            Icon = LoadIcon(),
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip(),
        };

        var menu = _icon.ContextMenuStrip!;
        var open = new Forms.ToolStripMenuItem("開啟主視窗");
        open.Click += (_, _) => ShowShellRequested?.Invoke();

        var monitor = new Forms.ToolStripMenuItem("顯示監控視窗");
        monitor.Click += (_, _) => ShowMonitorRequested?.Invoke();

        _pauseItem = new Forms.ToolStripMenuItem("暫停擷取");
        _pauseItem.Click += (_, _) => PauseToggleRequested?.Invoke();

        var exit = new Forms.ToolStripMenuItem("結束 PipSentry");
        exit.Click += (_, _) => ExitRequested?.Invoke();

        menu.Items.Add(open);
        menu.Items.Add(monitor);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exit);

        _icon.DoubleClick += (_, _) => ShowShellRequested?.Invoke();
    }

    public void SetPaused(bool paused) => _pauseItem.Text = paused ? "繼續擷取" : "暫停擷取";

    public void Notify(string title, string message)
    {
        try
        {
            _icon.BalloonTipTitle = title;
            _icon.BalloonTipText = message;
            _icon.ShowBalloonTip(2600);
        }
        catch
        {
        }
    }

    private static Icon LoadIcon()
    {
        try
        {
            var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
            if (resource is not null)
            {
                using var stream = resource.Stream;
                return new Icon(stream);
            }
        }
        catch
        {
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _icon.Visible = false;
        _icon.Dispose();
    }
}

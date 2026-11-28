using System;
using System.Drawing;
using System.Windows;
using PipSentry.Platform;
using Forms = System.Windows.Forms;

namespace PipSentry.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _openItem;
    private readonly Forms.ToolStripMenuItem _monitorItem;
    private readonly Forms.ToolStripMenuItem _pauseItem;
    private readonly Forms.ToolStripMenuItem _updateItem;
    private readonly Forms.ToolStripMenuItem _autoUpdateItem;
    private readonly Forms.ToolStripMenuItem _startItem;
    private readonly Forms.ToolStripMenuItem _exitItem;
    private bool _paused;
    private bool _disposed;

    public event Action? ShowShellRequested;
    public event Action? ShowMonitorRequested;
    public event Action? PauseToggleRequested;
    public event Action? ExitRequested;
    public event Action<bool>? AutostartToggled;
    public event Action? CheckUpdatesRequested;
    public event Action<bool>? AutoUpdateToggled;

    public TrayService()
    {
        _icon = new Forms.NotifyIcon
        {
            Text = L.Get("TrayTip"),
            Icon = LoadIcon(),
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip(),
        };

        var menu = _icon.ContextMenuStrip!;
        _openItem = new Forms.ToolStripMenuItem(L.Get("TrayOpen"));
        _openItem.Click += (_, _) => ShowShellRequested?.Invoke();

        _monitorItem = new Forms.ToolStripMenuItem(L.Get("TrayShowMonitor"));
        _monitorItem.Click += (_, _) => ShowMonitorRequested?.Invoke();

        _pauseItem = new Forms.ToolStripMenuItem(L.Get("MenuPause"));
        _pauseItem.Click += (_, _) => PauseToggleRequested?.Invoke();

        _updateItem = new Forms.ToolStripMenuItem(L.Get("UpdateCheck"));
        _updateItem.Click += (_, _) => CheckUpdatesRequested?.Invoke();

        _autoUpdateItem = new Forms.ToolStripMenuItem(L.Get("UpdateAutoCheck"))
        {
            CheckOnClick = true,
            Checked = App.Settings.AutoCheckUpdates,
        };
        _autoUpdateItem.CheckedChanged += (_, _) => AutoUpdateToggled?.Invoke(_autoUpdateItem.Checked);

        _startItem = new Forms.ToolStripMenuItem(L.Get("AutostartTitle"))
        {
            CheckOnClick = true,
            Checked = AutostartService.IsEnabled,
        };
        _startItem.CheckedChanged += (_, _) => AutostartToggled?.Invoke(_startItem.Checked);

        _exitItem = new Forms.ToolStripMenuItem(L.Get("TrayExit"));
        _exitItem.Click += (_, _) => ExitRequested?.Invoke();

        menu.Items.Add(_openItem);
        menu.Items.Add(_monitorItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_pauseItem);
        menu.Items.Add(_updateItem);
        menu.Items.Add(_autoUpdateItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_startItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_exitItem);

        _icon.DoubleClick += (_, _) => ShowShellRequested?.Invoke();
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        _pauseItem.Text = L.Get(paused ? "MenuResume" : "MenuPause");
    }

    public void RefreshTexts()
    {
        _icon.Text = L.Get("TrayTip");
        _openItem.Text = L.Get("TrayOpen");
        _monitorItem.Text = L.Get("TrayShowMonitor");
        _updateItem.Text = L.Get("UpdateCheck");
        _autoUpdateItem.Text = L.Get("UpdateAutoCheck");
        _autoUpdateItem.Checked = App.Settings.AutoCheckUpdates;
        _startItem.Text = L.Get("AutostartTitle");
        _startItem.Checked = AutostartService.IsEnabled;
        _exitItem.Text = L.Get("TrayExit");
        _pauseItem.Text = L.Get(_paused ? "MenuResume" : "MenuPause");
    }

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
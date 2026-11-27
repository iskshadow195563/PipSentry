using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Input;
using PipSentry.Capture;
using PipSentry.Interop;
using PipSentry.Platform;
using PipSentry.Services;
using PipSentry.Views;

namespace PipSentry;

public partial class App : Application
{
    private static Mutex? _instanceMutex;
    private static bool _isExiting;
    private static int _hotkeyToggle;
    private static int _hotkeyPause;
    private static int _hotkeyScreenshot;
    private static int _hotkeyShell;
    private static int _hotkeyThrough;

    public static AppSettings Settings { get; private set; } = new();
    public static HotkeyHost? Hotkeys { get; private set; }
    public static TrayService? Tray { get; private set; }
    public static ShellWindow? Shell { get; private set; }
    public static MonitorWindow? Monitor { get; private set; }
    public static KeepAliveService KeepAlive { get; } = new();
    public static CaptureController? MonitorController => null;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Single frame dump: PipSentry.exe --grab <png> [--scale WxH] [--region x,y,w,h]
        var grabPath = GetArg(e.Args, "--grab");
        if (!string.IsNullOrWhiteSpace(grabPath))
        {
            Settings = SettingsStore.Load();
            Loc.Apply(Settings.Language);
            ThemeManager.Apply(Settings.Theme);
            GrabFrame(grabPath!, GetArg(e.Args, "--scale"), GetArg(e.Args, "--region"));
            Current.Shutdown();
            return;
        }

        // Capture benchmark: PipSentry.exe --bench <report.txt> [--fps 60] [--region x,y,w,h]
        var benchPath = GetArg(e.Args, "--bench");
        if (!string.IsNullOrWhiteSpace(benchPath))
        {
            Settings = SettingsStore.Load();
            Loc.Apply(Settings.Language);
            ThemeManager.Apply(Settings.Theme);
            RunBench(benchPath!, GetArg(e.Args, "--fps"), GetArg(e.Args, "--region"),
                GetArg(e.Args, "--scale"), e.Args.Contains("--paused"), GetArg(e.Args, "--seconds"));
            Current.Shutdown();
            return;
        }

        // Headless UI render: PipSentry.exe --render <png> [--lang zh-Hant|zh-Hans|en-US] [--theme Dark|Light]
        var renderPath = GetArg(e.Args, "--render");
        if (!string.IsNullOrWhiteSpace(renderPath))
        {
            Settings = SettingsStore.Load();
            Settings.Language = GetArg(e.Args, "--lang") ?? (string.IsNullOrWhiteSpace(Settings.Language) ? Loc.DetectSystem() : Settings.Language);
            Loc.Apply(Settings.Language);
            ThemeManager.Apply(GetArg(e.Args, "--theme") ?? Settings.Theme);
            RenderShell(renderPath!);
            Current.Shutdown();
            return;
        }

        _instanceMutex = new Mutex(true, @"Global\PipSentry.SingleInstance", out var created);
        if (!created)
        {
            Shutdown();
            return;
        }

        Settings = SettingsStore.Load();
        if (string.IsNullOrWhiteSpace(Settings.Language)) Settings.Language = Loc.DetectSystem();
        Loc.Apply(Settings.Language);
        ThemeManager.Apply(Settings.Theme);
        KeepAliveService.OptOutOfPowerThrottling();
        AppLog.Write("startup");

        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Write("unhandled: " + args.Exception);
            if (Shell is null)
            {
                args.Handled = true;
                AppLog.Write("fatal startup failure");
                ShutdownApp();
                return;
            }

            Toast(L.Get("MsgUnexpected") + args.Exception.Message);
            args.Handled = true;
        };

        Shell = new ShellWindow();
        Shell.Show();

        Hotkeys = new HotkeyHost();
        _hotkeyToggle = Hotkeys.Register(ModifierKeys.Control | ModifierKeys.Alt, Key.M);
        _hotkeyPause = Hotkeys.Register(ModifierKeys.Control | ModifierKeys.Alt, Key.P);
        _hotkeyScreenshot = Hotkeys.Register(ModifierKeys.Control | ModifierKeys.Alt, Key.S);
        _hotkeyShell = Hotkeys.Register(ModifierKeys.Control | ModifierKeys.Alt, Key.R);
        _hotkeyThrough = Hotkeys.Register(ModifierKeys.Control | ModifierKeys.Alt, Key.T);
        Hotkeys.Pressed += OnHotkey;

        Tray = new TrayService();
        Tray.ShowShellRequested += ShowShell;
        Tray.ShowMonitorRequested += ToggleMonitorVisibility;
        Tray.PauseToggleRequested += () => Monitor?.TogglePause();
        Tray.ExitRequested += ShutdownApp;
    }

    private static void OnHotkey(int id)
    {
        if (id == _hotkeyToggle) ToggleMonitorVisibility();
        else if (id == _hotkeyPause) Monitor?.TogglePause();
        else if (id == _hotkeyScreenshot) Monitor?.SaveScreenshot();
        else if (id == _hotkeyShell) ShowShell();
        else if (id == _hotkeyThrough) Monitor?.ToggleClickThrough();
    }

    public static void StartMonitoring(WindowInfo source)
    {
        Settings.LastSourceKey = source.Key;
        Settings.LastSourceTitle = source.Title;
        SettingsStore.Save(Settings);

        if (Settings.OptimizeTargetApp && !string.IsNullOrWhiteSpace(source.ExecutablePath))
        {
            KeepAliveService.DisableWindowedGameOptimizations(source.ExecutablePath);
        }

        if (Monitor is null)
        {
            Monitor = new MonitorWindow();
            Monitor.Closed += OnMonitorClosed;
        }

        Monitor.Apply(source, Settings);
        Monitor.Show();
        Monitor.Activate();

        Shell?.Hide();
        KeepAlive.Start(Settings.PreventSleep, Settings.NudgeTarget, source.Handle);
        AppLog.Write($"monitor start: {source.Title}");
    }

    private static void OnMonitorClosed(object? sender, EventArgs e)
    {
        KeepAlive.Stop();
        Monitor = null;
        if (_isExiting) return;
        ShowShell();
    }

    public static void ShowShell()
    {
        if (Shell is null) return;
        Monitor?.Hide();
        KeepAlive.Stop();
        if (!Shell.IsVisible) Shell.Show();
        if (Shell.WindowState == WindowState.Minimized) Shell.WindowState = WindowState.Normal;
        Shell.Activate();
        Shell.RefreshFromSettings();
    }

    public static void ToggleMonitorVisibility()
    {
        if (Monitor is null)
        {
            ShowShell();
            return;
        }

        if (Monitor.IsVisible)
        {
            Monitor.Hide();
            Shell?.Show();
            Shell?.Activate();
        }
        else
        {
            Monitor.Show();
            Monitor.Activate();
            Shell?.Hide();
        }
    }

    public static void CloseMonitor()
    {
        if (Monitor is null) return;
        Monitor.Close();
    }

    public static void Toast(string message)
    {
        Shell?.ShowToast(message);
        Tray?.Notify("PipSentry", message);
        AppLog.Write("toast: " + message);
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        }

        return null;
    }

    private static void GrabFrame(string outputPath, string? scaleArg, string? regionArg)
    {
        var source = WindowCatalog.Enumerate().FirstOrDefault(w => w.IsDisplay);
        if (source is null) return;

        PixelRect? region = null;
        if (!string.IsNullOrWhiteSpace(regionArg))
        {
            var parts = regionArg.Split(',', 'x');
            if (parts.Length == 4 && int.TryParse(parts[0], out var rx) && int.TryParse(parts[1], out var ry) &&
                int.TryParse(parts[2], out var rw) && int.TryParse(parts[3], out var rh))
            {
                region = new PixelRect(rx, ry, rw, rh);
            }
        }

        int scaleW = 0, scaleH = 0;
        if (!string.IsNullOrWhiteSpace(scaleArg))
        {
            var parts = scaleArg.Split('x', 'X');
            if (parts.Length == 2) { int.TryParse(parts[0], out scaleW); int.TryParse(parts[1], out scaleH); }
        }

        BitmapSource? saved = null;
        var controller = new CaptureController(Current.Dispatcher);
        controller.FrameReady += frame =>
        {
            if (saved is not null) return;
            try { saved = FrameBitmap.Create(frame); } catch { }
        };

        controller.Start(source, region, 30);
        if (scaleW > 0 && scaleH > 0) controller.SetOutputSize(scaleW, scaleH);

        var pump = new DispatcherFrame();
        var timer = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Background,
            (_, _) => pump.Continue = false, Current.Dispatcher);
        timer.Start();
        Dispatcher.PushFrame(pump);
        controller.Dispose();

        if (saved is null) return;
        var directory = System.IO.Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory)) System.IO.Directory.CreateDirectory(directory);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(saved));
        using var stream = System.IO.File.Create(outputPath);
        encoder.Save(stream);
    }

    private static void RunBench(string outputPath, string? fpsArg, string? regionArg, string? scaleArg, bool startPaused,
        string? secondsArg)
    {
        var seconds = double.TryParse(secondsArg, out var parsedSeconds) && parsedSeconds >= 2 ? parsedSeconds : 10;
        var targetFps = int.TryParse(fpsArg, out var parsedFps) ? parsedFps : 60;
        PixelRect? region = null;

        if (!string.IsNullOrWhiteSpace(regionArg))
        {
            var parts = regionArg.Split(',', 'x');
            if (parts.Length == 4 &&
                int.TryParse(parts[0], out var rx) &&
                int.TryParse(parts[1], out var ry) &&
                int.TryParse(parts[2], out var rw) &&
                int.TryParse(parts[3], out var rh))
            {
                region = new PixelRect(rx, ry, rw, rh);
            }
        }

        var source = WindowCatalog.Enumerate().FirstOrDefault(w => w.IsDisplay);
        if (source is null)
        {
            System.IO.File.WriteAllText(outputPath, "no display found");
            return;
        }

        var frames = 0;
        long bytes = 0;
        var controller = new CaptureController(Current.Dispatcher);
        controller.FrameReady += frame =>
        {
            frames++;
            bytes += frame.Buffer.Length;
        };

        var report = new System.Text.StringBuilder();
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var cpuBefore = process.TotalProcessorTime;
        var memoryBefore = process.WorkingSet64;
        var started = DateTime.UtcNow;

        controller.Start(source, region, targetFps);

        if (!string.IsNullOrWhiteSpace(scaleArg))
        {
            var parts = scaleArg.Split('x', 'X');
            if (parts.Length == 2 && int.TryParse(parts[0], out var sw) && int.TryParse(parts[1], out var sh))
            {
                controller.SetOutputSize(sw, sh);
            }
        }

        if (startPaused) controller.Pause();

        var pump = new DispatcherFrame();
        var timer = new DispatcherTimer(TimeSpan.FromSeconds(seconds), DispatcherPriority.Background,
            (_, _) => pump.Continue = false, Current.Dispatcher);
        timer.Start();
        Dispatcher.PushFrame(pump);

        var elapsed = (DateTime.UtcNow - started).TotalSeconds;
        process.Refresh();
        var cpuUsed = process.TotalProcessorTime - cpuBefore;
        var memoryAfter = process.WorkingSet64;
        controller.Dispose();

        report.AppendLine($"source        : display {source.Width}x{source.Height}");
        report.AppendLine($"region        : {(region is { IsEmpty: false } r ? $"{r.X},{r.Y} {r.Width}x{r.Height}" : "full")}");
        report.AppendLine($"target fps    : {targetFps}");
        report.AppendLine($"elapsed       : {elapsed:0.0} s");
        report.AppendLine($"frames        : {frames} ({frames / elapsed:0.0} fps)");
        report.AppendLine($"pixel traffic : {bytes / 1024.0 / 1024.0 / elapsed:0.0} MB/s (frames copied to CPU)");
        report.AppendLine($"cpu           : {cpuUsed.TotalSeconds / elapsed * 100:0.0}% of one core, " +
                          $"{cpuUsed.TotalSeconds / elapsed * 100 / Environment.ProcessorCount:0.00}% of {Environment.ProcessorCount} cores");
        report.AppendLine($"output cap    : {scaleArg ?? "native"}");
        report.AppendLine($"paused        : {startPaused}");
        report.AppendLine($"working set   : {memoryBefore / 1024 / 1024} MB -> {memoryAfter / 1024 / 1024} MB");

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputPath) ?? ".");
        System.IO.File.WriteAllText(outputPath, report.ToString());
    }

    private static void RenderShell(string path)
    {
        var window = new ShellWindow { Width = 1148, Height = 866 };
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(1148, 866));
        content.Arrange(new Rect(0, 0, 1148, 866));
        content.UpdateLayout();

        const double dpi = 1.5;
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)(1148 * dpi), (int)(866 * dpi), 96 * dpi, 96 * dpi,
            System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(content);

        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) System.IO.Directory.CreateDirectory(directory);

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = System.IO.File.Create(path);
        encoder.Save(stream);
        AppLog.Write("rendered shell: " + path);
    }

    public static void SetLanguage(string culture)
    {
        Settings.Language = culture;
        Loc.Apply(culture);
        SettingsStore.Save(Settings);
        Shell?.RefreshLanguageUi();
        Tray?.RefreshTexts();
    }

    public static void ToggleTheme()
    {
        var next = ThemeManager.IsDark ? "Light" : "Dark";
        Settings.Theme = next;

        void Apply()
        {
            ThemeManager.Apply(next);
            Shell?.RefreshThemeIcon();
            Shell?.ApplyChromeForTheme();
        }

        if (Shell is { IsVisible: true } shell) shell.RunThemeTransition(Apply);
        else Apply();

        SettingsStore.Save(Settings);
    }

    public static void ShutdownApp()
    {
        if (_isExiting) return;
        _isExiting = true;

        try { Monitor?.SaveBounds(); } catch { }
        try { KeepAlive.Dispose(); } catch { }
        try { Hotkeys?.Dispose(); } catch { }
        try { Tray?.Dispose(); } catch { }
        try { Monitor?.Close(); } catch { }
        try { Shell?.Close(); } catch { }

        Current.Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _isExiting = true;
        try { KeepAlive.Dispose(); } catch { }
        try { Tray?.Dispose(); } catch { }
        try { Hotkeys?.Dispose(); } catch { }
        _instanceMutex?.Dispose();
        AppLog.Write("exit");
        base.OnExit(e);
    }
}
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PipSentry.Platform;
using PipSentry.Capture;
using PipSentry.Interop;
using PipSentry.Services;

namespace PipSentry.Views;

public partial class MonitorWindow : Window
{
    private CaptureController? _controller;
    private WriteableBitmap? _bitmap;
    private WindowInfo? _source;
    private readonly DispatcherTimer _statsTimer;
    private int _lastFrames;
    private DateTime _lastStats = DateTime.UtcNow;
    private bool _paused;
    private bool _clickThrough;
    private PixelRect? _region;
    private double _aspect = 16.0 / 9.0;
    private int _sourceWidth;
    private int _sourceHeight;
    private bool _chromeVisible;

    public MonitorWindow()
    {
        InitializeComponent();

        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _statsTimer.Tick += (_, _) => UpdateStats();

        SourceInitialized += OnSourceInitialized;
        MouseEnter += (_, _) => ShowToolbar(true);
        MouseLeave += (_, _) => ShowToolbar(false);
        MouseRightButtonUp += OnContextMenuRequest;
        MouseLeftButtonDown += (_, e) =>
        {
            if (_clickThrough || e.ButtonState != MouseButtonState.Pressed) return;
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // A synthetic click can land here without the button held down.
            }
        };
        Closed += OnClosedInternal;
        Loaded += (_, _) => ShowToolbar(false);
        SizeChanged += (_, _) => UpdateOutputSize();
        IsVisibleChanged += OnVisibilityChanged;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowEffects.ApplyDarkChrome(this);
        WindowEffects.ApplyRoundedCorners(this);
        WindowEffects.HideFromAltTab(this);
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
    }

    // ---------------------------------------------------------------- configure

    public void Apply(WindowInfo source, AppSettings settings)
    {
        _source = source;
        _region = settings.Region;
        if (_region is { IsEmpty: false } stored && settings.RegionSourceWidth > 0 && settings.RegionSourceHeight > 0)
        {
            _region = RegionMath.Scale(stored, settings.RegionSourceWidth, settings.RegionSourceHeight, source.Width, source.Height);
        }

        _paused = false;
        TitleText.Text = source.Title;

        ApplyScaleMode(settings.ScaleMode);
        Opacity = Math.Clamp(settings.MonitorOpacity, 0.25, 1);
        Topmost = settings.Topmost;
        FrameBorder.BorderThickness = new Thickness(settings.ShowBorder ? 1 : 0);
        FrameBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(102, 0x3F, 0xD2, 0xF5));
        StatsChip.Visibility = settings.ShowBorder ? Visibility.Visible : Visibility.Collapsed;
        ResizeGrip.Visibility = settings.ShowBorder ? Visibility.Visible : Visibility.Collapsed;

        SetClickThrough(settings.ClickThrough);

        _aspect = source.Width > 0 && source.Height > 0
            ? (double)source.Width / source.Height
            : 16.0 / 9.0;
        if (_region is { IsEmpty: false } region) _aspect = (double)region.Width / region.Height;

        RestoreWindowBounds(settings);
        StartCapture(settings);
        UpdateStatusOverlay();
    }

    private void RestoreWindowBounds(AppSettings settings)
    {
        if (settings.HasMonitorBounds && settings.MonitorWidth > 80 && settings.MonitorHeight > 60)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = settings.MonitorLeft;
            Top = settings.MonitorTop;
            Width = settings.MonitorWidth;
            Height = settings.MonitorHeight;
        }
        else
        {
            var work = SystemParameters.WorkArea;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Width = Math.Min(560, work.Width * 0.36);
            Height = Math.Max(160, Width / _aspect);
            Left = work.Right - Width - 32;
            Top = work.Top + 72;
        }
    }

    private void StartCapture(AppSettings settings)
    {
        if (_source is null) return;

        _controller?.Dispose();
        _controller = new CaptureController(Dispatcher);
        _controller.FrameReady += OnFrame;
        _controller.StateChanged += OnStateChanged;
        _controller.SourceSizeChanged += OnSourceSize;
        _controller.BlankDetected += () => App.Toast(L.Get("MsgBlankScreen"));
        _controller.Start(_source, _region, settings.Fps);
        _controller.SetCaptureCursor(settings.CaptureCursor);
        UpdateOutputSize();

        _bitmap = null;
        VideoBrush.ImageSource = null;
        _statsTimer.Start();
    }

    /// <summary>Tells the engine what the video area can actually show, so it can downscale before readback.</summary>
    private void UpdateOutputSize()
    {
        if (_controller is null) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        var width = (int)Math.Round(Root.ActualWidth * dpi.DpiScaleX);
        var height = (int)Math.Round(Root.ActualHeight * dpi.DpiScaleY);
        if (width < 8 || height < 8) return;
        _controller.SetOutputSize(width, height);
    }

    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            _statsTimer.Start();
            if (_controller is null) StartCapture(App.Settings);
            else _controller.Resume();
            UpdateOutputSize();
        }
        else
        {
            _statsTimer.Stop();
            if (App.Settings.StopWhenHidden) StopCapture();
            else _controller?.Pause();
        }
    }

    public void StopCapture()
    {
        _statsTimer.Stop();
        _controller?.Dispose();
        _controller = null;
    }

    // ---------------------------------------------------------------- frames

    private void OnFrame(CapturedFrame frame)
    {
        if (frame.Width <= 0 || frame.Height <= 0) return;

        if (_bitmap is null || _bitmap.PixelWidth != frame.Width || _bitmap.PixelHeight != frame.Height)
        {
            _bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null);
            VideoBrush.ImageSource = _bitmap;
        }

        _bitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), frame.Buffer, frame.Stride, 0);
        _lastFrames++;
        StatusOverlay.Visibility = _paused ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSourceSize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        if (_region is { IsEmpty: false } region && _sourceWidth > 0 && _sourceHeight > 0 && (_sourceWidth != width || _sourceHeight != height))
        {
            _region = RegionMath.Scale(region, _sourceWidth, _sourceHeight, width, height);
            _aspect = (double)_region.Value.Width / _region.Value.Height;
            _controller?.SetRegion(_region);
            App.Settings.SetRegion(_region);
            App.Settings.RegionSourceWidth = width;
            App.Settings.RegionSourceHeight = height;
            SettingsStore.Save(App.Settings);
        }
        else if (_region is null)
        {
            _aspect = (double)width / height;
        }

        _sourceWidth = width;
        _sourceHeight = height;
    }

    private void OnStateChanged(CaptureState state, string? message)
    {
        switch (state)
        {
            case CaptureState.Error:
                ShowStatus(L.Get("CaptureFailed"), message ?? L.Get("MsgUnknownError"), Native.DWMWCP_DEFAULT);
                break;
            case CaptureState.SourceClosed:
                ShowStatus(L.Get("SourceClosed"), L.Get("SourceClosedHint"), Native.DWMWCP_DEFAULT);
                break;
            case CaptureState.Running:
                StatusOverlay.Visibility = _paused ? Visibility.Visible : Visibility.Collapsed;
                if (_controller?.CompatibilityMode == true)
                {
                    StatsChip.Visibility = Visibility.Visible;
                }
                break;
            case CaptureState.Paused:
                ShowStatus(L.Get("Paused"), L.Get("PausedHint"), Native.DWMWCP_DEFAULT);
                break;
            case CaptureState.Idle:
                StatusOverlay.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private void ShowStatus(string title, string detail, int _)
    {
        StatusText.Text = $"{title} · {detail}";
        StatusIcon.Data = Geometry.Parse("M8,1 L8,9 M0.6,8 A7.6,7.6 0 1 0 15.4,8");
        StatusIcon.Stroke = (Brush)FindResource("Warn");
        StatusOverlay.Visibility = Visibility.Visible;
    }

    private void UpdateStats()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastStats).TotalSeconds;
        if (elapsed <= 0) return;

        var fps = (int)Math.Round(_lastFrames / elapsed);
        _lastFrames = 0;
        _lastStats = now;

        FpsText.Text = $"{fps} FPS";
        var compatibility = _controller?.CompatibilityMode == true ? " · " + L.Get("CompatMode") : string.Empty;
        StatsText.Text = _bitmap is null
            ? L.Get("WaitingFrame") + compatibility
            : $"{_bitmap.PixelWidth} × {_bitmap.PixelHeight}{compatibility}";
    }

    // ---------------------------------------------------------------- chrome

    private void ShowToolbar(bool visible)
    {
        if (_chromeVisible == visible) return;
        _chromeVisible = visible;
        var animation = new System.Windows.Media.Animation.DoubleAnimation
        {
            To = visible ? 1 : 0,
            Duration = TimeSpan.FromMilliseconds(visible ? 140 : 220),
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut },
        };
        Toolbar.BeginAnimation(OpacityProperty, animation);
        StatsChip.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation
        {
            To = visible ? 1 : 0,
            Duration = TimeSpan.FromMilliseconds(visible ? 140 : 220),
        });
    }

    private void OnDragHandle(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        try { DragMove(); } catch { }
    }

    private void ApplyScaleMode(string mode)
    {
        VideoBrush.Stretch = mode switch
        {
            "Fill" => Stretch.UniformToFill,
            "Stretch" => Stretch.Fill,
            _ => Stretch.Uniform,
        };
    }

    public void ToggleClickThrough()
    {
        SetClickThrough(!_clickThrough);
        App.Settings.ClickThrough = _clickThrough;
        SettingsStore.Save(App.Settings);
        App.Toast(_clickThrough ? L.Get("MsgClickThroughOn") : L.Get("MsgClickThroughOff"));
    }

    private void SetClickThrough(bool enabled)
    {
        _clickThrough = enabled;
        WindowEffects.SetClickThrough(this, enabled);
        ThroughBadge.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        if (enabled) ShowToolbar(false);
    }

    // ---------------------------------------------------------------- actions

    private void OnTogglePause(object sender, RoutedEventArgs e) => TogglePause();

    public void TogglePause()
    {
        _paused = !_paused;
        if (_paused)
        {
            _controller?.Pause();
            PauseButton.Tag = (Geometry)FindResource("IcoPlay");
            ShowStatus(L.Get("Paused"), L.Get("PausedHint"), Native.DWMWCP_DEFAULT);
        }
        else
        {
            _controller?.Resume();
            PauseButton.Tag = (Geometry)FindResource("IcoPause");
            StatusOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void OnScreenshot(object sender, RoutedEventArgs e) => SaveScreenshot();

    public void SaveScreenshot() => _ = SaveScreenshotAsync();

    private async System.Threading.Tasks.Task SaveScreenshotAsync()
    {
        if (_source is null) return;
        try
        {
            var snapshot = await SnapshotService.GrabAsync(_source);
            if (snapshot is null)
            {
                App.Toast(L.Get("MsgNoFrame"));
                return;
            }

            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PipSentry");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"PipSentry_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(snapshot));
            using (var stream = File.Create(path)) encoder.Save(stream);
            App.Toast(L.Get("MsgShotSaved") + Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            App.Toast(L.Get("MsgShotFailed") + ex.Message);
        }
    }

    private void OnToggleTopmost(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        App.Settings.Topmost = Topmost;
        WindowEffects.SetTopmost(this, Topmost);
        SettingsStore.Save(App.Settings);
    }

    private void OnBackToSettings(object sender, RoutedEventArgs e) => App.ShowShell();

    private void OnCloseMonitor(object sender, RoutedEventArgs e) => App.CloseMonitor();

    private async void OnRedrawRegion(object sender, RoutedEventArgs e)
    {
        if (_source is null) return;
        var snapshot = await SnapshotService.GrabAsync(_source);
        if (snapshot is null)
        {
            App.Toast(L.Get("MsgSnapshotFailed"));
            return;
        }

        var selector = new RegionSelectorWindow(_source, snapshot, _region);
        if (selector.ShowDialog() == true && selector.Result is { IsEmpty: false } region)
        {
            _region = region;
            _aspect = (double)region.Width / region.Height;
            App.Settings.SetRegion(region);
            App.Settings.UseRegion = true;
            SettingsStore.Save(App.Settings);
            _controller?.SetRegion(region);
            if (App.Shell is not null) App.Shell.RefreshFromSettings();
            App.Toast(L.Format("MsgRegionApplied", region.Width, region.Height));
        }
    }

    private void OnContextMenuRequest(object sender, MouseButtonEventArgs e)
    {
        if (_clickThrough) return;

        var menu = new System.Windows.Controls.ContextMenu();
        void Add(string header, Action action)
        {
            var item = new System.Windows.Controls.MenuItem { Header = header };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }

        Add(_paused ? L.Get("MenuResume") : L.Get("MenuPause"), TogglePause);
        Add(L.Get("MenuScreenshot"), async () =>
        {
            if (_source is null) return;
            var snapshot = await SnapshotService.GrabAsync(_source);
            if (snapshot is null) return;
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PipSentry");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"PipSentry_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(snapshot));
            using (var stream = File.Create(path)) encoder.Save(stream);
        });
        menu.Items.Add(new System.Windows.Controls.Separator());
        Add(Topmost ? L.Get("MenuTopmostOff") : L.Get("MenuTopmostOn"), () => OnToggleTopmost(this, new RoutedEventArgs()));
        Add(_clickThrough ? L.Get("MenuClickThroughOff") : L.Get("MenuClickThrough"), () => SetClickThrough(!_clickThrough));
        Add(App.Settings.CaptureCursor ? L.Get("MenuHideCursor") : L.Get("MenuShowCursor"), ToggleCursorCapture);
        Add((App.Settings.StopWhenHidden ? "✓ " : "   ") + L.Get("MenuStopWhenHidden"), ToggleStopWhenHidden);
        menu.Items.Add(new System.Windows.Controls.Separator());
        Add(L.Get("ScaleFit"), () => ApplyScaleMode("Fit"));
        Add(L.Get("ScaleFill"), () => ApplyScaleMode("Fill"));
        Add(L.Get("ScaleStretch"), () => ApplyScaleMode("Stretch"));
        menu.Items.Add(new System.Windows.Controls.Separator());
        Add(L.Get("SnapTL"), () => SnapToCorner("TL"));
        Add(L.Get("SnapTR"), () => SnapToCorner("TR"));
        Add(L.Get("SnapBL"), () => SnapToCorner("BL"));
        Add(L.Get("SnapBR"), () => SnapToCorner("BR"));
        Add(L.Get("Center"), () => SnapToCorner("C"));
        menu.Items.Add(new System.Windows.Controls.Separator());
        Add(L.Get("Size100"), () => ApplySizePreset(1.0));
        Add(L.Get("Size50"), () => ApplySizePreset(0.5));
        Add(L.Get("Size25"), () => ApplySizePreset(0.25));
        Add(L.Get("CoverScreen"), () =>
        {
            var work = GetWorkAreaDip();
            Left = work.Left;
            Top = work.Top;
            Width = work.Width;
            Height = work.Height;
        });
        menu.Items.Add(new System.Windows.Controls.Separator());
        Add(L.Get("BackToSettings"), () => App.ShowShell());
        Add(L.Get("CloseMonitor"), () => App.CloseMonitor());
        menu.PlacementTarget = this;
        menu.IsOpen = true;
        e.Handled = true;
    }

    // ---------------------------------------------------------------- layout helpers

    private Rect GetWorkAreaDip()
    {
        var work = ScreenGeometry.GetMonitorWorkArea(WindowHandle);
        var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (dpi <= 0) dpi = 1;
        return work.Width <= 0
            ? SystemParameters.WorkArea
            : new Rect(work.Left / dpi, work.Top / dpi, work.Width / dpi, work.Height / dpi);
    }

    public void SnapToCorner(string corner)
    {
        var work = GetWorkAreaDip();
        const double margin = 24;
        var width = ActualWidth > 40 ? ActualWidth : Width;
        var height = ActualHeight > 40 ? ActualHeight : Height;

        Left = corner switch
        {
            "TL" or "BL" => work.Left + margin,
            "TR" or "BR" => work.Right - width - margin,
            _ => work.Left + ((work.Width - width) / 2),
        };

        Top = corner switch
        {
            "TL" or "TR" => work.Top + margin,
            "BL" or "BR" => work.Bottom - height - margin,
            _ => work.Top + ((work.Height - height) / 2),
        };
    }

    public void ApplySizePreset(double fraction)
    {
        var work = GetWorkAreaDip();
        var width = work.Width * fraction;
        var height = width / _aspect;
        var maxHeight = work.Height * 0.92;
        if (height > maxHeight)
        {
            height = maxHeight;
            width = height * _aspect;
        }

        Width = Math.Max(MinWidth, width);
        Height = Math.Max(MinHeight, height);
        SnapToCorner("C");
    }

    public void ToggleStopWhenHidden()
    {
        App.Settings.StopWhenHidden = !App.Settings.StopWhenHidden;
        SettingsStore.Save(App.Settings);
        App.Toast(App.Settings.StopWhenHidden ? L.Get("MenuStopWhenHidden") : L.Get("MenuStopWhenHiddenOff"));
    }

    public void ToggleCursorCapture()
    {
        App.Settings.CaptureCursor = !App.Settings.CaptureCursor;
        _controller?.SetCaptureCursor(App.Settings.CaptureCursor);
        SettingsStore.Save(App.Settings);
        App.Toast(App.Settings.CaptureCursor ? L.Get("MsgCursorOn") : L.Get("MsgCursorOff"));
    }

    // ---------------------------------------------------------------- sizing

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_SIZING = 0x0214;
        const int WM_MOVING = 0x0216;

        if (msg == WM_MOVING && !_clickThrough)
        {
            var moved = System.Runtime.InteropServices.Marshal.PtrToStructure<Native.RECT>(lParam);
            var work = ScreenGeometry.GetMonitorWorkArea(hwnd);
            var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
            var threshold = (int)(20 * (dpi <= 0 ? 1 : dpi));
            var width = moved.Width;
            var height = moved.Height;

            if (Math.Abs(moved.Left - work.Left) <= threshold)
            {
                moved.Left = work.Left;
                moved.Right = moved.Left + width;
            }
            else if (Math.Abs(moved.Right - work.Right) <= threshold)
            {
                moved.Right = work.Right;
                moved.Left = moved.Right - width;
            }

            if (Math.Abs(moved.Top - work.Top) <= threshold)
            {
                moved.Top = work.Top;
                moved.Bottom = moved.Top + height;
            }
            else if (Math.Abs(moved.Bottom - work.Bottom) <= threshold)
            {
                moved.Bottom = work.Bottom;
                moved.Top = moved.Bottom - height;
            }

            System.Runtime.InteropServices.Marshal.StructureToPtr(moved, lParam, false);
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == WM_SIZING && App.Settings.LockAspect && !_clickThrough)
        {
            var rect = System.Runtime.InteropServices.Marshal.PtrToStructure<Native.RECT>(lParam);
            var edge = wParam.ToInt32();
            var width = rect.Width;
            var height = rect.Height;
            var adjustedHeight = (int)Math.Round(width / _aspect);
            var adjustedWidth = (int)Math.Round(height * _aspect);

            switch (edge)
            {
                case 1:
                case 2:
                    rect.Top = rect.Bottom - adjustedHeight;
                    break;
                case 4:
                case 5:
                    rect.Left = rect.Right - adjustedWidth;
                    break;
                case 7:
                case 8:
                    rect.Bottom = rect.Top + adjustedHeight;
                    break;
                default:
                    if (Math.Abs(width / (double)height - _aspect) > 0.01)
                    {
                        rect.Right = rect.Left + adjustedWidth;
                    }
                    break;
            }

            System.Runtime.InteropServices.Marshal.StructureToPtr(rect, lParam, false);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void UpdateStatusOverlay()
    {
        StatusOverlay.Visibility = _paused ? Visibility.Visible : Visibility.Collapsed;
        if (!_paused)
        {
            StatusIcon.Data = (Geometry)FindResource("IcoPause");
        }
    }

    private void OnClosedInternal(object? sender, EventArgs e)
    {
        _statsTimer.Stop();
        StopCapture();
        if (_source is not null && App.Settings.HasMonitorBounds)
        {
            App.Settings.MonitorLeft = Left;
            App.Settings.MonitorTop = Top;
            App.Settings.MonitorWidth = Width;
            App.Settings.MonitorHeight = Height;
            SettingsStore.Save(App.Settings);
        }
    }

    private IntPtr WindowHandle => new WindowInteropHelper(this).Handle;

    public void SaveBounds()
    {
        App.Settings.HasMonitorBounds = true;
        App.Settings.MonitorLeft = Left;
        App.Settings.MonitorTop = Top;
        App.Settings.MonitorWidth = Width;
        App.Settings.MonitorHeight = Height;
        SettingsStore.Save(App.Settings);
    }
}
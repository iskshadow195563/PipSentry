using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PipSentry.Platform;
using PipSentry.Capture;
using PipSentry.Interop;
using PipSentry.Services;

namespace PipSentry.Views;

public partial class ShellWindow : Window
{
    private readonly CaptureController _preview;
    private readonly DispatcherTimer _toastTimer;
    private WriteableBitmap? _previewBitmap;
    private List<WindowInfo> _allSources = new();
    private WindowInfo? _selected;
    private PixelRect? _pendingRegion;
    private bool _suppressUiEvents;
    private bool _applyingProfile;
    private int _profileCursor = -1;

    public ShellWindow()
    {
        InitializeComponent();

        _preview = new CaptureController(Dispatcher);
        _preview.FrameReady += OnPreviewFrame;
        _preview.StateChanged += OnPreviewState;
        _preview.SourceSizeChanged += OnPreviewSourceSizeChanged;
        _preview.BlankDetected += () => ShowToast(L.Get("MsgBlankScreen"));

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            Toast.Visibility = Visibility.Collapsed;
        };

        SourceInitialized += (_, _) =>
        {
            WindowEffects.ApplyDarkChrome(this, ThemeManager.IsDark);
            WindowEffects.ApplyRoundedCorners(this);
        };
        RefreshThemeIcon();

        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
        SizeChanged += (_, _) => UpdatePreviewOutputSize();
        IsVisibleChanged += OnShellVisibilityChanged;
        StateChanged += OnShellStateChanged;
    }

    private void OnShellVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        {
            _preview.Stop();
            AppLog.Write("preview stop: shell hidden");
        }
    }

    private void OnShellStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            _preview.Stop();
            AppLog.Write("preview stop: minimized");
        }
        else if (_preview.State == CaptureState.Idle && _selected is not null && IsVisible)
        {
            StartPreview();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplySettingsToUi();
        RefreshSources(App.Settings.LastSourceKey);
    }

    // ---------------------------------------------------------------- settings

    private void ApplySettingsToUi()
    {
        var settings = App.Settings;
        _suppressUiEvents = true;

        SwitchKeepAlive.IsChecked = settings.KeepAlive;
        SwitchPreventSleep.IsChecked = settings.PreventSleep;
        SwitchOptimize.IsChecked = settings.OptimizeTargetApp;
        SwitchNudge.IsChecked = settings.NudgeTarget;
        SwitchTopmost.IsChecked = settings.Topmost;
        SwitchClickThrough.IsChecked = settings.ClickThrough;
        SwitchBorder.IsChecked = settings.ShowBorder;
        SwitchLockAspect.IsChecked = settings.LockAspect;
        SwitchCursor.IsChecked = settings.CaptureCursor;

        (settings.Fps switch
        {
            15 => Fps15,
            60 => Fps60,
            120 => Fps120,
            _ => Fps30,
        }).IsChecked = true;

        (settings.ScaleMode switch
        {
            "Fill" => ScaleFill,
            "Stretch" => ScaleStretch,
            _ => ScaleFit,
        }).IsChecked = true;

        OpacitySlider.Value = Math.Clamp(settings.MonitorOpacity, 0.25, 1);
        OpacityText.Text = $"{OpacitySlider.Value * 100:0}%";

        if (settings.UseRegion && settings.Region is { } region)
        {
            _pendingRegion = region;
            ModeRegion.IsChecked = true;
        }
        else
        {
            _pendingRegion = null;
            ModeFull.IsChecked = true;
        }

        UpdateRegionUi();
        _suppressUiEvents = false;
    }

    private void SaveUiToSettings()
    {
        var settings = App.Settings;
        settings.KeepAlive = SwitchKeepAlive.IsChecked == true;
        settings.PreventSleep = SwitchPreventSleep.IsChecked == true;
        settings.OptimizeTargetApp = SwitchOptimize.IsChecked == true;
        settings.NudgeTarget = SwitchNudge.IsChecked == true;
        settings.Topmost = SwitchTopmost.IsChecked == true;
        settings.ClickThrough = SwitchClickThrough.IsChecked == true;
        settings.ShowBorder = SwitchBorder.IsChecked == true;
        settings.LockAspect = SwitchLockAspect.IsChecked == true;
        settings.CaptureCursor = SwitchCursor.IsChecked == true;

        settings.Fps = SelectedFps;
        settings.ScaleMode = ScaleFill.IsChecked == true ? "Fill" : ScaleStretch.IsChecked == true ? "Stretch" : "Fit";
        settings.MonitorOpacity = Math.Round(OpacitySlider.Value, 2);
        settings.SetRegion(ModeRegion.IsChecked == true ? _pendingRegion : null);

        SettingsStore.Save(settings);
    }

    private void OnFpsChanged(object sender, RoutedEventArgs e)
    {
        if (MetaFps is null || Fps15 is null || Fps30 is null || Fps60 is null || Fps120 is null) return;
        MetaFps.Text = L.Format("MsgFpsTarget", SelectedFps);
    }

    private int SelectedFps =>
        Fps15.IsChecked == true ? 15 :
        Fps60.IsChecked == true ? 60 :
        Fps120.IsChecked == true ? 120 : 30;

    // ---------------------------------------------------------------- sources

    private void RefreshSources(string? selectKey = null)
    {
        var previousKey = selectKey ?? (_selected?.Key ?? App.Settings.LastSourceKey);
        _allSources = WindowCatalog.Enumerate();
        ApplyFilter();

        if (!string.IsNullOrEmpty(previousKey))
        {
            var match = _allSources.FirstOrDefault(w => w.Key == previousKey);
            if (match is not null)
            {
                SourceList.SelectedItem = match;
                return;
            }
        }

        if (SourceList.Items.Count > 0 && SourceList.SelectedItem is null)
        {
            SourceList.SelectedIndex = 0;
        }
        else if (SourceList.Items.Count == 0)
        {
            _preview.Stop();
            _selected = null;
            PreviewEmpty.Visibility = Visibility.Visible;
            PreviewBadge.Visibility = Visibility.Collapsed;
            PreviewSizeBadge.Visibility = Visibility.Collapsed;
            PreviewBrush.ImageSource = null;
        }
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text?.Trim() ?? string.Empty;
        IEnumerable<WindowInfo> filtered = _allSources;
        if (query.Length > 0)
        {
            filtered = _allSources.Where(w =>
                w.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                w.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();
        SourceList.ItemsSource = list;
        SourceCount.Text = list.Count.ToString();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        RefreshSources();
        ShowToast(L.Get("MsgRefreshed"));
    }

    private void OnSourceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceList.SelectedItem is not WindowInfo info) return;
        if (_selected?.Key == info.Key) return;

        _selected = info;
        // Keep the persisted region when restoring the same source, otherwise start fresh.
        if (_applyingProfile)
        {
            UpdateRegionUi();
        }
        else
        {
            _pendingRegion = null;
            if (info.Key == App.Settings.LastSourceKey && App.Settings.UseRegion && App.Settings.Region is { } saved)
            {
                var sourceWidth = App.Settings.RegionSourceWidth > 0 ? App.Settings.RegionSourceWidth : info.Width;
                var sourceHeight = App.Settings.RegionSourceHeight > 0 ? App.Settings.RegionSourceHeight : info.Height;
                _pendingRegion = RegionMath.Scale(saved, sourceWidth, sourceHeight, info.Width, info.Height);
            }
        }
        UpdateRegionUi();

        SelectedTitle.Text = info.Title;
        SelectedMeta.Text = info.IsDisplay
            ? L.Format("MsgDisplayLine", info.DisplayIndex, info.Width, info.Height)
            : $"{info.ProcessName} · PID {info.ProcessId} · {info.Width} × {info.Height}";
        MetaTitle.Text = info.Title;
        MetaResolution.Text = $"{info.Width} × {info.Height}";
        MetaFps.Text = L.Format("MsgFpsTarget", SelectedFps);
        MetaMode.Text = "Windows Graphics Capture";
        MetaHintText.Text = info.IsMinimized
            ? L.Get("TipMinimized")
            : L.Get("TipBody");

        RegionButton.IsEnabled = true;
        StartButton.IsEnabled = true;
        StatusLine.Text = info.IsMinimized ? L.Get("MsgMinimizedWarn") : L.Get("MsgPreparing");

        StartPreview();
    }

    private void StartPreview()
    {
        if (_selected is null) return;

        var region = ModeRegion.IsChecked == true ? _pendingRegion : null;
        _previewBitmap = null;
        PreviewBrush.ImageSource = null;
        PreviewEmpty.Visibility = Visibility.Visible;

        try
        {
            _preview.Start(_selected, region, 15);
            UpdatePreviewOutputSize();
            AppLog.Write($"preview start: {_selected.Title} @15fps");
            PreviewBadge.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            StatusLine.Text = L.Get("MsgPreviewFailed") + ex.Message;
        }
    }

    private void UpdatePreviewOutputSize()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var width = (int)Math.Round(PreviewSurface.ActualWidth * dpi.DpiScaleX);
        var height = (int)Math.Round(PreviewSurface.ActualHeight * dpi.DpiScaleY);
        if (width < 8 || height < 8) return;
        _preview.SetOutputSize(width, height);
        AppLog.Write($"preview output cap: {width}x{height}");
    }

    private void OnPreviewFrame(CapturedFrame frame)
    {
        if (frame.Width <= 0 || frame.Height <= 0) return;

        if (_previewBitmap is null ||
            _previewBitmap.PixelWidth != frame.Width ||
            _previewBitmap.PixelHeight != frame.Height)
        {
            _previewBitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null);
            PreviewBrush.ImageSource = _previewBitmap;
        }

        _previewBitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), frame.Buffer, frame.Stride, 0);
        PreviewEmpty.Visibility = Visibility.Collapsed;
        PreviewBadge.Visibility = Visibility.Visible;
        PreviewSizeBadge.Visibility = Visibility.Visible;
        PreviewSizeText.Text = $"{frame.Width} × {frame.Height}";
    }

    private void OnPreviewState(CaptureState state, string? message)
    {
        switch (state)
        {
            case CaptureState.Running:
                StatusLine.Text = message ?? L.Get("Previewing");
                break;
            case CaptureState.Error:
            case CaptureState.SourceClosed:
                StatusLine.Text = message ?? L.Get("CaptureFailed");
                PreviewBadge.Visibility = Visibility.Collapsed;
                break;
            case CaptureState.Idle:
                PreviewBadge.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private void OnPreviewSourceSizeChanged(int width, int height)
    {
        PreviewSizeText.Text = $"{width} × {height}";
        MetaResolution.Text = $"{width} × {height}";
    }

    // ---------------------------------------------------------------- region

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressUiEvents) return;
        if (RegionButton is null || RegionLabel is null) return; // fires while XAML is still loading
        var regionMode = ModeRegion.IsChecked == true;
        RegionButton.IsEnabled = _selected is not null && regionMode;
        if (!regionMode) _pendingRegion = null;
        UpdateRegionUi();
        if (_selected is not null) StartPreview();
    }

    private async void OnPickRegion(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            ShowToast(L.Get("MsgPickFirst"));
            return;
        }

        StatusLine.Text = L.Get("MsgGrabbing");
        var snapshot = await SnapshotService.GrabAsync(_selected);
        if (snapshot is null)
        {
            StatusLine.Text = L.Get("MsgSnapshotFailed2");
            ShowToast(L.Get("MsgSnapshotFailed"));
            return;
        }

        var selector = new RegionSelectorWindow(_selected, snapshot, _pendingRegion);
        var accepted = selector.ShowDialog();
        if (accepted == true && selector.Result is { IsEmpty: false } region)
        {
            _pendingRegion = region;
            App.Settings.RegionSourceWidth = snapshot.PixelWidth;
            App.Settings.RegionSourceHeight = snapshot.PixelHeight;
            ModeRegion.IsChecked = true;
            UpdateRegionUi();
            StartPreview();
            ShowToast(L.Format("MsgRegionSet", region.Width, region.Height));
        }
        else
        {
            StatusLine.Text = L.Get("MsgRegionCancel");
        }
    }

    private void UpdateRegionUi()
    {
        var enabled = ModeRegion.IsChecked == true;
        RegionButton.IsEnabled = enabled && _selected is not null;
        RegionLabel.Text = _pendingRegion is { IsEmpty: false } region
            ? $"{region.X}, {region.Y} · {region.Width} × {region.Height}"
            : enabled ? L.Get("ScopeNone") : L.Get("ScopeFullValue");
    }

    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityText is not null) OpacityText.Text = $"{e.NewValue * 100:0}%";
    }

    // ---------------------------------------------------------------- actions

    private void OnStartMonitoring(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            ShowToast(L.Get("MsgPickFirst"));
            return;
        }

        SaveUiToSettings();
        App.StartMonitoring(_selected);
    }

    private void OnSourceDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SourceList.SelectedItem is WindowInfo) OnStartMonitoring(sender, e);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            RefreshSources();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && Keyboard.FocusedElement is not TextBox)
        {
            OnStartMonitoring(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SearchBox.Clear();
        }
    }

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        try { DragMove(); } catch { }
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnToggleTheme(object sender, RoutedEventArgs e) => App.ToggleTheme();

    private void OnHotkeysClick(object sender, RoutedEventArgs e)
    {
        var window = new HotkeyWindow { Owner = this };
        window.ShowDialog();
    }

    private void OnProfileClick(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();

        var save = new MenuItem { Header = L.Get("ProfileSave") };
        save.Click += (_, _) => SaveCurrentProfile();
        menu.Items.Add(save);
        menu.Items.Add(new Separator());

        if (App.Settings.Profiles.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = L.Get("ProfileEmpty"), IsEnabled = false });
        }
        else
        {
            foreach (var profile in App.Settings.Profiles.ToList())
            {
                var item = new MenuItem { Header = $"{profile.Name}  ·  {profile.SourceTitle}" };
                var captured = profile;
                item.Click += (_, _) => ApplyProfile(captured);
                menu.Items.Add(item);
            }

            menu.Items.Add(new Separator());
            var clear = new MenuItem { Header = L.Get("ProfileDeleteAll") };
            clear.Click += (_, _) =>
            {
                App.Settings.Profiles.Clear();
                SettingsStore.Save(App.Settings);
                ShowToast(L.Get("ProfileDeleteAll"));
            };
            menu.Items.Add(clear);
        }

        menu.PlacementTarget = ProfileButton;
        menu.IsOpen = true;
    }

    private void SaveCurrentProfile()
    {
        if (_selected is null)
        {
            ShowToast(L.Get("MsgPickFirst"));
            return;
        }

        SaveUiToSettings();
        var name = _selected.Title.Trim();
        if (name.Length > 28) name = name[..28];
        var existing = App.Settings.Profiles.FirstOrDefault(p => p.Name == name);
        if (existing is not null) App.Settings.Profiles.Remove(existing);

        App.Settings.Profiles.Insert(0, new MonitorProfile
        {
            Name = name,
            SourceKey = _selected.Key,
            SourceTitle = _selected.Title,
            UseRegion = App.Settings.UseRegion,
            RegionX = App.Settings.RegionX,
            RegionY = App.Settings.RegionY,
            RegionWidth = App.Settings.RegionWidth,
            RegionHeight = App.Settings.RegionHeight,
            RegionSourceWidth = App.Settings.RegionSourceWidth,
            RegionSourceHeight = App.Settings.RegionSourceHeight,
            Fps = App.Settings.Fps,
            ScaleMode = App.Settings.ScaleMode,
        });

        SettingsStore.Save(App.Settings);
        ShowToast(name);
    }

    public void CycleProfile()
    {
        var profiles = App.Settings.Profiles;
        if (profiles.Count == 0)
        {
            ShowToast(L.Get("ProfileEmpty"));
            return;
        }

        _profileCursor = (_profileCursor + 1) % profiles.Count;
        var profile = profiles[_profileCursor];
        ApplyProfile(profile);
        ShowToast(profile.Name);
    }

    public void ApplyProfile(MonitorProfile profile)
    {
        var match = _allSources.FirstOrDefault(w => w.Key == profile.SourceKey);
        if (match is null)
        {
            match = _allSources.FirstOrDefault(w => w.Title == profile.SourceTitle);
            if (match is not null) ShowToast(L.Get("ProfileMissing"));
        }

        if (match is null)
        {
            ShowToast(L.Get("MsgPickFirst"));
            return;
        }

        _suppressUiEvents = true;
        (profile.Fps switch { 15 => Fps15, 60 => Fps60, 120 => Fps120, _ => Fps30 }).IsChecked = true;
        (profile.ScaleMode switch { "Fill" => ScaleFill, "Stretch" => ScaleStretch, _ => ScaleFit }).IsChecked = true;
        ModeRegion.IsChecked = profile.UseRegion;
        ModeFull.IsChecked = !profile.UseRegion;
        _suppressUiEvents = false;

        _pendingRegion = profile.UseRegion && profile.RegionWidth > 0
            ? RegionMath.Scale(
                new PixelRect(profile.RegionX, profile.RegionY, profile.RegionWidth, profile.RegionHeight),
                profile.RegionSourceWidth > 0 ? profile.RegionSourceWidth : match.Width,
                profile.RegionSourceHeight > 0 ? profile.RegionSourceHeight : match.Height,
                match.Width, match.Height)
            : null;

        UpdateRegionUi();
        _applyingProfile = true;
        try
        {
            var same = _selected?.Key == match.Key;
            SourceList.SelectedItem = match;
            SourceList.ScrollIntoView(match);
            if (same && _selected is not null)
            {
                SelectedTitle.Text = match.Title;
                StartPreview();
            }
        }
        finally
        {
            _applyingProfile = false;
        }
    }

    private void OnLanguageClick(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        foreach (var culture in Loc.Available)
        {
            var label = L.Get(Loc.KeyFor(culture));
            var item = new MenuItem
            {
                Header = (Loc.Current == culture ? "✓  " : "     ") + label,
            };
            var code = culture;
            item.Click += (_, _) => App.SetLanguage(code);
            menu.Items.Add(item);
        }

        menu.PlacementTarget = LanguageButton;
        menu.IsOpen = true;
    }

    public void RefreshLanguageUi()
    {
        if (_selected is { } info)
        {
            MetaResolution.Text = $"{info.Width} × {info.Height}";
            MetaFps.Text = L.Format("MsgFpsTarget", SelectedFps);
            MetaHintText.Text = info.IsMinimized ? L.Get("TipMinimized") : L.Get("TipBody");
        }

        RefreshSources(_selected?.Key);
        UpdateRegionUi();
    }

    public void RefreshThemeIcon()
    {
        if (ThemeButton is null) return;
        ThemeButton.Tag = (Geometry)FindResource(ThemeManager.IsDark ? "IcoSun" : "IcoMoon");
        ThemeButton.ToolTip = ThemeManager.IsDark ? L.Get("ThemeTipLight") : L.Get("ThemeTipDark");
    }

    public void ApplyChromeForTheme() => WindowEffects.ApplyDarkChrome(this, ThemeManager.IsDark);

    /// <summary>Swaps the palette behind a fading snapshot instead of a hard cut.</summary>
    public void RunThemeTransition(Action apply)
    {
        try
        {
            var root = (FrameworkElement)Content;
            var dpi = VisualTreeHelper.GetDpi(this);
            var width = (int)Math.Round(root.ActualWidth * dpi.DpiScaleX);
            var height = (int)Math.Round(root.ActualHeight * dpi.DpiScaleY);

            if (width < 64 || height < 64 || !IsVisible)
            {
                apply();
                return;
            }

            var snapshot = new RenderTargetBitmap(width, height, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
            snapshot.Render(root);
            snapshot.Freeze();

            ThemeOverlay.BeginAnimation(OpacityProperty, null);
            ThemeOverlay.Source = snapshot;
            ThemeOverlay.Opacity = 1;
            ThemeOverlay.Visibility = Visibility.Visible;

            apply();

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                var fade = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(280)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                };
                fade.Completed += (_, _) =>
                {
                    ThemeOverlay.Visibility = Visibility.Collapsed;
                    ThemeOverlay.Source = null;
                };
                ThemeOverlay.BeginAnimation(OpacityProperty, fade);
            }));
        }
        catch
        {
            apply();
            ThemeOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void OnCloseWindow(object sender, RoutedEventArgs e) => App.ShutdownApp();

    public void ShowToast(string message)
    {
        ToastText.Text = message;
        Toast.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    public void RefreshFromSettings()
    {
        ApplySettingsToUi();
        if (_selected is not null) StartPreview();
    }

    protected override void OnClosed(EventArgs e)
    {
        _preview.Dispose();
        base.OnClosed(e);
    }
}
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PipSentry.Platform;
using PipSentry.Capture;
using PipSentry.Interop;

namespace PipSentry.Views;

public partial class RegionSelectorWindow : Window
{
    private readonly BitmapSource _snapshot;
    private readonly WindowInfo _source;
    private readonly PixelRect? _initialRegion;
    private Point _dragOrigin;
    private Rect _selection;
    private bool _dragging;
    private bool _moving;
    private Point _moveGrab;
    private Rect _moveStart;

    public PixelRect? Result { get; private set; }

    public RegionSelectorWindow(WindowInfo source, BitmapSource snapshot, PixelRect? initialRegion)
    {
        InitializeComponent();
        _source = source;
        _snapshot = snapshot;
        _initialRegion = initialRegion;

        SnapshotBrush.ImageSource = snapshot;
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
        Host.MouseLeftButtonDown += OnMouseDown;
        Host.MouseMove += OnMouseMove;
        Host.MouseLeftButtonUp += OnMouseUp;
    }

    private Native.RECT TargetRect =>
        _source.IsDisplay
            ? ScreenGeometry.GetMonitorBounds(_source.Monitor)
            : ScreenGeometry.GetClientScreenRect(_source.Handle);

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var rect = TargetRect;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            var work = SystemParameters.VirtualScreenWidth;
            rect = new Native.RECT
            {
                Left = 0,
                Top = 0,
                Right = (int)work,
                Bottom = (int)SystemParameters.VirtualScreenHeight,
            };
        }

        var handle = new WindowInteropHelper(this).Handle;
        WindowEffects.ApplyDarkChrome(this);
        Native.SetWindowPos(handle, Native.HWND_TOPMOST, rect.Left, rect.Top, rect.Width, rect.Height,
            Native.SWP_SHOWWINDOW | Native.SWP_NOACTIVATE);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Activate();
        Focus();
        if (_initialRegion is { IsEmpty: false } region)
        {
            var scaleX = ActualWidth <= 0 ? 1 : ActualWidth / _snapshot.PixelWidth;
            var scaleY = ActualHeight <= 0 ? 1 : ActualHeight / _snapshot.PixelHeight;
            SetSelection(new Rect(region.X * scaleX, region.Y * scaleY, region.Width * scaleX, region.Height * scaleY));
        }

        RedrawOverlay();
    }

    // ---------------------------------------------------------------- input

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var point = e.GetPosition(Host);

        if (e.OriginalSource is DependencyObject source && IsWithin(source, HintBar))
        {
            return;
        }

        if (_selection.Contains(point) && AcceptButton.IsEnabled)
        {
            _moving = true;
            _moveGrab = point;
            _moveStart = _selection;
        }
        else
        {
            _dragging = true;
            _dragOrigin = point;
            SetSelection(new Rect(point, point));
        }

        Host.CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging && !_moving) return;
        var point = e.GetPosition(Host);

        if (_moving)
        {
            var dx = point.X - _moveGrab.X;
            var dy = point.Y - _moveGrab.Y;
            var moved = new Rect(_moveStart.X + dx, _moveStart.Y + dy, _moveStart.Width, _moveStart.Height);
            moved = ClampToHost(moved);
            SetSelection(moved);
        }
        else
        {
            var x = Math.Min(_dragOrigin.X, point.X);
            var y = Math.Min(_dragOrigin.Y, point.Y);
            var width = Math.Abs(point.X - _dragOrigin.X);
            var height = Math.Abs(point.Y - _dragOrigin.Y);
            SetSelection(new Rect(x, y, width, height));
        }

        RedrawOverlay();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging && !_moving) return;
        _dragging = false;
        _moving = false;
        Host.ReleaseMouseCapture();
        RedrawOverlay();
    }

    private static bool IsWithin(DependencyObject? node, DependencyObject parent)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, parent)) return true;
            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            return;
        }

        if (e.Key == Key.Enter)
        {
            OnAccept(this, new RoutedEventArgs());
            return;
        }

        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        var dx = e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0;
        var dy = e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0;
        if (dx != 0 || dy != 0)
        {
            SetSelection(ClampToHost(new Rect(_selection.X + dx, _selection.Y + dy, _selection.Width, _selection.Height)));
            RedrawOverlay();
            e.Handled = true;
        }
    }

    private Rect ClampToHost(Rect rect)
    {
        var x = Math.Clamp(rect.X, 0, Math.Max(0, ActualWidth - rect.Width));
        var y = Math.Clamp(rect.Y, 0, Math.Max(0, ActualHeight - rect.Height));
        return new Rect(x, y, rect.Width, rect.Height);
    }

    private void SetSelection(Rect rect)
    {
        _selection = new Rect(
            new Point(Math.Round(rect.X), Math.Round(rect.Y)),
            new Point(Math.Round(rect.Right), Math.Round(rect.Bottom)));

        AcceptButton.IsEnabled = _selection.Width >= 8 && _selection.Height >= 8;
    }

    private void RedrawOverlay()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
        if (_selection.Width > 0 && _selection.Height > 0)
        {
            group.Children.Add(new RectangleGeometry(_selection));
        }

        DimPath.Data = group;

        SelectionBox.Visibility = _selection.Width > 1 ? Visibility.Visible : Visibility.Collapsed;
        SelectionBox.Margin = new Thickness(_selection.X, _selection.Y, 0, 0);
        SelectionBox.HorizontalAlignment = HorizontalAlignment.Left;
        SelectionBox.VerticalAlignment = VerticalAlignment.Top;
        SelectionBox.Width = _selection.Width;
        SelectionBox.Height = _selection.Height;

        HandleLayer.Children.Clear();
        if (_selection.Width > 12 && _selection.Height > 12)
        {
            foreach (var corner in new[]
            {
                new Point(_selection.Left, _selection.Top),
                new Point(_selection.Right, _selection.Top),
                new Point(_selection.Left, _selection.Bottom),
                new Point(_selection.Right, _selection.Bottom),
            })
            {
                var dot = new Rectangle
                {
                    Width = 6,
                    Height = 6,
                    Fill = (Brush)FindResource("Accent"),
                    RadiusX = 2,
                    RadiusY = 2,
                };
                Canvas.SetLeft(dot, corner.X - 3);
                Canvas.SetTop(dot, corner.Y - 3);
                HandleLayer.Children.Add(dot);
            }
        }

        var pixelWidth = (int)Math.Round(_selection.Width * (_snapshot.PixelWidth / ActualWidth));
        var pixelHeight = (int)Math.Round(_selection.Height * (_snapshot.PixelHeight / ActualHeight));
        SizeTipText.Text = $"{pixelWidth} × {pixelHeight}";
        SizeTip.Visibility = _selection.Width > 8 ? Visibility.Visible : Visibility.Collapsed;
        SizeTip.HorizontalAlignment = HorizontalAlignment.Left;
        SizeTip.VerticalAlignment = VerticalAlignment.Top;
        SizeTip.Margin = new Thickness(0);
        SizeTip.RenderTransform = new TranslateTransform(
            Math.Min(Math.Max(0, _selection.Left), Math.Max(0, ActualWidth - 130)),
            Math.Min(Math.Max(0, _selection.Bottom + 10), Math.Max(0, ActualHeight - 40)));
    }

    // ---------------------------------------------------------------- actions

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        if (_selection.Width < 8 || _selection.Height < 8 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var scaleX = _snapshot.PixelWidth / ActualWidth;
        var scaleY = _snapshot.PixelHeight / ActualHeight;
        var region = new PixelRect(
            (int)Math.Round(_selection.X * scaleX),
            (int)Math.Round(_selection.Y * scaleY),
            Math.Max(1, (int)Math.Round(_selection.Width * scaleX)),
            Math.Max(1, (int)Math.Round(_selection.Height * scaleY))).Clamp(_snapshot.PixelWidth, _snapshot.PixelHeight);

        Result = region;
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
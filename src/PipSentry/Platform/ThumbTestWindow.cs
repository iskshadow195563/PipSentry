using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace PipSentry.Platform;

/// <summary>Throwaway window that shows a DWM thumbnail; used by --thumbtest.</summary>
public sealed class ThumbTestWindow : Window
{
    private const int TnpRectDestination = 0x1;
    private const int TnpRectSource = 0x2;
    private const int TnpOpacity = 0x4;
    private const int TnpVisible = 0x8;
    private const int TnpSourceClientAreaOnly = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ThumbnailProperties
    {
        public int Flags;
        public Rect Destination;
        public Rect Source;
        public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool SourceClientAreaOnly;
    }

    [DllImport("dwmapi.dll")] private static extern int DwmRegisterThumbnail(IntPtr destination, IntPtr source, out IntPtr thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail, ref ThumbnailProperties properties);

    private readonly IntPtr _source;
    private IntPtr _thumbnail;

    public IntPtr Handle { get; private set; }

    public void FillClient()
    {
        if (_thumbnail == IntPtr.Zero) return;
        PipSentry.Interop.Native.GetClientRect(Handle, out var client);
        var props = new ThumbnailProperties
        {
            Flags = TnpRectDestination | TnpSourceClientAreaOnly | TnpVisible | TnpOpacity,
            Destination = new Rect { Left = 0, Top = 0, Right = client.Right, Bottom = client.Bottom },
            Opacity = 255,
            Visible = true,
            SourceClientAreaOnly = true,
        };
        DwmUpdateThumbnailProperties(_thumbnail, ref props);
    }

    public bool Registered { get; private set; }

    public ThumbTestWindow(IntPtr source)
    {
        _source = source;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        Width = 520;
        Height = 300;
        Left = 260;
        Top = 220;
        Content = new Border
        {
            Background = Brushes.Transparent,
            Child = new TextBlock
            {
                Text = "DWM thumbnail test",
                Foreground = Brushes.DeepPink,
                FontSize = 20,
                Margin = new Thickness(12),
            },
        };

        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            Handle = handle;
            var hr = DwmRegisterThumbnail(handle, _source, out _thumbnail);
            if (hr != 0 || _thumbnail == IntPtr.Zero) return;

            var dpi = VisualTreeHelper.GetDpi(this);
            var props = new ThumbnailProperties
            {
                Flags = TnpRectDestination | TnpRectSource | TnpOpacity | TnpVisible | TnpSourceClientAreaOnly,
                Destination = new Rect
                {
                    Left = 0,
                    Top = 0,
                    Right = (int)(Width * dpi.DpiScaleX),
                    Bottom = (int)(Height * dpi.DpiScaleY),
                },
                Opacity = 255,
                Visible = true,
                SourceClientAreaOnly = true,
            };
            DwmUpdateThumbnailProperties(_thumbnail, ref props);
            Registered = true;
        };

        Closed += (_, _) =>
        {
            if (_thumbnail != IntPtr.Zero)
            {
                DwmUnregisterThumbnail(_thumbnail);
                _thumbnail = IntPtr.Zero;
            }
        };
    }
}
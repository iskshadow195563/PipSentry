using System;
using System.Windows;
using PipSentry.Interop;

namespace PipSentry.Platform;

internal static class ScreenGeometry
{
    public static Native.RECT GetClientScreenRect(IntPtr hwnd)
    {
        var rect = new Native.RECT();
        if (hwnd == IntPtr.Zero || !Native.GetClientRect(hwnd, out rect)) return rect;
        var origin = new Native.POINT(rect.Left, rect.Top);
        Native.ClientToScreen(hwnd, ref origin);
        return new Native.RECT
        {
            Left = origin.X,
            Top = origin.Y,
            Right = origin.X + rect.Width,
            Bottom = origin.Y + rect.Height,
        };
    }

    public static Native.RECT GetMonitorBounds(IntPtr monitor)
    {
        var info = new Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFO>() };
        if (monitor == IntPtr.Zero || !Native.GetMonitorInfoW(monitor, ref info)) return new Native.RECT();
        return info.rcMonitor;
    }

    public static Native.RECT GetMonitorWorkArea(IntPtr hwnd)
    {
        var monitor = Native.MonitorFromWindow(hwnd, Native.MONITOR_DEFAULTTONEAREST);
        var info = new Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFO>() };
        if (!Native.GetMonitorInfoW(monitor, ref info)) return new Native.RECT();
        return info.rcWork;
    }

    public static double GetScale(IntPtr hwnd)
    {
        var dpi = Native.GetDpiForWindow(hwnd);
        return dpi <= 0 ? 1.0 : dpi / 96.0;
    }

    public static Rect PixelsToDips(IntPtr hwnd, Native.RECT rect)
    {
        var scale = GetScale(hwnd);
        return new Rect(rect.Left / scale, rect.Top / scale, rect.Width / scale, rect.Height / scale);
    }
}

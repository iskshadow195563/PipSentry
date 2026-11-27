using System;
using System.Windows;
using System.Windows.Interop;
using PipSentry.Interop;

namespace PipSentry.Platform;

public static class WindowEffects
{
    public static void ApplyDarkChrome(Window window, bool dark = true)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        try
        {
            var on = dark ? 1 : 0;
            Native.DwmSetWindowAttribute(handle, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
        }
        catch
        {
        }
    }

    public static void ApplyRoundedCorners(Window window, int preference = Native.DWMWCP_ROUND)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        try
        {
            var value = preference;
            Native.DwmSetWindowAttribute(handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref value, sizeof(int));
        }
        catch
        {
        }
    }

    public static void SetTopmost(Window window, bool topmost)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        Native.SetWindowPos(handle, topmost ? Native.HWND_TOPMOST : Native.HWND_NOTOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    public static void SetClickThrough(Window window, bool enabled)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var style = (long)Native.GetWindowLongPtr(handle, Native.GWL_EXSTYLE);
        long updated;
        if (enabled)
        {
            updated = style | Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE;
        }
        else
        {
            updated = style & ~(long)Native.WS_EX_TRANSPARENT;
        }

        Native.SetWindowLongPtr(handle, Native.GWL_EXSTYLE, new IntPtr(updated));
    }

    public static void HideFromAltTab(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var style = (long)Native.GetWindowLongPtr(handle, Native.GWL_EXSTYLE);
        var updated = style | Native.WS_EX_TOOLWINDOW;
        Native.SetWindowLongPtr(handle, Native.GWL_EXSTYLE, new IntPtr(updated));
    }
}

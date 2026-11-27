using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PipSentry.Interop;

public static class WindowCatalog
{
    public static List<WindowInfo> Enumerate(bool includeDisplays = true)
    {
        var results = new List<WindowInfo>();
        if (includeDisplays) results.AddRange(EnumerateDisplays());

        var ownPid = (uint)Environment.ProcessId;
        Native.EnumWindows((hwnd, _) =>
        {
            try
            {
                if (!Native.IsWindowVisible(hwnd)) return true;
                var titleLength = Native.GetWindowTextLengthW(hwnd);
                if (titleLength <= 0) return true;

                var title = new System.Text.StringBuilder(titleLength + 1);
                Native.GetWindowTextW(hwnd, title, title.Capacity);
                var text = title.ToString().Trim();
                if (text.Length == 0) return true;

                Native.GetWindowThreadProcessId(hwnd, out var pid);
                if (pid == ownPid) return true;

                if (!Native.GetClientRect(hwnd, out var client)) return true;
                if (client.Width < 64 || client.Height < 64) return true;

                var className = new System.Text.StringBuilder(256);
                Native.GetClassNameW(hwnd, className, className.Capacity);
                var cls = className.ToString();
                if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return true;

                if (IsCloaked(hwnd)) return true;

                string processName = "app";
                string? exePath = null;
                try
                {
                    using var process = Process.GetProcessById((int)pid);
                    processName = process.ProcessName;
                    try { exePath = process.MainModule?.FileName; } catch { }
                }
                catch { }

                results.Add(new WindowInfo
                {
                    Handle = hwnd,
                    Title = text,
                    ClassName = cls,
                    ProcessName = processName,
                    ProcessId = pid,
                    ExecutablePath = exePath,
                    IsMinimized = Native.IsIconic(hwnd),
                    Width = client.Width,
                    Height = client.Height,
                    Icon = LoadIcon(exePath),
                });
            }
            catch
            {
                // A window can vanish mid-enumeration; ignore and continue.
            }

            return true;
        }, IntPtr.Zero);

        results.Sort((a, b) =>
        {
            if (a.IsDisplay != b.IsDisplay) return a.IsDisplay ? -1 : 1;
            var cmp = string.Compare(a.ProcessName, b.ProcessName, StringComparison.OrdinalIgnoreCase);
            return cmp != 0 ? cmp : string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
        });

        return results;
    }

    private static IEnumerable<WindowInfo> EnumerateDisplays()
    {
        var list = new List<WindowInfo>();
        var index = 0;
        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr hdc, ref Native.RECT rect, IntPtr data) =>
        {
            index++;
            var bounds = rect;
            list.Add(new WindowInfo
            {
                Handle = IntPtr.Zero,
                Monitor = monitor,
                IsDisplay = true,
                DisplayIndex = index,
                Title = $"螢幕 {index}",
                ClassName = "Display",
                ProcessName = "桌面擷取",
                Width = bounds.Width,
                Height = bounds.Height,
                Icon = null,
            });
            return true;
        }, IntPtr.Zero);
        return list;
    }

    private static bool IsCloaked(IntPtr hwnd)
    {
        try
        {
            if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;
        }
        catch { }
        return false;
    }

    private static readonly Dictionary<string, ImageSource?> IconCache = new(StringComparer.OrdinalIgnoreCase);

    private static ImageSource? LoadIcon(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return null;
        lock (IconCache)
        {
            if (IconCache.TryGetValue(exePath, out var cached)) return cached;
        }

        ImageSource? source = null;
        try
        {
            if (File.Exists(exePath))
            {
                using var icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon is not null)
                {
                    var bitmap = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(32, 32));
                    bitmap.Freeze();
                    source = bitmap;
                }
            }
        }
        catch { }

        lock (IconCache) { IconCache[exePath] = source; }
        return source;
    }
}

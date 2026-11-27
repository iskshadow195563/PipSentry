using System;
using System.Windows.Media;

namespace PipSentry.Interop;

public sealed class WindowInfo
{
    public IntPtr Handle { get; init; }
    public IntPtr Monitor { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public uint ProcessId { get; init; }
    public string? ExecutablePath { get; init; }
    public bool IsMinimized { get; init; }
    public bool IsDisplay { get; init; }
    public int DisplayIndex { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public ImageSource? Icon { get; init; }

    public string Key => IsDisplay ? $"display:{Monitor}" : $"window:{Handle}";

    public string Subtitle => IsDisplay
        ? $"螢幕 {DisplayIndex} · {Width} × {Height}"
        : $"{ProcessName} · {Width} × {Height}";

    public string Badge => IsMinimized ? "最小化" : IsDisplay ? "桌面" : string.Empty;
}

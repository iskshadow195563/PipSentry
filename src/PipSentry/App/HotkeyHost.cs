using System;
using System.Windows.Input;
using System.Windows.Interop;
using PipSentry.Interop;

namespace PipSentry.Platform;

public sealed class HotkeyHost : IDisposable
{
    private readonly HwndSource _source;
    private int _nextId = 0x5000;

    public event Action<int>? Pressed;

    public HotkeyHost()
    {
        var parameters = new HwndSourceParameters("PipSentryHotkeys")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = (int)Native.HWND_MESSAGE,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public IntPtr Handle => _source.Handle;

    public int Register(ModifierKeys modifiers, Key key)
    {
        uint mods = Native.MOD_NOREPEAT;
        if (modifiers.HasFlag(ModifierKeys.Control)) mods |= Native.MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Alt)) mods |= Native.MOD_ALT;
        if (modifiers.HasFlag(ModifierKeys.Shift)) mods |= Native.MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Windows)) mods |= Native.MOD_WIN;

        var id = _nextId++;
        if (!Native.RegisterHotKey(_source.Handle, id, mods, (uint)KeyInterop.VirtualKeyFromKey(key)))
        {
            return 0;
        }

        return id;
    }

    public void Unregister(int id)
    {
        if (id != 0) Native.UnregisterHotKey(_source.Handle, id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY)
        {
            Pressed?.Invoke(wParam.ToInt32());
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}

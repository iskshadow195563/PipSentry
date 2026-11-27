param([int]$X, [int]$Y)
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RClick {
  [DllImport("user32.dll")] public static extern bool SetThreadDpiAwarenessContext(IntPtr ctx);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr extra);
}
"@
[RClick]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null
[RClick]::SetCursorPos($X, $Y) | Out-Null
Start-Sleep -Milliseconds 150
[RClick]::mouse_event(0x0008, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RClick]::mouse_event(0x0010, 0, 0, 0, [IntPtr]::Zero)
"right clicked $X,$Y"

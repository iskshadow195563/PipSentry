param([int]$X, [int]$Y)
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Mouse2 {
  [DllImport("user32.dll")] public static extern bool SetThreadDpiAwarenessContext(IntPtr ctx);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr extra);
}
"@
[Mouse2]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null
[Mouse2]::SetCursorPos($X, $Y) | Out-Null
Start-Sleep -Milliseconds 120
[Mouse2]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 60
[Mouse2]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
"clicked $X,$Y"

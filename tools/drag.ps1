param([int]$X1, [int]$Y1, [int]$X2, [int]$Y2)
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Drag {
  [DllImport("user32.dll")] public static extern bool SetThreadDpiAwarenessContext(IntPtr ctx);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr extra);
}
"@
[Drag]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null
[Drag]::SetCursorPos($X1, $Y1) | Out-Null
Start-Sleep -Milliseconds 150
[Drag]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 120
$steps = 18
for ($i = 1; $i -le $steps; $i++) {
  $x = [int]($X1 + ($X2 - $X1) * $i / $steps)
  $y = [int]($Y1 + ($Y2 - $Y1) * $i / $steps)
  [Drag]::SetCursorPos($x, $y) | Out-Null
  Start-Sleep -Milliseconds 16
}
[Drag]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
"dragged"

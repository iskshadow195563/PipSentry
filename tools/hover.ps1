param([int]$X, [int]$Y)
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Mv {
  [DllImport("user32.dll")] public static extern bool SetThreadDpiAwarenessContext(IntPtr ctx);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
}
"@
[Mv]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null
[Mv]::SetCursorPos($X, $Y) | Out-Null
"moved to $X,$Y"

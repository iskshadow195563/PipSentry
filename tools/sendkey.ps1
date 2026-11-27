param([int]$Vk = 0x0D)
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Kb {
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
}
"@
[Kb]::keybd_event([byte]$Vk, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 60
[Kb]::keybd_event([byte]$Vk, 0, 2, [IntPtr]::Zero)
"key sent"

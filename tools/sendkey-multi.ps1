param([int]$Vk = 0x28, [int]$Times = 1)
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Kb2 {
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
}
"@
for ($i = 0; $i -lt $Times; $i++) {
  [Kb2]::keybd_event([byte]$Vk, 0, 0, [IntPtr]::Zero)
  Start-Sleep -Milliseconds 40
  [Kb2]::keybd_event([byte]$Vk, 0, 2, [IntPtr]::Zero)
  Start-Sleep -Milliseconds 40
}
"sent $Vk x$Times"

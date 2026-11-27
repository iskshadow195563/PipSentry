using System;
using System.Threading;
using Microsoft.Win32;
using PipSentry.Interop;

namespace PipSentry.Services;

public sealed class KeepAliveService : IDisposable
{
    private Thread? _thread;
    private Timer? _nudgeTimer;
    private volatile bool _running;
    private volatile bool _preventSleep;
    private volatile bool _nudge;
    private long _targetHandle;

    public void Start(bool preventSleep, bool nudge, IntPtr target)
    {
        _preventSleep = preventSleep;
        _nudge = nudge;
        _targetHandle = target.ToInt64();

        if (_thread is null)
        {
            _running = true;
            _thread = new Thread(ExecutionStateLoop)
            {
                IsBackground = true,
                Name = "PipSentry keep-alive",
            };
            _thread.Start();
        }

        _nudgeTimer ??= new Timer(_ => NudgeTarget(), null, 900, 900);
    }

    public void Update(bool preventSleep, bool nudge, IntPtr target)
    {
        _preventSleep = preventSleep;
        _nudge = nudge;
        _targetHandle = target.ToInt64();
    }

    private void ExecutionStateLoop()
    {
        while (_running)
        {
            try
            {
                if (_preventSleep)
                {
                    Native.SetThreadExecutionState(
                        Native.EXECUTION_STATE_CONTINUOUS |
                        Native.EXECUTION_STATE_SYSTEM_REQUIRED |
                        Native.EXECUTION_STATE_DISPLAY_REQUIRED);
                }
                else
                {
                    Native.SetThreadExecutionState(Native.EXECUTION_STATE_CONTINUOUS);
                }
            }
            catch
            {
            }

            Thread.Sleep(15000);
        }
    }

    private void NudgeTarget()
    {
        if (!_nudge) return;
        var handle = new IntPtr(Interlocked.Read(ref _targetHandle));
        if (handle == IntPtr.Zero) return;

        try
        {
            if (!Native.IsWindow(handle)) return;
            Native.InvalidateRect(handle, IntPtr.Zero, false);
            Native.RedrawWindow(handle, IntPtr.Zero, IntPtr.Zero, Native.RDW_INVALIDATE | Native.RDW_ALLCHILDREN);
        }
        catch
        {
        }
    }

    public static void OptOutOfPowerThrottling()
    {
        try
        {
            var state = new Native.PROCESS_POWER_THROTTLING_STATE
            {
                Version = 1,
                ControlMask = 0x1 | 0x2,
                StateMask = 0x0,
            };
            Native.SetProcessInformation(Native.GetCurrentProcess(), 4, ref state, System.Runtime.InteropServices.Marshal.SizeOf<Native.PROCESS_POWER_THROTTLING_STATE>());
        }
        catch
        {
        }
    }

    public static void DisableWindowedGameOptimizations(string executablePath)
    {
        try
        {
            var name = System.IO.Path.GetFileName(executablePath);
            if (string.IsNullOrWhiteSpace(name)) return;
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences", true);
            key?.SetValue(name, "GpuPreference=2;SwapEffectUpgradeEnable=0;", RegistryValueKind.String);
            AppLog.Write($"gpu preference written for {name}");
        }
        catch (Exception ex)
        {
            AppLog.Write("gpu preference failed: " + ex.Message);
        }
    }

    public void Stop()
    {
        _running = false;
        try
        {
            Native.SetThreadExecutionState(Native.EXECUTION_STATE_CONTINUOUS);
        }
        catch
        {
        }

        _nudgeTimer?.Dispose();
        _nudgeTimer = null;
        try { _thread?.Join(250); } catch { }
        _thread = null;
    }

    public void Dispose() => Stop();
}

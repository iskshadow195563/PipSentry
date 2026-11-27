using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PipSentry.Interop;
using PipSentry.Platform;

namespace PipSentry.Capture;

public enum CaptureState
{
    Idle,
    Running,
    Paused,
    Error,
    SourceClosed,
}

public sealed class CaptureController : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private ICaptureEngine? _engine;
    private volatile bool _disposed;
    private int _pendingFrames;
    private bool _fallbackAttempted;
    private int _blankStreak;
    private bool _blankReported;

    public CaptureController(Dispatcher dispatcher) => _dispatcher = dispatcher;

    public event Action<CapturedFrame>? FrameReady;
    public event Action<CaptureState, string?>? StateChanged;
    public event Action<int, int>? SourceSizeChanged;
    public event Action? BlankDetected;

    public CaptureState State { get; private set; } = CaptureState.Idle;
    public bool CompatibilityMode { get; private set; }
    public WindowInfo? Source { get; private set; }
    public int DeliveredFrames;
    public int DroppedFrames;
    public int SourceWidth { get; private set; }
    public int SourceHeight { get; private set; }

    public void Start(WindowInfo source, PixelRect? region, int fps)
    {
        Stop();
        Source = source;
        _fallbackAttempted = false;
        _blankStreak = 0;
        _blankReported = false;

        var wgc = new WgcCaptureEngine();
        try
        {
            Attach(wgc, source, region, fps);
            CompatibilityMode = false;
        }
        catch (Exception)
        {
            wgc.Dispose();
            AttachFallback(source, region, fps);
        }

        SetState(CaptureState.Running, CompatibilityMode ? L.Get("CompatLabel") : null);
    }

    private void AttachFallback(WindowInfo source, PixelRect? region, int fps)
    {
        CompatibilityMode = true;
        Attach(new GdiCaptureEngine(), source, region, Math.Min(fps, 30));
    }

    private void Attach(ICaptureEngine engine, WindowInfo source, PixelRect? region, int fps)
    {
        _engine = engine;
        engine.FrameReady += OnEngineFrame;
        engine.Failed += OnEngineFailed;
        engine.SourceClosed += OnSourceClosed;
        engine.SourceSizeChanged += OnSourceSizeChanged;
        engine.Start(source, region, fps);
    }

    /// <summary>Samples a small grid; the frame counts as blank when almost every sample is near black.</summary>
    private static bool LooksBlank(CapturedFrame frame)
    {
        if (frame.Width < 8 || frame.Height < 8) return false;

        var buffer = frame.Buffer;
        var stride = frame.Stride;
        var dark = 0;
        var samples = 0;

        for (var gy = 1; gy <= 4; gy++)
        {
            var y = frame.Height * gy / 5;
            for (var gx = 1; gx <= 6; gx++)
            {
                var x = frame.Width * gx / 7;
                var offset = (y * stride) + (x * 4);
                if (offset + 3 >= buffer.Length) continue;
                samples++;
                if (buffer[offset] <= 8 && buffer[offset + 1] <= 8 && buffer[offset + 2] <= 8) dark++;
            }
        }

        return samples > 0 && dark >= samples * 9 / 10;
    }

    private void OnSourceSizeChanged(int width, int height)
    {
        SourceWidth = width;
        SourceHeight = height;
        _dispatcher.BeginInvoke(() => SourceSizeChanged?.Invoke(width, height));
    }

    private void OnEngineFrame(CapturedFrame frame)
    {
        if (_disposed)
        {
            frame.Release();
            return;
        }

        if (Interlocked.Increment(ref _pendingFrames) > 2)
        {
            Interlocked.Decrement(ref _pendingFrames);
            Interlocked.Increment(ref DroppedFrames);
            frame.Release();
            return;
        }

        try
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                Interlocked.Decrement(ref _pendingFrames);
                if (_disposed) { frame.Release(); return; }
                Interlocked.Increment(ref DeliveredFrames);
                try
                {
                    if (!_blankReported && LooksBlank(frame))
                    {
                        if (++_blankStreak >= 30)
                        {
                            _blankReported = true;
                            BlankDetected?.Invoke();
                        }
                    }
                    else if (_blankStreak > 0)
                    {
                        _blankStreak = 0;
                    }

                    FrameReady?.Invoke(frame);
                }
                finally
                {
                    frame.Release();
                }
            }));
        }
        catch
        {
            Interlocked.Decrement(ref _pendingFrames);
            frame.Release();
        }
    }

    private void OnEngineFailed(Exception exception)
    {
        if (_disposed) return;

        if (!_fallbackAttempted && !CompatibilityMode && Source is not null)
        {
            _fallbackAttempted = true;
            _dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var source = Source;
                    var previous = _engine;
                    if (source is null) return;
                    previous?.Dispose();
                    AttachFallback(source, null, 30);
                    SetState(CaptureState.Running, L.Get("MsgCompatFallback"));
                }
                catch (Exception ex)
                {
                    SetState(CaptureState.Error, ex.Message);
                }
            }));
            return;
        }

        _dispatcher.BeginInvoke(new Action(() => SetState(CaptureState.Error, exception.Message)));
    }

    private void OnSourceClosed()
    {
        if (_disposed) return;
        _dispatcher.BeginInvoke(new Action(() => SetState(CaptureState.SourceClosed, L.Get("SourceClosed"))));
    }

    public void Pause()
    {
        _engine?.SetPaused(true);
        if (State == CaptureState.Running) SetState(CaptureState.Paused, null);
    }

    public void Resume()
    {
        _engine?.SetPaused(false);
        if (State == CaptureState.Paused) SetState(CaptureState.Running, null);
    }

    public void SetRegion(PixelRect? region) => _engine?.SetRegion(region);

    public void SetFps(int fps) => _engine?.SetFps(fps);

    public void SetCaptureCursor(bool enabled) => _engine?.SetCaptureCursor(enabled);

    public void SetOutputSize(int width, int height) => _engine?.SetOutputSize(width, height);

    public void Stop()
    {
        var engine = _engine;
        _engine = null;
        if (engine is not null)
        {
            engine.FrameReady -= OnEngineFrame;
            engine.Failed -= OnEngineFailed;
            engine.SourceClosed -= OnSourceClosed;
            engine.SourceSizeChanged -= OnSourceSizeChanged;
            engine.Dispose();
        }

        SetState(CaptureState.Idle, null);
    }

    private void SetState(CaptureState state, string? message)
    {
        State = state;
        if (_dispatcher.CheckAccess()) StateChanged?.Invoke(state, message);
        else _dispatcher.BeginInvoke(() => StateChanged?.Invoke(state, message));
    }

    public void Dispose()
    {
        _disposed = true;
        Stop();
    }
}

public static class SnapshotService
{
    /// <summary>Grabs one frame from the source, preferring Windows.Graphics.Capture.</summary>
    public static async Task<BitmapSource?> GrabAsync(WindowInfo source, int timeoutMs = 2600)
    {
        var completion = new TaskCompletionSource<CapturedFrame?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new WgcCaptureEngine();

        void OnFrame(CapturedFrame frame)
        {
            completion.TrySetResult(frame);
        }

        engine.FrameReady += OnFrame;
        try
        {
            engine.Start(source, null, 60);
            var finished = await Task.WhenAny(completion.Task, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (finished == completion.Task && completion.Task.Result is { } frame)
            {
                try
                {
                    return FrameBitmap.Create(frame);
                }
                finally
                {
                    frame.Release();
                }
            }
        }
        catch
        {
        }
        finally
        {
            engine.FrameReady -= OnFrame;
            engine.Dispose();
        }

        return GdiSnapshot.Grab(source);
    }
}

public static class FrameBitmap
{
    public static BitmapSource Create(CapturedFrame frame)
    {
        var bitmap = BitmapSource.Create(
            frame.Width,
            frame.Height,
            96,
            96,
            System.Windows.Media.PixelFormats.Bgra32,
            null,
            frame.Buffer,
            frame.Stride);
        bitmap.Freeze();
        return bitmap;
    }
}

public static class GdiSnapshot
{
    public static BitmapSource? Grab(WindowInfo source)
    {
        try
        {
            var width = source.Width;
            var height = source.Height;
            var screenDc = Native.GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero) return null;

            var memoryDc = Native.CreateCompatibleDC(screenDc);
            var bitmap = Native.CreateCompatibleBitmap(screenDc, width, height);
            var previous = Native.SelectObject(memoryDc, bitmap);
            try
            {
                if (!Native.PrintWindow(source.Handle, memoryDc, Native.PW_RENDERFULLCONTENT))
                {
                    return null;
                }

                var info = new Native.BITMAPINFO
                {
                    bmiHeader = new Native.BITMAPINFOHEADER
                    {
                        biSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.BITMAPINFOHEADER>(),
                        biWidth = width,
                        biHeight = -height,
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = Native.BI_RGB,
                    },
                };

                var buffer = new byte[width * height * 4];
                if (Native.GetDIBits(memoryDc, bitmap, 0, (uint)height, buffer, ref info, Native.DIB_RGB_COLORS) == 0)
                    return null;

                var image = BitmapSource.Create(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, buffer, width * 4);
                image.Freeze();
                return image;
            }
            finally
            {
                Native.SelectObject(memoryDc, previous);
                Native.DeleteObject(bitmap);
                Native.DeleteDC(memoryDc);
                Native.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
        catch
        {
            return null;
        }
    }
}
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using PipSentry.Interop;
using PipSentry.Platform;

namespace PipSentry.Capture;

/// <summary>Fallback when WGC is unavailable.</summary>
public sealed class GdiCaptureEngine : ICaptureEngine
{
    private readonly BufferPool _buffers = new();
    private readonly object _gate = new();
    private Thread? _thread;
    private volatile bool _running;
    private volatile bool _paused;
    private volatile int _fps = 15;
    private PixelRect? _region;
    private WindowInfo? _source;
    private int _width;
    private int _height;

    public event Action<CapturedFrame>? FrameReady;
    public event Action<Exception>? Failed;
    public event Action? SourceClosed;
    public event Action<int, int>? SourceSizeChanged;

    public void Start(WindowInfo source, PixelRect? region, int fps)
    {
        _source = source;
        _region = region;
        _fps = Math.Clamp(fps, 1, 60);
        _running = true;

        if (source.IsDisplay)
        {
            var bounds = ScreenGeometry.GetMonitorBounds(source.Monitor);
            _width = bounds.Width;
            _height = bounds.Height;
        }
        else
        {
            Native.GetClientRect(source.Handle, out var client);
            _width = client.Width;
            _height = client.Height;
        }

        SourceSizeChanged?.Invoke(_width, _height);
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "PipSentry GDI capture",
        };
        _thread.Start();
    }

    public void SetRegion(PixelRect? region) => _region = region;

    public void SetFps(int fps) => _fps = Math.Clamp(fps, 1, 60);

    public void SetPaused(bool paused) => _paused = paused;

    public void SetCaptureCursor(bool enabled)
    {
    }

    private void Loop()
    {
        while (_running)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                if (!_paused) CaptureOnce();
            }
            catch (Exception ex)
            {
                Failed?.Invoke(ex);
            }

            var elapsedMs = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            var delay = (1000.0 / Math.Max(1, _fps)) - elapsedMs;
            if (delay > 0) Thread.Sleep((int)delay);
        }
    }

    private void CaptureOnce()
    {
        var source = _source;
        if (source is null) return;

        if (!source.IsDisplay && !Native.IsWindow(source.Handle))
        {
            SourceClosed?.Invoke();
            _running = false;
            return;
        }

        var clientWidth = _width;
        var clientHeight = _height;
        if (!source.IsDisplay && Native.GetClientRect(source.Handle, out var client))
        {
            clientWidth = Math.Max(1, client.Width);
            clientHeight = Math.Max(1, client.Height);
            if (clientWidth != _width || clientHeight != _height)
            {
                _width = clientWidth;
                _height = clientHeight;
                SourceSizeChanged?.Invoke(clientWidth, clientHeight);
            }
        }

        var hdcSource = source.IsDisplay
            ? Native.GetDC(IntPtr.Zero)
            : Native.GetDC(source.Handle);

        if (hdcSource == IntPtr.Zero) return;

        var memoryDc = Native.CreateCompatibleDC(hdcSource);
        var bitmap = Native.CreateCompatibleBitmap(hdcSource, clientWidth, clientHeight);
        var previous = Native.SelectObject(memoryDc, bitmap);

        try
        {
            var captured = false;
            if (source.IsDisplay)
            {
                var bounds = ScreenGeometry.GetMonitorBounds(source.Monitor);
                captured = Native.BitBlt(memoryDc, 0, 0, clientWidth, clientHeight, hdcSource, bounds.Left, bounds.Top, Native.SRCCOPY);
            }
            else
            {
                captured = Native.PrintWindow(source.Handle, memoryDc, Native.PW_RENDERFULLCONTENT);
                if (!captured)
                {
                    captured = Native.BitBlt(memoryDc, 0, 0, clientWidth, clientHeight, hdcSource, 0, 0, Native.SRCCOPY);
                }
            }

            if (!captured) return;

            var info = new Native.BITMAPINFO
            {
                bmiHeader = new Native.BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<Native.BITMAPINFOHEADER>(),
                    biWidth = clientWidth,
                    biHeight = -clientHeight,
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = Native.BI_RGB,
                },
            };

            var fullStride = clientWidth * 4;
            var fullBuffer = _buffers.Rent(fullStride * clientHeight);
            var lines = Native.GetDIBits(memoryDc, bitmap, 0, (uint)clientHeight, fullBuffer, ref info, Native.DIB_RGB_COLORS);
            if (lines == 0)
            {
                _buffers.Return(fullBuffer);
                return;
            }

            var region = _region is { IsEmpty: false } r ? r.Clamp(clientWidth, clientHeight) : new PixelRect(0, 0, clientWidth, clientHeight);
            var tightStride = region.Width * 4;

            if (region.X == 0 && region.Y == 0 && region.Width == clientWidth && region.Height == clientHeight)
            {
                FrameReady?.Invoke(new CapturedFrame(fullBuffer, clientWidth, clientHeight, Stopwatch.GetTimestamp(), _buffers.Return));
                return;
            }

            var cropped = _buffers.Rent(tightStride * region.Height);
            for (var y = 0; y < region.Height; y++)
            {
                Buffer.BlockCopy(fullBuffer, ((region.Y + y) * fullStride) + (region.X * 4), cropped, y * tightStride, tightStride);
            }

            _buffers.Return(fullBuffer);
            FrameReady?.Invoke(new CapturedFrame(cropped, region.Width, region.Height, Stopwatch.GetTimestamp(), _buffers.Return));
        }
        finally
        {
            Native.SelectObject(memoryDc, previous);
            Native.DeleteObject(bitmap);
            Native.DeleteDC(memoryDc);
            Native.ReleaseDC(source.IsDisplay ? IntPtr.Zero : source.Handle, hdcSource);
        }
    }

    public void Dispose()
    {
        _running = false;
        try { _thread?.Join(400); } catch { }
        _thread = null;
    }
}

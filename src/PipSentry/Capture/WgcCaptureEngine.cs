using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using PipSentry.Interop;
using PipSentry.Platform;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Capture;
using SharpDX.Direct3D;
using D3D11Device = SharpDX.Direct3D11.Device;

namespace PipSentry.Capture;

public interface ICaptureEngine : IDisposable
{
    event Action<CapturedFrame>? FrameReady;
    event Action<Exception>? Failed;
    event Action? SourceClosed;
    event Action<int, int>? SourceSizeChanged;

    void Start(WindowInfo source, PixelRect? region, int fps);
    void SetRegion(PixelRect? region);
    void SetFps(int fps);
    void SetPaused(bool paused);
    void SetCaptureCursor(bool enabled);
    void SetOutputSize(int width, int height);
}

public sealed class WgcCaptureEngine : ICaptureEngine
{
    private readonly object _gate = new();
    private readonly BufferPool _buffers = new();
    private D3D11Device? _d3d;
    private Windows.Graphics.DirectX.Direct3D11.IDirect3DDevice? _device;
    private GraphicsCaptureItem? _item;
    private Direct3D11CaptureFramePool? _pool;
    private GraphicsCaptureSession? _session;
    private Texture2D? _staging;
    private int _stagingWidth;
    private int _stagingHeight;
    private SizeInt32 _poolSize;
    private PixelRect? _region;
    private int _fps = 30;
    private long _lastFrameTick;
    private bool _paused;
    private bool _captureCursor;
    private int _outputWidth;
    private int _outputHeight;
    private GpuScaler? _scaler;
    private readonly System.Collections.Generic.Stack<CapturedFrame> _framePool = new();
    private bool _disposed;

    public event Action<CapturedFrame>? FrameReady;
    public event Action<Exception>? Failed;
    public event Action? SourceClosed;
    public event Action<int, int>? SourceSizeChanged;

    public SizeInt32 SourceSize { get; private set; }

    public void Start(WindowInfo source, PixelRect? region, int fps)
    {
        _region = region;
        _fps = Math.Clamp(fps, 1, 240);

        _d3d = WgcInterop.CreateD3DDevice();
        _device = WgcInterop.CreateWinRTDevice(_d3d);

        _item = source.IsDisplay
            ? WgcInterop.CreateItemForMonitor(source.Monitor)
            : WgcInterop.CreateItemForWindow(source.Handle);

        SourceSize = _item.Size;
        _poolSize = _item.Size;

        _pool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            _device,
            Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
            3,
            _item.Size);
        _pool.FrameArrived += OnFrameArrived;

        _item.Closed += OnItemClosed;

        _session = _pool.CreateCaptureSession(_item);
        try
        {
            _session.IsBorderRequired = false;
        }
        catch
        {
        }

        SetCaptureCursor(_captureCursor);

        _session.StartCapture();
    }

    public void SetRegion(PixelRect? region)
    {
        lock (_gate)
        {
            _region = region;
            _staging?.Dispose();
            _staging = null;
        }
    }

    public void SetFps(int fps) => _fps = Math.Clamp(fps, 1, 240);

    public void SetPaused(bool paused) => _paused = paused;

    /// <summary>Caps the frame handed to the UI; 0 means native size.</summary>
    public void SetOutputSize(int width, int height)
    {
        _outputWidth = width;
        _outputHeight = height;
    }

    public void SetCaptureCursor(bool enabled)
    {
        _captureCursor = enabled;
        if (_session is null) return;
        try
        {
            _session.IsCursorCaptureEnabled = enabled;
        }
        catch
        {
        }
    }

    private void OnItemClosed(GraphicsCaptureItem sender, object args) => SourceClosed?.Invoke();

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        if (_disposed) return;

        using var frame = sender.TryGetNextFrame();
        if (frame is null) return;

        var now = Stopwatch.GetTimestamp();
        var minimumGap = Stopwatch.Frequency / Math.Max(1, _fps);
        if (now - _lastFrameTick < minimumGap) return;
        _lastFrameTick = now;

        if (_paused) return;

        try
        {
            PublishFrame(frame);
        }
        catch (Exception ex)
        {
            Failed?.Invoke(ex);
        }
    }

    private void PublishFrame(Direct3D11CaptureFrame frame)
    {
        if (_d3d is null) return;

        using var texture = WgcInterop.GetTexture(frame.Surface);
        var description = texture.Description;
        var frameWidth = (int)description.Width;
        var frameHeight = (int)description.Height;
        if (frameWidth <= 0 || frameHeight <= 0) return;

        var region = _region is { IsEmpty: false } r ? r.Clamp(frameWidth, frameHeight) : new PixelRect(0, 0, frameWidth, frameHeight);

        lock (_gate)
        {
            if (_disposed) return;

            var (outWidth, outHeight) = CaptureMath.FitOutput(region.Width, region.Height, _outputWidth, _outputHeight);
            if (outWidth != region.Width || outHeight != region.Height)
            {
                _scaler ??= new GpuScaler(_d3d);
                if (_scaler.Draw(texture, region, outWidth, outHeight))
                {
                    var scaledStride = outWidth * 4;
                    var scaledBuffer = _buffers.Rent(scaledStride * outHeight);
                    _scaler.CopyTo(scaledBuffer, outWidth, outHeight);
                    FrameReady?.Invoke(RentFrame(scaledBuffer, outWidth, outHeight));
                    return;
                }
            }

            EnsureStaging(region.Width, region.Height);
            var context = _d3d.ImmediateContext;
            var sourceRegion = new ResourceRegion(region.X, region.Y, 0, region.Right, region.Bottom, 1);
            context.CopySubresourceRegion(texture, 0, sourceRegion, _staging, 0, 0, 0, 0);

            var mapped = context.MapSubresource(_staging, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
            outWidth = region.Width;
            outHeight = region.Height;
            byte[] buffer;
            try
            {
                var stride = region.Width * 4;
                buffer = _buffers.Rent(stride * region.Height);
                for (var y = 0; y < region.Height; y++)
                {
                    Marshal.Copy(mapped.DataPointer + (y * mapped.RowPitch), buffer, y * stride, stride);
                }
            }
            finally
            {
                context.UnmapSubresource(_staging, 0);
            }

            FrameReady?.Invoke(RentFrame(buffer, outWidth, outHeight));
        }

        var content = frame.ContentSize;
        if (content.Width > 0 && content.Height > 0 && (content.Width != _poolSize.Width || content.Height != _poolSize.Height))
        {
            _poolSize = content;
            SourceSize = content;
            try
            {
                _pool?.Recreate(
                    _device,
                    Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    3,
                    content);
            }
            catch
            {
            }

            SourceSizeChanged?.Invoke(content.Width, content.Height);
        }
    }

    private void EnsureStaging(int width, int height)
    {
        if (_staging is not null && _stagingWidth == width && _stagingHeight == height) return;
        _staging?.Dispose();
        _stagingWidth = width;
        _stagingHeight = height;
        _staging = new Texture2D(_d3d, new Texture2DDescription
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CpuAccessFlags = CpuAccessFlags.Read,
            OptionFlags = ResourceOptionFlags.None,
        });
    }

    private CapturedFrame RentFrame(byte[] buffer, int width, int height)
    {
        CapturedFrame frame;
        lock (_framePool)
        {
            frame = _framePool.Count > 0 ? _framePool.Pop() : new CapturedFrame();
        }

        frame.Init(buffer, width, height, Stopwatch.GetTimestamp(), _buffers.Return, ReturnFrame);
        return frame;
    }

    private void ReturnFrame(CapturedFrame frame)
    {
        lock (_framePool)
        {
            if (_framePool.Count < 4) _framePool.Push(frame);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }

        try
        {
            if (_item is not null) _item.Closed -= OnItemClosed;
            if (_pool is not null) _pool.FrameArrived -= OnFrameArrived;
            _session?.Dispose();
            _pool?.Dispose();
            _item = null;
            lock (_gate)
            {
                _staging?.Dispose();
                _staging = null;
            }
            _scaler?.Dispose();
            _scaler = null;
            _d3d?.Dispose();
            _d3d = null;
            _device = null;
        }
        catch
        {
        }
    }
}
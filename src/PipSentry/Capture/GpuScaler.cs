using System;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using D3D11Device = SharpDX.Direct3D11.Device;
using D2DDeviceContext = SharpDX.Direct2D1.DeviceContext;

namespace PipSentry.Capture;

/// <summary>GPU downscale of the captured frame, so only display-sized pixels come back to the CPU.</summary>
internal sealed class GpuScaler : IDisposable
{
    private readonly D3D11Device _device;
    private readonly SharpDX.DXGI.Device _dxgiDevice;
    private SharpDX.Direct2D1.Factory1? _factory;
    private SharpDX.Direct2D1.Device? _d2dDevice;
    private D2DDeviceContext? _context;
    private Texture2D? _target;
    private Bitmap1? _targetBitmap;
    private Texture2D? _staging;
    private int _width;
    private int _height;

    public GpuScaler(D3D11Device device)
    {
        _device = device;
        _dxgiDevice = device.QueryInterface<SharpDX.DXGI.Device>();
    }

    public bool Failed { get; private set; }

    private bool Ensure(int width, int height)
    {
        if (Failed) return false;
        if (_target is not null && _width == width && _height == height) return true;

        try
        {
            _factory ??= new SharpDX.Direct2D1.Factory1();
            if (_d2dDevice is null)
            {
                _d2dDevice = new SharpDX.Direct2D1.Device(_dxgiDevice);
                _context = new D2DDeviceContext(_d2dDevice, DeviceContextOptions.None);
            }

            _targetBitmap?.Dispose();
            _target?.Dispose();
            _staging?.Dispose();

            _target = new Texture2D(_device, new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None,
            });

            using (var surface = _target.QueryInterface<SharpDX.DXGI.Surface>())
            {
                var properties = new BitmapProperties1(
                    new PixelFormat(Format.B8G8R8A8_UNorm, SharpDX.Direct2D1.AlphaMode.Ignore), 96, 96, BitmapOptions.Target, null);
                _targetBitmap = new Bitmap1(_context, surface, properties);
            }

            _staging = new Texture2D(_device, new Texture2DDescription
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

            _width = width;
            _height = height;
            return true;
        }
        catch (Exception)
        {
            Failed = true;
            return false;
        }
    }

    public bool Draw(Texture2D source, PixelRect region, int width, int height)
    {
        if (!Ensure(width, height)) return false;

        try
        {
            using var surface = source.QueryInterface<SharpDX.DXGI.Surface>();
            var properties = new BitmapProperties1(
                new PixelFormat(Format.B8G8R8A8_UNorm, SharpDX.Direct2D1.AlphaMode.Ignore), 96, 96, BitmapOptions.None, null);
            using var sourceBitmap = new Bitmap1(_context!, surface, properties);

            _context!.Target = _targetBitmap;
            _context.BeginDraw();
            _context.DrawBitmap(
                sourceBitmap,
                new RawRectangleF(0, 0, width, height),
                1f,
                BitmapInterpolationMode.Linear,
                new RawRectangleF(region.X, region.Y, region.Right, region.Bottom));
            _context.EndDraw();

            _device.ImmediateContext.CopyResource(_target, _staging);
            return true;
        }
        catch (Exception)
        {
            Failed = true;
            return false;
        }
    }

    public void CopyTo(byte[] destination, int width, int height)
    {
        var context = _device.ImmediateContext;
        var mapped = context.MapSubresource(_staging, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
        try
        {
            var stride = width * 4;
            for (var y = 0; y < height; y++)
            {
                Marshal.Copy(mapped.DataPointer + (y * mapped.RowPitch), destination, y * stride, stride);
            }
        }
        finally
        {
            context.UnmapSubresource(_staging, 0);
        }
    }

    public void Dispose()
    {
        _targetBitmap?.Dispose();
        _staging?.Dispose();
        _target?.Dispose();
        _context?.Dispose();
        _d2dDevice?.Dispose();
        _factory?.Dispose();
        _dxgiDevice.Dispose();
    }
}
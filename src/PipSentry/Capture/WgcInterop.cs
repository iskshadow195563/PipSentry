using System;
using System.Runtime.InteropServices;
using SharpDX.Direct3D11;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace PipSentry.Capture;

[ComImport]
[Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDirect3DDxgiInterfaceAccess
{
    IntPtr GetInterface([In] ref Guid iid);
}

[ComImport]
[Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IGraphicsCaptureItemInterop
{
    IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);
    IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
}

internal static class WgcInterop
{
    private static readonly Guid IID_IActivationFactory = new("00000035-0000-0000-C000-000000000046");
    private static readonly Guid IID_IGraphicsCaptureItemInterop = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
    private static readonly Guid IID_IGraphicsCaptureItem = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid IID_ID3D11Texture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string source, int length, out IntPtr hstring);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    public static SharpDX.Direct3D11.Device CreateD3DDevice()
    {
        return new SharpDX.Direct3D11.Device(SharpDX.Direct3D.DriverType.Hardware, DeviceCreationFlags.BgraSupport);
    }

    public static IDirect3DDevice CreateWinRTDevice(SharpDX.Direct3D11.Device device)
    {
        using var dxgiDevice = device.QueryInterface<SharpDX.DXGI.Device3>();
        var hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var inspectable);
        if (hr != 0) throw new InvalidOperationException($"CreateDirect3D11DeviceFromDXGIDevice failed: 0x{hr:X8}");
        try
        {
            return MarshalInspectable<object>.FromAbi(inspectable).As<IDirect3DDevice>();
        }
        finally
        {
            if (inspectable != IntPtr.Zero) Marshal.Release(inspectable);
        }
    }

    private static IGraphicsCaptureItemInterop GetItemInterop()
    {
        const string className = "Windows.Graphics.Capture.GraphicsCaptureItem";
        var hr = WindowsCreateString(className, className.Length, out var hstring);
        if (hr != 0) throw new InvalidOperationException($"WindowsCreateString failed: 0x{hr:X8}");

        try
        {
            var iid = IID_IActivationFactory;
            hr = RoGetActivationFactory(hstring, ref iid, out var factory);
            if (hr != 0) throw new InvalidOperationException($"RoGetActivationFactory failed: 0x{hr:X8}");

            try
            {
                var iidInterop = IID_IGraphicsCaptureItemInterop;
                hr = Marshal.QueryInterface(factory, ref iidInterop, out var interopPtr);
                if (hr != 0) throw new InvalidOperationException($"QI capture interop failed: 0x{hr:X8}");

                try
                {
                    return (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(interopPtr);
                }
                finally
                {
                    Marshal.Release(interopPtr);
                }
            }
            finally
            {
                Marshal.Release(factory);
            }
        }
        finally
        {
            WindowsDeleteString(hstring);
        }
    }

    public static GraphicsCaptureItem CreateItemForWindow(IntPtr hwnd)
    {
        var interop = GetItemInterop();
        var iid = IID_IGraphicsCaptureItem;
        var pointer = interop.CreateForWindow(hwnd, ref iid);
        if (pointer == IntPtr.Zero) throw new InvalidOperationException("CreateForWindow returned null");
        try
        {
            return MarshalInspectable<GraphicsCaptureItem>.FromAbi(pointer);
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }

    public static GraphicsCaptureItem CreateItemForMonitor(IntPtr hmonitor)
    {
        var interop = GetItemInterop();
        var iid = IID_IGraphicsCaptureItem;
        var pointer = interop.CreateForMonitor(hmonitor, ref iid);
        if (pointer == IntPtr.Zero) throw new InvalidOperationException("CreateForMonitor returned null");
        try
        {
            return MarshalInspectable<GraphicsCaptureItem>.FromAbi(pointer);
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }

    public static Texture2D GetTexture(IDirect3DSurface surface)
    {
        var access = (IDirect3DDxgiInterfaceAccess)Marshal.GetObjectForIUnknown(
            ((IWinRTObject)surface).NativeObject.ThisPtr);
        var iid = IID_ID3D11Texture2D;
        var pointer = access.GetInterface(ref iid);
        if (pointer == IntPtr.Zero) throw new InvalidOperationException("DXGI interface access returned null");
        return new Texture2D(pointer);
    }
}

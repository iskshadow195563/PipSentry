using System;

namespace PipSentry.Capture;

/// <summary>BGRA32 frame lent from the engine pool. Caller must call Release.</summary>
public sealed class CapturedFrame
{
    private Action<byte[]>? _release;
    private Action<CapturedFrame>? _return;

    internal CapturedFrame()
    {
    }

    internal CapturedFrame(byte[] buffer, int width, int height, long timestamp, Action<byte[]> release)
    {
        Init(buffer, width, height, timestamp, release, null);
    }

    public byte[] Buffer { get; private set; } = Array.Empty<byte>();
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int Stride => Width * 4;
    public long Timestamp { get; private set; }

    internal void Init(byte[] buffer, int width, int height, long timestamp, Action<byte[]> release, Action<CapturedFrame>? returnFrame)
    {
        Buffer = buffer;
        Width = width;
        Height = height;
        Timestamp = timestamp;
        _release = release;
        _return = returnFrame;
    }

    public void Release()
    {
        var release = _release;
        if (release is null) return;
        _release = null;
        var returnFrame = _return;
        _return = null;
        release(Buffer);
        returnFrame?.Invoke(this);
    }
}
using System;

namespace PipSentry.Capture;

/// <summary>A BGRA32 frame whose buffer is rented from the engine pool. The consumer must call Release once done.</summary>
public sealed class CapturedFrame
{
    private Action<byte[]>? _release;

    internal CapturedFrame(byte[] buffer, int width, int height, long timestamp, Action<byte[]> release)
    {
        Buffer = buffer;
        Width = width;
        Height = height;
        Timestamp = timestamp;
        _release = release;
    }

    public byte[] Buffer { get; }
    public int Width { get; }
    public int Height { get; }
    public int Stride => Width * 4;
    public long Timestamp { get; }

    public void Release()
    {
        var release = _release;
        _release = null;
        release?.Invoke(Buffer);
    }
}

internal sealed class BufferPool
{
    private readonly System.Collections.Generic.Stack<byte[]> _free = new();
    private readonly int _capacity;

    public BufferPool(int capacity = 6) => _capacity = capacity;

    public byte[] Rent(int size)
    {
        lock (_free)
        {
            while (_free.Count > 0)
            {
                var buffer = _free.Pop();
                if (buffer.Length >= size) return buffer;
            }
        }

        return new byte[size];
    }

    public void Return(byte[] buffer)
    {
        lock (_free)
        {
            if (_free.Count < _capacity) _free.Push(buffer);
        }
    }
}

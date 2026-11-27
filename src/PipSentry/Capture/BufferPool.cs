using System.Collections.Generic;

namespace PipSentry.Capture;

internal sealed class BufferPool
{
    private readonly Stack<byte[]> _free = new();
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

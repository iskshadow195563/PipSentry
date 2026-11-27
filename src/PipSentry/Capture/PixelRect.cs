using System;

namespace PipSentry.Capture;

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public PixelRect Clamp(int maxWidth, int maxHeight)
    {
        var x = Math.Clamp(X, 0, Math.Max(0, maxWidth - 1));
        var y = Math.Clamp(Y, 0, Math.Max(0, maxHeight - 1));
        var w = Math.Clamp(Width, 1, maxWidth - x);
        var h = Math.Clamp(Height, 1, maxHeight - y);
        return new PixelRect(x, y, w, h);
    }

    public override string ToString() => $"{Width}×{Height}";
}

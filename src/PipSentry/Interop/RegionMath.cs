using System;
using PipSentry.Capture;

namespace PipSentry.Interop;

public static class RegionMath
{
    /// <summary>
    /// Rescales a pixel region when the source surface changes size (e.g. game switches resolution).
    /// </summary>
    public static PixelRect Scale(PixelRect region, int fromWidth, int fromHeight, int toWidth, int toHeight)
    {
        if (region.IsEmpty || fromWidth <= 0 || fromHeight <= 0 || toWidth <= 0 || toHeight <= 0)
        {
            return region;
        }

        if (fromWidth == toWidth && fromHeight == toHeight) return region;

        var scaleX = (double)toWidth / fromWidth;
        var scaleY = (double)toHeight / fromHeight;

        return new PixelRect(
            (int)Math.Round(region.X * scaleX),
            (int)Math.Round(region.Y * scaleY),
            Math.Max(1, (int)Math.Round(region.Width * scaleX)),
            Math.Max(1, (int)Math.Round(region.Height * scaleY))).Clamp(toWidth, toHeight);
    }
}

namespace PipSentry.Capture;

public static class CaptureMath
{
    /// <summary>Fits a source rect into an output box, preserving aspect. Zero box means native size.</summary>
    public static (int Width, int Height) FitOutput(int sourceWidth, int sourceHeight, int maxWidth, int maxHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0) return (0, 0);
        if (maxWidth <= 0 || maxHeight <= 0) return (sourceWidth, sourceHeight);
        if (sourceWidth <= maxWidth && sourceHeight <= maxHeight) return (sourceWidth, sourceHeight);

        var scale = System.Math.Min((double)maxWidth / sourceWidth, (double)maxHeight / sourceHeight);
        return (System.Math.Max(1, (int)System.Math.Round(sourceWidth * scale)),
                System.Math.Max(1, (int)System.Math.Round(sourceHeight * scale)));
    }
}

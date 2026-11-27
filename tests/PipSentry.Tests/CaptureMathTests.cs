using System;
using Xunit;
using PipSentry.Capture;

namespace PipSentry.Tests;

public class CaptureMathTests
{
    [Fact]
    public void ZeroBox_KeepsNativeSize()
    {
        var (w, h) = CaptureMath.FitOutput(1920, 1080, 0, 0);
        Assert.Equal(1920, w);
        Assert.Equal(1080, h);
    }

    [Fact]
    public void ZeroBox_KeepsNativeSize_ForLargeSource()
    {
        var (w, h) = CaptureMath.FitOutput(3840, 2160, 0, 0);
        Assert.Equal(3840, w);
        Assert.Equal(2160, h);
    }

    [Fact]
    public void SourceSmallerThanBox_ReturnsSource()
    {
        var (w, h) = CaptureMath.FitOutput(200, 100, 560, 315);
        Assert.Equal(200, w);
        Assert.Equal(100, h);
    }

    [Fact]
    public void SourcePartiallyExceedsBox_ScalesByTheConstrainedAxis()
    {
        // 500 wide does not fit in 400, 200 high does fit in 800.
        var (w, h) = CaptureMath.FitOutput(500, 200, 400, 800);
        Assert.Equal(400, w);
        Assert.Equal(160, h);
    }

    [Fact]
    public void FullHd4K_Into560x315_ReturnsBoxSize()
    {
        var (w, h) = CaptureMath.FitOutput(3840, 2160, 560, 315);
        Assert.Equal(560, w);
        Assert.Equal(315, h);
    }

    [Fact]
    public void PortraitSource_FitsLandscapeBox_HeightIsLimiting()
    {
        // 1080x1920 into 560x315: the box height (315) is the limiting dimension,
        // so the fitted width is 1080 * 315 / 1920 = 177.1875 -> 177.
        var (w, h) = CaptureMath.FitOutput(1080, 1920, 560, 315);
        Assert.Equal(177, w);
        Assert.Equal(315, h);
        Assert.True(h <= 315 && w <= 560, "result must stay inside the box");
    }

    [Fact]
    public void PortraitSource_FitsPortraitBox_Returns315x560()
    {
        // Same portrait source into a 315x560 box is an exact fit: 315x560.
        var (w, h) = CaptureMath.FitOutput(1080, 1920, 315, 560);
        Assert.Equal(315, w);
        Assert.Equal(560, h);
    }

    [Theory]
    [InlineData(0, 1080, 560, 315)]
    [InlineData(1920, 0, 560, 315)]
    [InlineData(-1, 1080, 560, 315)]
    [InlineData(1920, -1, 560, 315)]
    [InlineData(0, 0, 0, 0)]
    public void InvalidSource_ReturnsZero(int sourceWidth, int sourceHeight, int boxWidth, int boxHeight)
    {
        var (w, h) = CaptureMath.FitOutput(sourceWidth, sourceHeight, boxWidth, boxHeight);
        Assert.Equal(0, w);
        Assert.Equal(0, h);
    }

    [Theory]
    [InlineData(3840, 2160, 560, 315)]
    [InlineData(1920, 1080, 560, 315)]
    [InlineData(1080, 1920, 560, 315)]
    [InlineData(1080, 1920, 315, 560)]
    [InlineData(2560, 1440, 100, 100)]
    [InlineData(800, 600, 801, 599)]
    [InlineData(1000, 1000, 1, 1)]
    [InlineData(20000, 1000, 1001, 1000)]
    [InlineData(7, 900, 3, 2000)]
    [InlineData(640, 480, 1920, 1080)]
    [InlineData(1366, 768, 333, 777)]
    public void ResultNeverExceedsBox(int sourceWidth, int sourceHeight, int boxWidth, int boxHeight)
    {
        var (w, h) = CaptureMath.FitOutput(sourceWidth, sourceHeight, boxWidth, boxHeight);
        Assert.InRange(w, 1, boxWidth);
        Assert.InRange(h, 1, boxHeight);
    }

    [Theory]
    [InlineData(3840, 2160, 560, 315)]
    [InlineData(1080, 1920, 560, 315)]
    [InlineData(1080, 1920, 315, 560)]
    [InlineData(2560, 1440, 300, 300)]
    [InlineData(1366, 768, 200, 500)]
    public void AspectRatioIsPreserved(int sourceWidth, int sourceHeight, int boxWidth, int boxHeight)
    {
        var (w, h) = CaptureMath.FitOutput(sourceWidth, sourceHeight, boxWidth, boxHeight);
        var expected = (double)sourceWidth / sourceHeight;
        var actual = (double)w / h;
        Assert.True(Math.Abs(expected - actual) <= 0.01,
            $"aspect {actual:F4} drifted from source {expected:F4} for {sourceWidth}x{sourceHeight} -> {w}x{h}");
    }
}

using System;
using Xunit;
using PipSentry.Capture;
using PipSentry.Interop;

namespace PipSentry.Tests;

public class RegionMathTests
{
    [Fact]
    public void SameSize_ReturnsSameRect()
    {
        var region = new PixelRect(10, 20, 100, 200);
        var scaled = RegionMath.Scale(region, 1920, 1080, 1920, 1080);
        Assert.Equal(region, scaled);
    }

    [Fact]
    public void DoublingSourceSize_DoublesEveryField()
    {
        var region = new PixelRect(10, 20, 100, 200);
        var scaled = RegionMath.Scale(region, 1920, 1080, 3840, 2160);
        Assert.Equal(new PixelRect(20, 40, 200, 400), scaled);
    }

    [Fact]
    public void HalvingSourceSize_HalvesEveryField()
    {
        var region = new PixelRect(100, 200, 400, 600);
        var scaled = RegionMath.Scale(region, 3840, 2160, 1920, 1080);
        Assert.Equal(new PixelRect(50, 100, 200, 300), scaled);
    }

    [Theory]
    [InlineData(1900, 1000, 200, 100, 1920, 1080, 3840, 2160)]
    [InlineData(5000, 5000, 100, 100, 1920, 1080, 3840, 2160)]
    [InlineData(-50, -50, 100, 100, 1920, 1080, 3840, 2160)]
    [InlineData(1919, 1079, 2, 2, 1920, 1080, 3840, 2160)]
    [InlineData(0, 0, 1920, 1080, 1920, 1080, 1280, 720)]
    public void Clamp_KeepsRectInsideTarget(
        int x, int y, int width, int height,
        int fromWidth, int fromHeight, int toWidth, int toHeight)
    {
        var scaled = RegionMath.Scale(new PixelRect(x, y, width, height), fromWidth, fromHeight, toWidth, toHeight);

        Assert.True(scaled.X >= 0, $"X {scaled.X} is negative");
        Assert.True(scaled.Y >= 0, $"Y {scaled.Y} is negative");
        Assert.True(scaled.Width >= 1, $"Width {scaled.Width} is not positive");
        Assert.True(scaled.Height >= 1, $"Height {scaled.Height} is not positive");
        Assert.True(scaled.Right <= toWidth, $"Right {scaled.Right} exceeds target width {toWidth}");
        Assert.True(scaled.Bottom <= toHeight, $"Bottom {scaled.Bottom} exceeds target height {toHeight}");
    }

    [Fact]
    public void Clamp_TrimsOverflowingEdgeExactly()
    {
        // 200x100 at (1900,1000) on a 1920x1080 source, target 3840x2160:
        // raw rect is (3800,2000,400,200) -> the target edge trims it to 40x160.
        var scaled = RegionMath.Scale(new PixelRect(1900, 1000, 200, 100), 1920, 1080, 3840, 2160);
        Assert.Equal(new PixelRect(3800, 2000, 40, 160), scaled);
    }

    [Fact]
    public void EmptyRegion_ReturnsEmpty()
    {
        var scaled = RegionMath.Scale(new PixelRect(0, 0, 0, 0), 1920, 1080, 3840, 2160);
        Assert.True(scaled.IsEmpty);
        Assert.Equal(new PixelRect(0, 0, 0, 0), scaled);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(50, 80)]
    public void EmptyRegion_IsUnchangedEvenWhenSourceShrinks(int x, int y)
    {
        var empty = new PixelRect(x, y, 0, 0);
        var scaled = RegionMath.Scale(empty, 1920, 1080, 960, 540);
        Assert.Equal(empty, scaled);
        Assert.True(scaled.IsEmpty);
    }
}

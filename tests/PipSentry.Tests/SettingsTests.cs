using System.Text.Json;
using Xunit;
using PipSentry.Capture;
using PipSentry.Services;

namespace PipSentry.Tests;

public class SettingsTests
{
    [Fact]
    public void RoundTrip_PreservesRegionAndUiOptions()
    {
        var original = new AppSettings
        {
            UseRegion = true,
            RegionX = 10,
            RegionY = 20,
            RegionWidth = 300,
            RegionHeight = 200,
            RegionSourceWidth = 3840,
            RegionSourceHeight = 2160,
            Theme = "Light",
            Language = "en-US",
            Fps = 60,
        };

        var json = JsonSerializer.Serialize(original);
        var copy = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(copy);
        Assert.True(copy!.UseRegion);
        Assert.Equal(new PixelRect(10, 20, 300, 200), copy.Region.GetValueOrDefault());
        Assert.Equal(3840, copy.RegionSourceWidth);
        Assert.Equal(2160, copy.RegionSourceHeight);
        Assert.Equal("Light", copy.Theme);
        Assert.Equal("en-US", copy.Language);
        Assert.Equal(60, copy.Fps);
    }

    [Theory]
    [InlineData("Dark", "zh-Hant")]
    [InlineData("Light", "zh-Hans")]
    [InlineData("Dark", "en-US")]
    public void RoundTrip_PreservesThemeAndLanguage(string theme, string language)
    {
        var original = new AppSettings { Theme = theme, Language = language };

        var copy = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(original));

        Assert.NotNull(copy);
        Assert.Equal(theme, copy!.Theme);
        Assert.Equal(language, copy.Language);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void RoundTrip_PreservesFps(int fps)
    {
        var copy = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(new AppSettings { Fps = fps }));

        Assert.NotNull(copy);
        Assert.Equal(fps, copy!.Fps);
    }

    [Fact]
    public void RoundTrip_WithRegionDisabled_KeepsRegionDisabled()
    {
        var original = new AppSettings { UseRegion = false, RegionWidth = 300, RegionHeight = 200 };

        var copy = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(original));

        Assert.NotNull(copy);
        Assert.False(copy!.UseRegion);
        Assert.Null(copy.Region);
    }

    [Fact]
    public void Region_IsNull_WhenUseRegionIsFalse()
    {
        var settings = new AppSettings { UseRegion = false, RegionWidth = 300, RegionHeight = 200 };

        Assert.Null(settings.Region);
    }

    [Fact]
    public void Region_IsNull_OnDefaultSettings()
    {
        Assert.Null(new AppSettings().Region);
    }

    [Fact]
    public void Region_IsComputedAndNotSerialized()
    {
        var settings = new AppSettings { UseRegion = true, RegionX = 1, RegionY = 2, RegionWidth = 30, RegionHeight = 40 };

        var json = JsonSerializer.Serialize(settings);

        // Region is [JsonIgnore]: it is rebuilt from the scalar fields, never persisted as a nested object.
        Assert.DoesNotContain("\"Region\"", json);
        Assert.Contains("\"RegionX\"", json);
        Assert.Equal(new PixelRect(1, 2, 30, 40), settings.Region.GetValueOrDefault());
    }
}

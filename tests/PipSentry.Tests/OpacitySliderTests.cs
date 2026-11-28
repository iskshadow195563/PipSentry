using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PipSentry.Tests;

public class OpacitySliderTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "PipSentry", "PipSentry.csproj")))
            dir = dir.Parent;
        if (dir is null) throw new InvalidOperationException("repo root not found");
        return dir.FullName;
    }

    private static string SliderMarkup()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "src", "PipSentry", "Views", "ShellWindow.xaml"));
        var match = Regex.Match(xaml, "<Slider[^>]*x:Name=\"OpacitySlider\"[^>]*>", RegexOptions.Singleline);
        Assert.True(match.Success, "OpacitySlider not found");
        return match.Value;
    }

    [Fact]
    public void ClickingTheTrackMovesTheThumb()
    {
        // Without this the default track repeat buttons race to the ends on a click.
        Assert.Contains("IsMoveToPointEnabled=\"True\"", SliderMarkup());
    }

    [Fact]
    public void SliderKeepsItsRangeAndTicks()
    {
        var markup = SliderMarkup();
        Assert.Contains("Minimum=\"0.25\"", markup);
        Assert.Contains("Maximum=\"1\"", markup);
        Assert.Contains("TickFrequency=\"0.05\"", markup);
    }

    [Fact]
    public void PercentageLabelHasAFixedWidth()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "src", "PipSentry", "Views", "ShellWindow.xaml"));
        var match = Regex.Match(xaml, "<TextBlock[^>]*x:Name=\"OpacityText\"[^>]*>", RegexOptions.Singleline);
        Assert.True(match.Success, "OpacityText not found");
        Assert.Contains("Width=\"44\"", match.Value);
        Assert.DoesNotContain("MinWidth", match.Value);
    }
}

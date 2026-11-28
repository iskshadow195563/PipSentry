using System;
using PipSentry.Services;
using Xunit;

namespace PipSentry.Tests;

public class UpdateServiceTests
{
    [Theory]
    [InlineData("v1.5.0", 1, 5, 0)]
    [InlineData("1.5.0", 1, 5, 0)]
    [InlineData("v1.6", 1, 6, 0)]
    [InlineData("v2.0.0-beta1", 2, 0, 0)]
    [InlineData("v1.5.10", 1, 5, 10)]
    public void TryParseTag_ReadsVersions(string tag, int major, int minor, int build)
    {
        Assert.True(UpdateService.TryParseTag(tag, out var version));
        Assert.Equal(new Version(major, minor, build), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v1")]
    [InlineData("v1.x")]
    public void TryParseTag_RejectsJunk(string tag)
    {
        Assert.False(UpdateService.TryParseTag(tag, out _));
    }

    [Theory]
    [InlineData("1.5.0", "1.5.1", true)]
    [InlineData("1.5.0", "1.6.0", true)]
    [InlineData("1.5.0", "2.0.0", true)]
    [InlineData("1.5.0", "1.5.0", false)]
    [InlineData("1.5.1", "1.5.0", false)]
    public void IsNewer_ComparesThreeParts(string current, string latest, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsNewer(Version.Parse(current), Version.Parse(latest)));
    }

    [Theory]
    [InlineData("https://github.com/iskshadow195563/PipSentry/releases/download/v1/PipSentry.exe", true)]
    [InlineData("https://objects.githubusercontent.com/x.exe", true)]
    [InlineData("https://evil.example.com/x.exe", false)]
    [InlineData("http://github.com/x.exe", false)]
    public void IsTrustedDownload_OnlyAcceptsHttpsGitHub(string url, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsTrustedDownload(new Uri(url)));
    }

    [Fact]
    public void Normalize_DropsRevision()
    {
        Assert.Equal(new Version(1, 5, 0), UpdateService.Normalize(new Version(1, 5, 0, 42)));
    }
}

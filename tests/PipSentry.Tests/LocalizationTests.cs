using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PipSentry.Tests;

public class LocalizationTests
{
    private const string LocalizationFolder = "src/PipSentry/Localization";
    private const string ThemesFolder = "src/PipSentry/Themes";
    private const string ViewsFolder = "src/PipSentry/Views";

    private static readonly Regex LocalizedStringPattern =
        new("<sys:String x:Key=\"([^\"]+)\">", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BrushKeyPattern =
        new("<(?:SolidColorBrush|LinearGradientBrush|RadialGradientBrush)\\s+x:Key=\"([^\"]+)\"",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ResourceKeyPattern =
        new("x:Key=\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DynamicResourcePattern =
        new("\\{DynamicResource\\s+([^\\s\\}]+)\\s*\\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void LanguageFiles_ShareTheSameNonEmptyKeySet()
    {
        var zhHant = LocalizationKeys("zh-Hant");
        var zhHans = LocalizationKeys("zh-Hans");
        var enUs = LocalizationKeys("en-US");

        Assert.True(zhHant.Count > 100, $"expected more than 100 entries in Lang.zh-Hant.xaml, found {zhHant.Count}");
        Assert.True(zhHans.Count > 100, $"expected more than 100 entries in Lang.zh-Hans.xaml, found {zhHans.Count}");
        Assert.True(enUs.Count > 100, $"expected more than 100 entries in Lang.en-US.xaml, found {enUs.Count}");

        AssertNoDuplicateKeys("Lang.zh-Hant.xaml", zhHant);
        AssertNoDuplicateKeys("Lang.zh-Hans.xaml", zhHans);
        AssertNoDuplicateKeys("Lang.en-US.xaml", enUs);

        AssertKeySetsEqual("Lang.zh-Hant.xaml", zhHant, "Lang.zh-Hans.xaml", zhHans);
        AssertKeySetsEqual("Lang.zh-Hant.xaml", zhHant, "Lang.en-US.xaml", enUs);
    }

    [Fact]
    public void ThemeBrushKeys_DoNotCollideWithLocalizationKeys()
    {
        var localized = LocalizationKeys("zh-Hant");

        foreach (var theme in new[] { "Dark", "Light" })
        {
            var brushKeys = Matches($"{ThemesFolder}/{theme}.xaml", BrushKeyPattern);
            Assert.True(brushKeys.Count > 0, $"{theme}.xaml declares no solid or gradient brush keys");

            var collisions = brushKeys
                .Intersect(localized, StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();

            // A theme dictionary merged after a language dictionary would shadow the string
            // with a brush (the old "Live" bug), so keys must stay disjoint.
            Assert.True(collisions.Count == 0,
                $"{theme}.xaml brush keys also exist as localized strings: [{string.Join(", ", collisions)}]");
        }
    }

    [Fact]
    public void EveryReferencedDynamicResource_IsDefinedSomewhere()
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var language in new[] { "zh-Hant", "zh-Hans", "en-US" })
        {
            known.UnionWith(LocalizationKeys(language));
        }

        known.UnionWith(Matches($"{ThemesFolder}/Dark.xaml", BrushKeyPattern));
        // Tokens.xaml is merged before Dark.xaml and holds fonts and icon geometries, which
        // also resolve through {DynamicResource}; accept those keys as defined.
        known.UnionWith(Matches($"{ThemesFolder}/Tokens.xaml", ResourceKeyPattern));

        var files = Directory
            .GetFiles(RepoPath(ViewsFolder), "*.xaml", SearchOption.TopDirectoryOnly)
            .Concat(new[] { RepoPath($"{ThemesFolder}/Controls.xaml") })
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.True(files.Count > 1, "no view dictionaries were found; the repository scan is broken");

        var unresolved = new List<string>();
        var scanned = 0;

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (Match match in DynamicResourcePattern.Matches(text).Cast<Match>())
            {
                scanned++;
                var key = match.Groups[1].Value;
                if (!known.Contains(key))
                {
                    unresolved.Add($"{Path.GetFileName(file)} -> {key}");
                }
            }
        }

        Assert.True(scanned > 0, "no {DynamicResource X} usages were found; the scan is probably broken");
        Assert.True(unresolved.Count == 0,
            "unresolved {DynamicResource} keys: " +
            string.Join(", ", unresolved.Distinct(StringComparer.Ordinal).OrderBy(entry => entry, StringComparer.Ordinal)));
    }

    private static IReadOnlyList<string> LocalizationKeys(string language) =>
        Matches($"{LocalizationFolder}/Lang.{language}.xaml", LocalizedStringPattern);

    private static IReadOnlyList<string> Matches(string relativePath, Regex pattern) =>
        pattern.Matches(File.ReadAllText(RepoPath(relativePath)))
            .Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .ToList();

    private static void AssertNoDuplicateKeys(string fileName, IReadOnlyList<string> keys)
    {
        var duplicates = keys
            .GroupBy(key => key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.True(duplicates.Count == 0, $"{fileName} repeats keys: [{string.Join(", ", duplicates)}]");
    }

    private static void AssertKeySetsEqual(
        string leftName, IReadOnlyList<string> left,
        string rightName, IReadOnlyList<string> right)
    {
        var missing = left.Except(right, StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).ToList();
        var extra = right.Except(left, StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0 && extra.Count == 0,
            $"{rightName} differs from {leftName}. missing: [{string.Join(", ", missing)}] extra: [{string.Join(", ", extra)}]");
    }

    private static string RepoPath(string relativePath) =>
        Path.Combine(RepositoryLayout.Root(), relativePath.Replace('/', Path.DirectorySeparatorChar));
}

/// <summary>Walks up from the test output folder until the application project file is found.</summary>
internal static class RepositoryLayout
{
    private const string MarkerRelativePath = "src/PipSentry/PipSentry.csproj";

    public static string Root()
    {
        var marker = Path.Combine("src", "PipSentry", "PipSentry.csproj");
        var starts = new List<string>();

        var configured = Environment.GetEnvironmentVariable("PIPSENTRY_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            starts.Add(configured!);
        }

        starts.Add(AppContext.BaseDirectory);
        starts.Add(Directory.GetCurrentDirectory());

        foreach (var start in starts)
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, marker)))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException(
            $"could not locate the repository root: no parent of '{AppContext.BaseDirectory}' contains '{MarkerRelativePath}'. " +
            "Set PIPSENTRY_REPO_ROOT to the repository root when the tests run outside the checkout.");
    }
}

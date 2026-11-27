using System;
using System.Globalization;
using System.Windows;

namespace PipSentry.Platform;

public static class Loc
{
    public static readonly string[] Available = { "zh-Hant", "zh-Hans", "en-US" };

    public static string Current { get; private set; } = "zh-Hant";

    public static void Apply(string? culture)
    {
        Current = Normalize(culture);
        var uri = new Uri($"Localization/Lang.{Current}.xaml", UriKind.Relative);
        var dict = new ResourceDictionary { Source = uri };
        var merged = Application.Current.Resources.MergedDictionaries;

        for (var i = 0; i < merged.Count; i++)
        {
            var source = merged[i].Source?.OriginalString ?? string.Empty;
            if (source.Contains("Localization/Lang.", StringComparison.OrdinalIgnoreCase))
            {
                merged[i] = dict;
                return;
            }
        }

        merged.Add(dict);
    }

    public static string Normalize(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture)) return DetectSystem();

        var value = culture.Trim().ToLowerInvariant();
        if (value.StartsWith("zh-hant") || value is "zh-tw" or "zh-hk" or "zh-mo" || value.Contains("hant")) return "zh-Hant";
        if (value.StartsWith("zh")) return "zh-Hans";
        if (value.StartsWith("en")) return "en-US";
        return "en-US";
    }

    public static string DetectSystem()
    {
        var name = CultureInfo.CurrentUICulture.Name;
        var normalized = Normalize(name);
        return normalized;
    }

    public static string KeyFor(string culture) => culture switch
    {
        "zh-Hans" => "LangZhHans",
        "en-US" => "LangEn",
        _ => "LangZhHant",
    };
}

/// <summary>Shorthand accessor for UI strings.</summary>
public static class L
{
    public static string Get(string key)
    {
        try
        {
            return Application.Current?.TryFindResource(key) as string ?? key;
        }
        catch
        {
            return key;
        }
    }

    public static string Format(string key, params object?[] args)
    {
        var template = Get(key);
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
    }
}

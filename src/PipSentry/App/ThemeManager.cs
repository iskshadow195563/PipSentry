using System;
using System.Windows;

namespace PipSentry.Platform;

public static class ThemeManager
{
    public static bool IsDark { get; private set; } = true;

    public static string Current => IsDark ? "Dark" : "Light";

    public static void Apply(string? theme)
    {
        IsDark = !string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase);
        var uri = new Uri(IsDark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative);
        var palette = new ResourceDictionary { Source = uri };
        var merged = Application.Current.Resources.MergedDictionaries;

        for (var i = 0; i < merged.Count; i++)
        {
            var source = merged[i].Source?.OriginalString ?? string.Empty;
            if (source.EndsWith("Dark.xaml", StringComparison.OrdinalIgnoreCase) ||
                source.EndsWith("Light.xaml", StringComparison.OrdinalIgnoreCase))
            {
                merged[i] = palette;
                return;
            }
        }

        merged.Add(palette);
    }
}

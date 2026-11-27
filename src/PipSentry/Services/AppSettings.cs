using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PipSentry.Capture;

namespace PipSentry.Services;

public sealed class AppSettings
{
    public string LastSourceKey { get; set; } = string.Empty;
    public string LastSourceTitle { get; set; } = string.Empty;

    public bool UseRegion { get; set; }
    public int RegionX { get; set; }
    public int RegionY { get; set; }
    public int RegionWidth { get; set; }
    public int RegionHeight { get; set; }
    public int RegionSourceWidth { get; set; }
    public int RegionSourceHeight { get; set; }

    public string Theme { get; set; } = "Dark";
    public string Language { get; set; } = string.Empty;
    public int Fps { get; set; } = 30;
    public string ScaleMode { get; set; } = "Fit";
    public double MonitorOpacity { get; set; } = 1.0;
    public bool Topmost { get; set; } = true;
    public bool ClickThrough { get; set; }
    public bool ShowBorder { get; set; } = true;
    public bool LockAspect { get; set; } = true;
    public bool CaptureCursor { get; set; }

    public bool KeepAlive { get; set; } = true;
    public bool PreventSleep { get; set; } = true;
    public bool OptimizeTargetApp { get; set; }
    public bool NudgeTarget { get; set; }


    public bool HasMonitorBounds { get; set; }
    public double MonitorLeft { get; set; }
    public double MonitorTop { get; set; }
    public double MonitorWidth { get; set; }
    public double MonitorHeight { get; set; }

    [JsonIgnore]
    public PixelRect? Region => UseRegion && RegionWidth > 0 && RegionHeight > 0
        ? new PixelRect(RegionX, RegionY, RegionWidth, RegionHeight)
        : null;

    public void SetRegion(PixelRect? region)
    {
        if (region is { IsEmpty: false } value)
        {
            UseRegion = true;
            RegionX = value.X;
            RegionY = value.Y;
            RegionWidth = value.Width;
            RegionHeight = value.Height;
        }
        else
        {
            UseRegion = false;
            RegionX = RegionY = RegionWidth = RegionHeight = 0;
        }
    }
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string Directory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PipSentry");

    public static string FilePath => Path.Combine(Directory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, Options);
                if (settings is not null) return settings;
            }
        }
        catch (Exception ex)
        {
            AppLog.Write("settings load failed: " + ex.Message);
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var json = JsonSerializer.Serialize(settings, Options);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);
            File.Copy(temp, FilePath, true);
            File.Delete(temp);
        }
        catch (Exception ex)
        {
            AppLog.Write("settings save failed: " + ex.Message);
        }
    }
}

public static class AppLog
{
    private static readonly object Gate = new();

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                System.IO.Directory.CreateDirectory(SettingsStore.Directory);
                var path = Path.Combine(SettingsStore.Directory, "log.txt");
                if (File.Exists(path) && new FileInfo(path).Length > 262144)
                {
                    File.Delete(path);
                }

                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
        }
    }
}

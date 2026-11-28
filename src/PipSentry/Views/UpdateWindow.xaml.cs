using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using PipSentry.Platform;
using PipSentry.Services;

namespace PipSentry.Views;

public partial class UpdateWindow : Window
{
    private readonly ReleaseInfo _release;
    private readonly CancellationTokenSource _cancellation = new();
    private bool _busy;

    public UpdateWindow(ReleaseInfo release)
    {
        InitializeComponent();
        _release = release;

        var current = UpdateService.Normalize(UpdateService.CurrentVersion);
        VersionText.Text = L.Format("UpdateAvailable", current.ToString(3), release.Version.ToString(3));
        NotesText.Text = CleanNotes(release.Notes);

        SourceInitialized += (_, _) =>
        {
            WindowEffects.ApplyDarkChrome(this, true);
            WindowEffects.ApplyRoundedCorners(this);
        };

        Closed += (_, _) => _cancellation.Cancel();
    }

    private static string CleanNotes(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return string.Empty;

        var text = notes.Replace("\r\n", "\n");
        var builder = new StringBuilder(text.Length);
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.TrimEnd();
            if (trimmed.StartsWith("![", StringComparison.Ordinal)) continue;
            if (trimmed.StartsWith("<", StringComparison.Ordinal)) continue;
            builder.AppendLine(trimmed);
        }

        var result = builder.ToString().Trim();
        return result.Length > 4000 ? result[..4000] + "…" : result;
    }

    private void OnLater(object sender, RoutedEventArgs e) => Close();

    private void OnSkip(object sender, RoutedEventArgs e)
    {
        App.Settings.SkippedUpdate = _release.Tag;
        SettingsStore.Save(App.Settings);
        Close();
    }

    private void OnOpenPage(object sender, RoutedEventArgs e)
    {
        if (Uri.TryCreate(_release.PageUrl, UriKind.Absolute, out var uri))
        {
            Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
        }
    }

    private async void OnDownload(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        var asset = ChooseAsset();
        if (asset is null)
        {
            MessageBox.Show(L.Get("UpdateNoAsset"), L.Get("UpdateTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _busy = true;
        DownloadButton.IsEnabled = false;
        SkipButton.IsEnabled = false;
        Progress.Visibility = Visibility.Visible;
        StatusText.Visibility = Visibility.Visible;

        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PipSentry", "updates");
            var target = Path.Combine(folder, asset.Name);
            var progress = new Progress<double>(value =>
            {
                Progress.Value = value;
                StatusText.Text = L.Format("UpdateDownloading", (int)value);
            });

            await UpdateService.DownloadAsync(asset, target, progress, _cancellation.Token);

            StatusText.Text = L.Get("UpdateReady");
            Progress.Value = 100;
            Install(target, asset.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _busy = false;
            DownloadButton.IsEnabled = true;
            SkipButton.IsEnabled = true;
            Progress.Visibility = Visibility.Collapsed;
            StatusText.Text = L.Format("UpdateFailed", ex.Message);
            AppLog.Write("update failed: " + ex);
        }
    }

    private ReleaseAsset? ChooseAsset()
    {
        var installed = IsInstalled();
        if (installed) return _release.Msi ?? _release.Portable;
        return _release.Portable ?? _release.Msi;
    }

    private static bool IsInstalled()
    {
        var path = Environment.ProcessPath ?? string.Empty;
        return path.Contains(@"\Program Files", StringComparison.OrdinalIgnoreCase);
    }

    private void Install(string file, bool isMsi)
    {
        if (isMsi)
        {
            Process.Start(new ProcessStartInfo("msiexec.exe", "/i \"" + file + "\"") { UseShellExecute = true });
            Application.Current.Shutdown();
            return;
        }

        var target = Environment.ProcessPath;
        if (string.IsNullOrEmpty(target))
        {
            Application.Current.Shutdown();
            return;
        }

        var script = Path.Combine(Path.GetTempPath(), "PipSentry-update.cmd");
        var pid = Environment.ProcessId;
        var lines = new[]
        {
            "@echo off",
            ":wait",
            "tasklist /FI \"PID eq " + pid + "\" 2>nul | find \"" + pid + "\" >nul",
            "if not errorlevel 1 (",
            "  timeout /t 1 /nobreak >nul",
            "  goto wait",
            ")",
            "copy /Y \"" + file + "\" \"" + target + "\" >nul",
            "start \"\" \"" + target + "\"",
            "del \"%~f0\"",
        };
        File.WriteAllLines(script, lines);

        Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        });

        Application.Current.Shutdown();
    }
}

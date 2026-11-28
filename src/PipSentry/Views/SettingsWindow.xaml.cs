using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PipSentry.Platform;
using PipSentry.Services;

namespace PipSentry.Views;

public partial class SettingsWindow : Window
{
    private sealed record Row(ReleaseInfo Release, string Tag, string DateText, string Badge, Visibility BadgeVisibility);

    private readonly CancellationTokenSource _cancellation = new();
    private List<ReleaseInfo> _releases = new();
    private Row? _selected;
    private bool _busy;

    public SettingsWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            WindowEffects.ApplyDarkChrome(this, true);
            WindowEffects.ApplyRoundedCorners(this);
        };

        AutoCheckToggle.IsChecked = App.Settings.AutoCheckUpdates;
        AutoCheckToggle.Checked += OnAutoCheckChanged;
        AutoCheckToggle.Unchecked += OnAutoCheckChanged;

        var current = UpdateService.Normalize(UpdateService.CurrentVersion);
        CurrentVersionText.Text = L.Format("SettingsCurrentVersion", current.ToString(3));

        Loaded += async (_, _) => await ReloadAsync();
        Closed += (_, _) => _cancellation.Cancel();
    }

    private void OnAutoCheckChanged(object sender, RoutedEventArgs e)
    {
        App.Settings.AutoCheckUpdates = AutoCheckToggle.IsChecked == true;
        SettingsStore.Save(App.Settings);
    }

    private async void OnCheck(object sender, RoutedEventArgs e) => await ReloadAsync();

    private async System.Threading.Tasks.Task ReloadAsync()
    {
        CheckButton.IsEnabled = false;
        CheckStatusText.Text = L.Get("UpdateChecking");
        try
        {
            var releases = await UpdateService.GetReleasesAsync(cancellationToken: _cancellation.Token);
            _releases = releases;
            var current = UpdateService.Normalize(UpdateService.CurrentVersion);

            var rows = new List<Row>();
            foreach (var release in releases)
            {
                var isCurrent = release.Version == current;
                var isLatest = rows.Count == 0;
                var badge = isCurrent ? L.Get("SettingsCurrentBadge") : isLatest ? L.Get("SettingsLatestBadge") : string.Empty;
                rows.Add(new Row(release, release.Tag,
                    release.PublishedAt?.ToLocalTime().ToString("yyyy-MM-dd") ?? string.Empty,
                    badge, string.IsNullOrEmpty(badge) ? Visibility.Collapsed : Visibility.Visible));
            }

            ReleaseList.ItemsSource = rows;
            var pick = rows.FirstOrDefault(r => r.Release.Version == current) ?? rows.FirstOrDefault();
            ReleaseList.SelectedItem = pick;

            if (rows.Count == 0)
            {
                CheckStatusText.Text = L.Get("SettingsNoReleases");
            }
            else
            {
                var newest = rows[0].Release.Version;
                CheckStatusText.Text = UpdateService.IsNewer(current, newest)
                    ? L.Format("UpdateAvailable", current.ToString(3), newest.ToString(3))
                    : L.Get("UpdateLatest");
            }
        }
        catch (Exception ex)
        {
            CheckStatusText.Text = L.Format("UpdateFailed", ex.Message);
            AppLog.Write("settings releases: " + ex.Message);
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }

    private void OnReleaseSelected(object sender, SelectionChangedEventArgs e)
    {
        _selected = ReleaseList.SelectedItem as Row;
        NotesText.Text = _selected is null ? string.Empty : CleanNotes(_selected.Release.Notes);
        InstallButton.IsEnabled = _selected is not null;

        var current = UpdateService.Normalize(UpdateService.CurrentVersion);
        InstallButton.Content = _selected is not null && _selected.Release.Version < current
            ? L.Get("SettingsDowngrade")
            : L.Get("UpdateDownload");
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
        return result.Length > 6000 ? result[..6000] + "…" : result;
    }

    private void OnOpenPage(object sender, RoutedEventArgs e)
    {
        var page = _selected?.Release.PageUrl;
        if (!string.IsNullOrEmpty(page) && Uri.TryCreate(page, UriKind.Absolute, out var uri))
        {
            Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private async void OnInstall(object sender, RoutedEventArgs e)
    {
        if (_busy || _selected is null) return;

        var installed = IsInstalled();
        var asset = installed
            ? _selected.Release.Msi ?? _selected.Release.Portable
            : _selected.Release.Portable ?? _selected.Release.Msi;

        if (asset is null)
        {
            MessageBox.Show(L.Get("UpdateNoAsset"), L.Get("SettingsTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _busy = true;
        InstallButton.IsEnabled = false;
        CloseButton.IsEnabled = false;
        PageButton.IsEnabled = false;
        Progress.Visibility = Visibility.Visible;
        DownloadStatusText.Visibility = Visibility.Visible;

        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PipSentry", "updates");
            var target = Path.Combine(folder, asset.Name);
            var progress = new Progress<double>(value =>
            {
                Progress.Value = value;
                DownloadStatusText.Text = L.Format("UpdateDownloading", (int)value);
            });

            await UpdateService.DownloadAsync(asset, target, progress, _cancellation.Token);

            DownloadStatusText.Text = L.Get("UpdateReady");
            Progress.Value = 100;
            UpdateService.LaunchInstaller(target, asset.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _busy = false;
            InstallButton.IsEnabled = true;
            CloseButton.IsEnabled = true;
            PageButton.IsEnabled = true;
            Progress.Visibility = Visibility.Collapsed;
            DownloadStatusText.Text = L.Format("UpdateFailed", ex.Message);
            AppLog.Write("update failed: " + ex);
        }
    }

    internal void SeedSample()
    {
        var current = UpdateService.Normalize(UpdateService.CurrentVersion);
        var rows = new List<Row>();
        for (var i = 0; i < 4; i++)
        {
            var version = new Version(current.Major, current.Minor + i + 1, 0);
            var badge = i == 0 ? L.Get("SettingsLatestBadge") : string.Empty;
            rows.Add(new Row(
                new ReleaseInfo(version, "v" + version.ToString(3), "PipSentry " + version.ToString(3),
                    "新增\n- 範例項目\n\n修正\n- 範例修正", "https://github.com/iskshadow195563/PipSentry/releases", null, null,
                    DateTime.UtcNow.AddDays(-i * 3)),
                "v" + version.ToString(3), DateTime.UtcNow.AddDays(-i * 3).ToString("yyyy-MM-dd"),
                badge, string.IsNullOrEmpty(badge) ? Visibility.Collapsed : Visibility.Visible));
        }

        ReleaseList.ItemsSource = rows;
        ReleaseList.SelectedItem = rows[0];
        CheckStatusText.Text = L.Format("UpdateAvailable", current.ToString(3), rows[0].Release.Version.ToString(3));
    }

    private static bool IsInstalled()
    {
        var path = Environment.ProcessPath ?? string.Empty;
        return path.Contains(@"\Program Files", StringComparison.OrdinalIgnoreCase);
    }
}

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PipSentry.Platform;
using PipSentry.Services;

namespace PipSentry.Views;

public partial class HotkeyWindow : Window
{
    private readonly List<Row> _rows = new();
    private Row? _recording;

    private sealed record Row(string Action, string LabelKey, Func<string> Get, Action<string> Set, TextBlock Value, Button Record);

    public HotkeyWindow()
    {
        InitializeComponent();
        BuildRows();
        PreviewKeyDown += OnPreviewKeyDown;
        SourceInitialized += (_, _) =>
        {
            WindowEffects.ApplyDarkChrome(this, true);
            WindowEffects.ApplyRoundedCorners(this);
        };
        Loaded += (_, _) => Focus();
    }

    private void BuildRows()
    {
        AddRow("toggle", "HotkeyToggle", () => App.Settings.HotkeyToggle, v => App.Settings.HotkeyToggle = v);
        AddRow("pause", "HotkeyPause", () => App.Settings.HotkeyPause, v => App.Settings.HotkeyPause = v);
        AddRow("shot", "HotkeyShot", () => App.Settings.HotkeyScreenshot, v => App.Settings.HotkeyScreenshot = v);
        AddRow("back", "HotkeyBack", () => App.Settings.HotkeyBack, v => App.Settings.HotkeyBack = v);
        AddRow("through", "HotkeyThrough", () => App.Settings.HotkeyThrough, v => App.Settings.HotkeyThrough = v);
        AddRow("next", "HotkeyNext", () => App.Settings.HotkeyNextPreset, v => App.Settings.HotkeyNextPreset = v);
    }

    private void AddRow(string action, string labelKey, Func<string> get, Action<string> set)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = L.Get(labelKey),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12.5,
            Foreground = (System.Windows.Media.Brush)FindResource("Text1"),
        };
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        var value = new TextBlock
        {
            Text = get(),
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new System.Windows.Media.FontFamily("Consolas, Cascadia Mono, Segoe UI"),
            FontSize = 12,
            Foreground = (System.Windows.Media.Brush)FindResource("Accent"),
            Margin = new Thickness(12, 0, 12, 0),
            MinWidth = 96,
            TextAlignment = TextAlignment.Right,
        };
        Grid.SetColumn(value, 1);
        grid.Children.Add(value);

        var record = new Button
        {
            Content = L.Get("HotkeyRecord"),
            Style = (Style)FindResource("GhostBtn"),
            Height = 28,
            Padding = new Thickness(12, 0, 12, 0),
            FontSize = 11.5,
            Tag = action,
            Cursor = Cursors.Hand,
        };
        var row = new Row(action, labelKey, get, set, value, record);
        record.Click += (_, _) => BeginRecording(row);
        Grid.SetColumn(record, 2);
        grid.Children.Add(record);

        Rows.Children.Add(grid);
        _rows.Add(row);
    }

    private void BeginRecording(Row row)
    {
        EndRecording();
        _recording = row;
        row.Record.Content = L.Get("HotkeyRecording");
        row.Record.Foreground = (System.Windows.Media.Brush)FindResource("Warn");
        Focus();
    }

    private void EndRecording()
    {
        if (_recording is null) return;
        _recording.Record.Content = L.Get("HotkeyRecord");
        _recording.Record.Foreground = (System.Windows.Media.Brush)FindResource("Text1");
        _recording = null;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_recording is null || e.Key == Key.System) return;

        if (e.Key == Key.Escape)
        {
            EndRecording();
            e.Handled = true;
            return;
        }

        if (HotkeyBinding.IsModifierKey(e.Key))
        {
            e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            return;
        }

        var text = HotkeyBinding.Format(modifiers, e.Key);
        _recording.Set(text);
        _recording.Value.Text = text;
        EndRecording();
        e.Handled = true;
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        EndRecording();
        SettingsStore.Save(App.Settings);
        if (!App.RegisterHotkeys()) App.Toast(L.Get("MsgHotkeyBusy"));
        Close();
    }
}

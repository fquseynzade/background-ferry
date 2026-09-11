using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Threading.Tasks;
using BackgroundFerry.Core;
using BackgroundFerry.Windows;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using static BackgroundFerry.App.UiText;

namespace BackgroundFerry.App;

public sealed class AppChoice : INotifyPropertyChanged
{
    public string Process { get; init; } = "";
    public string ProcessFile => Process + ".exe";
    private string? displayName;
    private ImageSource icon = AppIdentityReader.FallbackIcon;
    public string Name => displayName ?? Process;
    public ImageSource Icon => icon;
    private int metadataProcessId;
    private DateTime nextMetadataAttempt;
    private bool metadataPending;
    public async Task RefreshIdentityAsync(int processId)
    {
        if (metadataPending || (metadataProcessId == processId && DateTime.UtcNow < nextMetadataAttempt)) return;
        metadataPending = true;
        metadataProcessId = processId;
        try
        {
            var identity = await Task.Run(() => AppIdentityReader.Read(processId, Process));
            nextMetadataAttempt = DateTime.UtcNow.AddSeconds(identity is null || identity.Icon == AppIdentityReader.FallbackIcon ? 30 : 300);
            if (identity is null) return;
            displayName = identity.Name; icon = identity.Icon;
            PropertyChanged?.Invoke(this, new(nameof(Name)));
            PropertyChanged?.Invoke(this, new(nameof(Icon)));
        }
        catch (Exception ex) { Storage.Log(ex); nextMetadataAttempt = DateTime.UtcNow.AddSeconds(30); }
        finally { metadataPending = false; }
    }
    private bool selected;
    private bool canPrioritize = true;
    public bool Selected { get => selected; set { selected = value; PropertyChanged?.Invoke(this, new(nameof(Selected))); } }
    public bool CanPrioritize { get => canPrioritize; set { canPrioritize = value; PropertyChanged?.Invoke(this, new(nameof(CanPrioritize))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class MainWindow : Window
{
    private readonly ObservableCollection<AppChoice> choices = new();
    private readonly MixService engine = new();
    private readonly DispatcherTimer timer;
    private readonly DispatcherTimer saveTimer;
    private readonly Forms.NotifyIcon tray;
    private readonly Forms.ToolStripMenuItem trayToggle;
    private readonly Forms.ToolStripItem trayOpen;
    private readonly Forms.ToolStripItem trayExit;
    private MixSettings saved;
    private Process? guardian;
    private bool loading = true;
    private bool shutdown;
    private bool toldAboutTray;
    private int ticks;
    private string noticeKey = "";
    private ToolTip? keyboardTip;
    public bool MinimizedAtStart { get; }

    public MainWindow(bool minimized)
    {
        InitializeComponent();
        try { saved = Storage.LoadSettings(); }
        catch (Exception ex) { Storage.Log(ex); saved = new(); noticeKey = "BadSettings"; }
        UiText.Apply(saved.Language);
        LanguagePicker.SelectedIndex = saved.Language == "en" ? 1 : 0;
        MinimizedAtStart = minimized || saved.StartMinimized;
        MusicPicker.ItemsSource = choices;
        PriorityList.ItemsSource = choices;
        foreach (string process in saved.PriorityProcesses.Append(saved.MusicProcess).Where(p => p.Length > 0).Distinct())
            choices.Add(new AppChoice { Process = process, Selected = saved.PriorityProcesses.Contains(process) });
        MusicPicker.SelectedValue = saved.MusicProcess;
        ModePicker.SelectedIndex = (int)saved.Mode;
        DuckSlider.Value = saved.DuckPercent;
        ThresholdSlider.Value = saved.ThresholdDb;
        AttackSlider.Value = saved.AttackMs;
        HoldSlider.Value = saved.HoldMs;
        ReleaseSlider.Value = saved.ReleaseMs;
        StartupCheck.IsChecked = saved.StartWithWindows;
        MinimizedCheck.IsChecked = saved.StartMinimized;
        var menu = new Forms.ContextMenuStrip();
        trayOpen = menu.Items.Add(T("Open"), null, (_, _) => ShowWindow());
        trayToggle = new Forms.ToolStripMenuItem(T("Start"), null, (_, _) => Toggle());
        menu.Items.Add(trayToggle);
        menu.Items.Add(new Forms.ToolStripSeparator());
        trayExit = menu.Items.Add(T("Exit"), null, (_, _) => { Shutdown(); Application.Current.Shutdown(); });
        tray = new Forms.NotifyIcon { Text = "Background Ferry", Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => ShowWindow();
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += Tick;
        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); Save(); };
        Closing += (_, e) => { if (!shutdown) { e.Cancel = true; Hide(); if (!toldAboutTray) { tray.ShowBalloonTip(3000, T("TrayTitle"), T("TrayText"), Forms.ToolTipIcon.Info); toldAboutTray = true; } } };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.Control) { Toggle(); e.Handled = true; } };
        loading = false;
        UpdateChoices();
        UpdateLabels();
        UpdateStatus();
        timer.Start();
    }

    private MixSettings ReadSettings() => new()
    {
        MusicProcess = (MusicPicker.SelectedItem as AppChoice)?.Process ?? "",
        PriorityProcesses = choices.Where(c => c.Selected && c.CanPrioritize).Select(c => c.Process).ToArray(),
        Mode = (MixMode)Math.Max(0, ModePicker.SelectedIndex), DuckPercent = DuckSlider.Value,
        ThresholdDb = ThresholdSlider.Value, AttackMs = AttackSlider.Value, HoldMs = HoldSlider.Value,
        ReleaseMs = ReleaseSlider.Value, StartWithWindows = StartupCheck.IsChecked == true,
        StartMinimized = MinimizedCheck.IsChecked == true, Language = UiText.Language
    };

    private void Save() { saved = ReadSettings(); Storage.SaveSettings(saved); }
    private void EnsureGuardian()
    {
        if (guardian is { HasExited: false }) return;
        guardian?.Dispose();
        using var current = Process.GetCurrentProcess();
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        start.ArgumentList.Add("--watch"); start.ArgumentList.Add(Environment.ProcessId.ToString());
        start.ArgumentList.Add(current.StartTime.ToUniversalTime().Ticks.ToString()); start.ArgumentList.Add(Storage.DirectoryPath);
        guardian = Process.Start(start) ?? throw new InvalidOperationException(T("GuardianError"));
    }

    private void Toggle()
    {
        if (engine.Enabled) { engine.Stop(); SetNotice("Restored"); }
        else
        {
            try { var settings = ReadSettings(); settings.Validate(); EnsureGuardian(); Save(); engine.Start(settings); SetNotice(""); }
            catch (ArgumentException) { SetNotice("SelectSources"); return; }
        }
        UpdateStatus();
    }
    private void Toggle_Click(object sender, RoutedEventArgs e) => Toggle();
    private void Music_Changed(object sender, SelectionChangedEventArgs e) { if (!loading) { UpdateChoices(); SettingsChanged(); } }
    private void Priority_Changed(object sender, RoutedEventArgs e) { if (!loading) SettingsChanged(); }
    private void Setting_Changed(object sender, RoutedEventArgs e) { if (!loading) { UpdateLabels(); SettingsChanged(); } }

    private void SettingsChanged()
    {
        if (engine.Enabled) { engine.Stop(); SetNotice("SettingsChanged"); UpdateStatus(); }
        saveTimer.Stop(); saveTimer.Start();
    }

    private void Startup_Changed(object sender, RoutedEventArgs e)
    {
        if (loading) return;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (StartupCheck.IsChecked == true) key.SetValue("BackgroundFerry", $"\"{Environment.ProcessPath}\" --minimized");
            else key.DeleteValue("BackgroundFerry", false);
            Preference_Changed(sender, e);
        }
        catch (Exception ex) { Storage.Log(ex); loading = true; StartupCheck.IsChecked = false; loading = false; SetNotice("StartupError"); }
    }

    private void UpdateChoices()
    {
        loading = true;
        string music = (MusicPicker.SelectedItem as AppChoice)?.Process ?? saved.MusicProcess;
        foreach (var choice in choices) { choice.CanPrioritize = choice.Process != music; if (!choice.CanPrioritize) choice.Selected = false; }
        loading = false;
    }

    private void Tick(object? sender, EventArgs e)
    {
        if (engine.Snapshot.Error is not null) { SetNotice("AudioError"); UpdateStatus(); return; }
        if (++ticks % 10 != 0) return;
        loading = true;
        foreach (var level in engine.Levels)
        {
            var choice = choices.FirstOrDefault(c => c.Process == level.Process);
            if (choice is null) { choice = new AppChoice { Process = level.Process }; choices.Add(choice); }
            _ = choice.RefreshIdentityAsync(level.ProcessId);
        }
        loading = false;
        UpdateChoices();
        var music = engine.Levels.FirstOrDefault(l => l.Process == (MusicPicker.SelectedItem as AppChoice)?.Process);
        MusicMeter.Value = (music?.Peak ?? 0) * 100;
        MusicLevel.Text = music == null ? T("Waiting") : $"{T("Volume")} {music.Volume:P0}  ·  {T(music.Active ? "Playing" : "Idle")}";
        if (engine.OverrideCount > 0) SetNotice("Override");
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        ToggleButton.Content = T(engine.Enabled ? "Stop" : "Start");
        trayToggle.Text = T(engine.Enabled ? "Stop" : "Start");
        trayOpen.Text = T("Open"); trayExit.Text = T("Exit");
        tray.Text = "Background Ferry · " + T(engine.Phase);
        Status.Text = T(engine.Enabled ? engine.Phase : "Ready");
        StatusDetail.Text = engine.Enabled ? $"{20 * Math.Log10(Math.Max(engine.PriorityPeak, 0.000001)):F0} dBFS · {engine.OverrideCount} {T("Overrides")}" : "Ctrl+M";
        GainLabel.Text = $"{T("Gain")}  {engine.Gain:P0}";
        GainMeter.Value = engine.Gain * 100;
    }

    private void UpdateLabels()
    {
        DuckLabel.Text = $"{T("Duck")} · {DuckSlider.Value:0}%";
        ThresholdLabel.Text = $"{T("Sensitivity")} · {ThresholdSlider.Value:0} dBFS";
        AttackLabel.Text = $"{T("Attack")} · {AttackSlider.Value:0} {T("Ms")}";
        HoldLabel.Text = $"{T("Hold")} · {HoldSlider.Value:0} {T("Ms")}";
        ReleaseLabel.Text = $"{T("Release")} · {ReleaseSlider.Value:0} {T("Ms")}";
        Notice.Text = T(noticeKey);
    }

    private void ShowWindow() { Show(); WindowState = WindowState.Normal; Activate(); }
    public void EmergencyStop() { engine.Stop(); UpdateStatus(); }
    public void Shutdown()
    {
        if (shutdown) return;
        shutdown = true;
        timer.Stop(); saveTimer.Stop();
        try { Save(); } catch (Exception ex) { Storage.Log(ex); }
        try { engine.Dispose(); } catch (Exception ex) { Storage.Log(ex); }
        tray.Visible = false; tray.Dispose(); guardian?.Dispose();
    }

    private void SetNotice(string key) { noticeKey = key; Notice.Text = T(key); }
    private void Preference_Changed(object sender, RoutedEventArgs e) { if (!loading) { saveTimer.Stop(); saveTimer.Start(); } }
    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (loading) return;
        UiText.Apply(LanguagePicker.SelectedIndex == 1 ? "en" : "ru");
        UpdateLabels(); UpdateStatus(); ticks = 9; Tick(null, EventArgs.Empty);
        TabsNotice.Text = "";
        Preference_Changed(sender, e);
    }
    private void SetLanguage_Click(object sender, RoutedEventArgs e) => LanguagePicker.SelectedIndex = (string)((Button)sender).Tag == "en" ? 1 : 0;
    private void Navigate(object sender, RoutedEventArgs e)
    {
        if (MixerPage is null) return;
        MixerPage.Visibility = MixerNav.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        TabsPage.Visibility = TabsNav.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = SettingsNav.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        MixFooter.Visibility = TabsNav.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
    }
    private void Info_Focus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var button = (Button)sender;
        keyboardTip = new ToolTip { Content = button.ToolTip, PlacementTarget = button, IsOpen = true };
    }
    private void Info_Blur(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (keyboardTip is not null) { keyboardTip.IsOpen = false; keyboardTip = null; }
    }
    private void ExtensionFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string folder = Path.Combine(dir.FullName, "chrome-extension");
            if (Directory.Exists(folder)) { Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); return; }
            dir = dir.Parent;
        }
        TabsNotice.Text = T("FolderMissing");
    }
    private void CopyAddress_Click(object sender, RoutedEventArgs e) { Clipboard.SetText("chrome://extensions"); TabsNotice.Text = T("Copied"); }
}

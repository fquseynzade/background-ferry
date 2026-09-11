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
using BackgroundFerry.Core;
using BackgroundFerry.Windows;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace BackgroundFerry.App;

public sealed class AppChoice : INotifyPropertyChanged
{
    public string Process { get; init; } = "";
    public string Name => Process + ".exe";
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
    private MixSettings saved;
    private Process? guardian;
    private bool loading = true;
    private bool shutdown;
    private bool toldAboutTray;
    private int ticks;
    public bool MinimizedAtStart { get; }

    public MainWindow(bool minimized)
    {
        InitializeComponent();
        try { saved = Storage.LoadSettings(); }
        catch (Exception ex) { Storage.Log(ex); saved = new(); Notice.Text = "Saved settings could not be read. Defaults loaded; choose your sources again."; }
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
        menu.Items.Add("Open Background Ferry", null, (_, _) => ShowWindow());
        trayToggle = new Forms.ToolStripMenuItem("Start mixing", null, (_, _) => Toggle());
        menu.Items.Add(trayToggle);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit and restore volume", null, (_, _) => { Shutdown(); Application.Current.Shutdown(); });
        tray = new Forms.NotifyIcon { Text = "Background Ferry · off", Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => ShowWindow();
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += Tick;
        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); Save(); };
        Closing += (_, e) => { if (!shutdown) { e.Cancel = true; Hide(); if (!toldAboutTray) { tray.ShowBalloonTip(3000, "Background Ferry is in the tray", "Mixing continues. Use the tray menu to stop or exit.", Forms.ToolTipIcon.Info); toldAboutTray = true; } } };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.Control) { Toggle(); e.Handled = true; } };
        loading = false;
        UpdateChoices();
        UpdateLabels();
        timer.Start();
    }

    private MixSettings ReadSettings() => new()
    {
        MusicProcess = (MusicPicker.SelectedItem as AppChoice)?.Process ?? "",
        PriorityProcesses = choices.Where(c => c.Selected && c.CanPrioritize).Select(c => c.Process).ToArray(),
        Mode = (MixMode)Math.Max(0, ModePicker.SelectedIndex), DuckPercent = DuckSlider.Value,
        ThresholdDb = ThresholdSlider.Value, AttackMs = AttackSlider.Value, HoldMs = HoldSlider.Value,
        ReleaseMs = ReleaseSlider.Value, StartWithWindows = StartupCheck.IsChecked == true,
        StartMinimized = MinimizedCheck.IsChecked == true
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
        guardian = Process.Start(start) ?? throw new InvalidOperationException("Could not start volume recovery helper.");
    }

    private void Toggle()
    {
        if (engine.Enabled) { engine.Stop(); Notice.Text = "Original volume restored. Manual volume changes were kept."; }
        else
        {
            try { var settings = ReadSettings(); settings.Validate(); EnsureGuardian(); Save(); engine.Start(settings); Notice.Text = "Your music keeps playing. Only its Windows app volume changes."; }
            catch (ArgumentException ex) { Notice.Text = ex.Message; return; }
        }
        UpdateStatus();
    }
    private void Toggle_Click(object sender, RoutedEventArgs e) => Toggle();
    private void Music_Changed(object sender, SelectionChangedEventArgs e) { if (!loading) { UpdateChoices(); SettingsChanged(); } }
    private void Priority_Changed(object sender, RoutedEventArgs e) { if (!loading) SettingsChanged(); }
    private void Setting_Changed(object sender, RoutedEventArgs e) { if (!loading) { UpdateLabels(); SettingsChanged(); } }

    private void SettingsChanged()
    {
        if (engine.Enabled) { engine.Stop(); Notice.Text = "Settings changed. Press Start mixing to use the new mix."; UpdateStatus(); }
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
            SettingsChanged();
        }
        catch (Exception ex) { Storage.Log(ex); loading = true; StartupCheck.IsChecked = false; loading = false; Notice.Text = "Could not update Windows startup: " + ex.Message; }
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
        if (engine.Snapshot.Error is { } error) { Notice.Text = "Audio is temporarily unavailable. " + error; UpdateStatus(); return; }
        if (++ticks % 10 != 0) return;
        loading = true;
        foreach (var level in engine.Levels)
            if (!choices.Any(c => c.Process == level.Process)) choices.Add(new AppChoice { Process = level.Process });
        loading = false;
        UpdateChoices();
        var music = engine.Levels.FirstOrDefault(l => l.Process == (MusicPicker.SelectedItem as AppChoice)?.Process);
        MusicMeter.Value = (music?.Peak ?? 0) * 100;
        MusicLevel.Text = music == null ? "Waiting for this app to play audio" : $"App volume {music.Volume:P0}  ·  {(music.Active ? "playing" : "idle")}";
        MusicHint.Text = "Music and video must be in separate apps. Browser tabs may share one volume control.";
        if (engine.OverrideCount > 0) Notice.Text = "Manual volume change detected. That music session is released until you restart mixing.";
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        ToggleButton.Content = engine.Enabled ? "Stop mixing" : "Start mixing";
        trayToggle.Text = engine.Enabled ? "Stop and restore volume" : "Start mixing";
        tray.Text = "Background Ferry · " + engine.Phase;
        Status.Text = engine.Enabled ? engine.Phase : "Ready when you are";
        StatusDetail.Text = engine.Enabled ? $"Priority signal {20 * Math.Log10(Math.Max(engine.PriorityPeak, 0.000001)):F0} dBFS · {engine.OverrideCount} manual overrides" : "Select your sources, then start the mix.  Ctrl+M";
        GainLabel.Text = $"MUSIC   {engine.Gain:P0}";
        GainMeter.Value = engine.Gain * 100;
    }

    private void UpdateLabels()
    {
        DuckLabel.Text = $"Music remaining · {DuckSlider.Value:0}%";
        ThresholdLabel.Text = $"Signal threshold · {ThresholdSlider.Value:0} dBFS";
        AttackLabel.Text = $"Fade down · {AttackSlider.Value:0} ms";
        HoldLabel.Text = $"Wait in silence · {HoldSlider.Value:0} ms";
        ReleaseLabel.Text = $"Fade back · {ReleaseSlider.Value:0} ms";
        ModeHint.Text = ModePicker.SelectedIndex == 1 ? "Music follows the priority signal within a small range. Quiet content still gets room." : "Music stays consistently quiet during audio, then returns after a pause. Recommended for speech.";
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

    private void Help_Click(object sender, RoutedEventArgs e) => MessageBox.Show(this,
        "1. Start music in Spotify, Apple Music, Telegram or a local player.\n2. Select it under Background music.\n3. Play a video in another app and check that app under Priority audio.\n4. Press Start mixing.\n\nMusic remaining is relative to the original Windows app volume: 20% of an original 50% becomes 10%.\n\nSteady is recommended for speech. Adaptive varies the background within a limited quiet range. Neither mode recognizes speech.\n\nClosing this window keeps the app in the tray. Exit from the tray restores volume. Manual changes in the Windows mixer release that session until mixing is restarted.\n\nStartup opens the app with mixing OFF. Same-browser tabs, exclusive audio and remote Spotify Connect devices are not supported.\n\nSettings and recovery log: " + Storage.DirectoryPath,
        "Background Ferry · Quick start", MessageBoxButton.OK, MessageBoxImage.Information);
}

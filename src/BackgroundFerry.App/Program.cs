using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using BackgroundFerry.Windows;

namespace BackgroundFerry.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
        int stateIndex = Array.IndexOf(args, "--state-dir");
        if (stateIndex >= 0 && stateIndex + 1 < args.Length) Storage.DirectoryPath = Path.GetFullPath(args[stateIndex + 1]);
        if (args.Length >= 4 && args[0] == "--watch")
        {
            Storage.DirectoryPath = args[3];
            try
            {
                using var parent = Process.GetProcessById(int.Parse(args[1]));
                if (parent.StartTime.ToUniversalTime().Ticks == long.Parse(args[2])) parent.WaitForExit();
            }
            catch (ArgumentException) { }
            using var recoveryGate = new RecoveryGate(0);
            if (!recoveryGate.Acquired) return 0; // A replacement app owns/recovered this journal already.
            for (int i = 0; i < 5; i++)
            {
                try { RecoveryJournal.Recover(); }
                catch (Exception ex) { Storage.Log(ex); }
                Thread.Sleep(1000);
            }
            return 0;
        }
        if (args.Contains("--diagnose"))
        {
            try
            {
                var sessions = AudioCatalog.Enumerate();
                try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "diagnostics.json"), JsonSerializer.Serialize(sessions.Select(s => new { s.ProcessName, s.ProcessId, s.Active, s.Volume, s.Muted, s.Peak }))); }
                finally { foreach (var s in sessions) s.Dispose(); }
                return 0;
            }
            catch (Exception ex) { Storage.Log(ex); return 1; }
        }
        using var mutex = new Mutex(true, @"Local\BackgroundFerry.Desktop", out bool first);
        if (!first) { MessageBox.Show("Background Ferry is already running. Open it from the system tray.", "Background Ferry"); return 0; }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        MainWindow? window = null;
        app.DispatcherUnhandledException += (_, e) =>
        {
            Storage.Log(e.Exception);
            try { window?.EmergencyStop(); } catch (Exception stopError) { Storage.Log(stopError); }
            MessageBox.Show("Mixing stopped after an error. Your previous volume will be restored where possible.\n\n" + e.Exception.Message, "Background Ferry");
            e.Handled = true;
        };
        try
        {
            using var recoveryGate = new RecoveryGate(6000);
            if (!recoveryGate.Acquired) throw new InvalidOperationException("Volume recovery is still running. Try again in a moment.");
            RecoveryJournal.Recover();
            window = new MainWindow(args.Contains("--minimized"));
            app.MainWindow = window;
            app.SessionEnding += (_, _) => window.Shutdown();
            app.Exit += (_, _) => window.Shutdown();
            if (!window.MinimizedAtStart) window.Show();
            return app.Run();
        }
        catch (Exception ex) { Storage.Log(ex); MessageBox.Show(ex.Message, "Background Ferry could not start"); return 1; }
    }
}

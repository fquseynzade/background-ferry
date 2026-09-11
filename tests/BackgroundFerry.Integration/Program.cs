using System.Diagnostics;
using BackgroundFerry.Core;
using BackgroundFerry.Windows;

internal static class Program
{
    private static readonly MixSettings settings = new() { MusicProcess = "fixturemusic", PriorityProcesses = ["fixturepriority"], ThresholdDb = -65, HoldMs = 300, ReleaseMs = 300 };
    [MTAThread]
    public static int Main(string[] args)
    {
        try
        {
            if (args[0] == "--owner")
            {
                Storage.DirectoryPath = args[1];
                using var gate = new RecoveryGate(5000);
                using var owner = new MixEngine(); owner.Start(settings);
                while (true) { owner.Tick(50); Thread.Sleep(50); }
            }
            string root = Path.GetFullPath(args[0]);
            Storage.DirectoryPath = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(Storage.DirectoryPath);
            Console.WriteLine("Uses two quiet test tones on the default output. No user app volume is changed.");
            using var music = Fixture(root, "FixtureMusic", "220");
            using var priority = Fixture(root, "FixturePriority", "330");
            try
            {
                Thread.Sleep(500);
                WithMusic(s => s.SetVolume(0.5));
                using (var engine = new MixEngine())
                {
                    engine.Start(settings); Pump(engine, 1800);
                    Console.WriteLine($"Priority peak={engine.PriorityPeak:F6}, gain={engine.Gain:F3}");
                    Assert(engine.PriorityPeak > 0, "priority peak is read through real Core Audio");
                    CheckVolume(0.1, "real session is ducked to 20% of baseline");
                    engine.Stop(); CheckVolume(0.5, "Stop restores original volume");
                    engine.Start(settings); Pump(engine, 1000);
                    WithMusic(s => s.SetVolume(0.37)); Pump(engine, 500);
                    Assert(engine.OverrideCount == 1, "manual override detected");
                    engine.Stop(); CheckVolume(0.37, "manual override survives Stop");
                    WithMusic(s => s.SetVolume(0.5)); engine.Start(settings); Pump(engine, 1000);
                    priority.StandardInput.WriteLine(); priority.WaitForExit(5000); Pump(engine, 2400);
                    CheckVolume(0.5, "priority process exit returns music to baseline");
                    using var restarted = Fixture(root, "FixturePriority", "330");
                    try { Pump(engine, 2000); CheckVolume(0.1, "new priority session is detected without restarting the engine"); }
                    finally { restarted.StandardInput.WriteLine(); restarted.WaitForExit(3000); }
                    engine.Stop();
                }
                using var priority2 = Fixture(root, "FixturePriority", "330");
                try
                {
                    using var owner = Start(Path.Combine(AppContext.BaseDirectory, "BackgroundFerry.Integration.exe"), "--owner", Storage.DirectoryPath);
                    try
                    {
                        using var guardian = Start(Path.Combine(root, "src/BackgroundFerry.App/bin/Release/net10.0-windows/BackgroundFerry.exe"), "--watch", owner.Id.ToString(), owner.StartTime.ToUniversalTime().Ticks.ToString(), Storage.DirectoryPath);
                        Thread.Sleep(1800); CheckVolume(0.1, "separate owner ducks music before crash");
                        owner.Kill(); owner.WaitForExit(5000);
                        Assert(guardian.WaitForExit(10000), "crash watchdog exits");
                        CheckVolume(0.5, "watchdog restores volume after forced process termination");
                    }
                    finally { if (!owner.HasExited) { owner.Kill(); owner.WaitForExit(5000); } }
                }
                finally { if (!priority2.HasExited) { priority2.StandardInput.WriteLine(); priority2.WaitForExit(3000); } }

                // Exercise the exact worker service used by the WPF window, including
                // sessions created after the service has already populated its catalog.
                using (var service = new MixService())
                {
                    Thread.Sleep(1200);
                    using var latePriority = Fixture(root, "FixturePriority", "330");
                    try
                    {
                        service.Start(settings);
                        Thread.Sleep(2200);
                        Assert(service.Snapshot.Error == null, "MTA worker publishes a healthy snapshot");
                        Assert(service.Levels.Any(l => l.Process == "fixturepriority"), "worker discovers a late priority session");
                        CheckVolume(0.1, "UI worker service ducks music");
                        service.Stop(); CheckVolume(0.5, "UI worker Stop restores music synchronously");
                        service.Start(settings with { Mode = MixMode.Adaptive });
                        Thread.Sleep(1600);
                        Assert(service.Gain >= 0.2 && service.Gain <= 0.33, "adaptive gain remains in the configured quiet band");
                    }
                    finally { latePriority.StandardInput.WriteLine(); latePriority.WaitForExit(3000); }
                }
                CheckVolume(0.5, "disposing the worker restores music");
                Console.WriteLine("All Windows integration checks passed.");
                return 0;
            }
            finally
            {
                if (!music.HasExited) { music.StandardInput.WriteLine(); music.WaitForExit(3000); }
                if (!priority.HasExited) { priority.StandardInput.WriteLine(); priority.WaitForExit(3000); }
            }
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static Process Fixture(string root, string name, string frequency)
    {
        var p = Start(Path.Combine(root, $"tests/{name}/bin/Release/net10.0-windows/{name}.exe"), frequency);
        if (p.StandardOutput.ReadLine() != "READY") throw new Exception($"{name} failed to create an audio session.");
        return p;
    }
    private static Process Start(string path, params string[] args)
    {
        var start = new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardInput = true, RedirectStandardOutput = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        return Process.Start(start)!;
    }
    private static void Pump(MixEngine engine, int ms) { for (int t = 0; t < ms; t += 50) { engine.Tick(50); Thread.Sleep(50); } }
    private static void WithMusic(Action<AudioSession> action)
    {
        var sessions = AudioCatalog.Enumerate();
        try { action(sessions.Single(s => s.ProcessName == "fixturemusic")); }
        finally { foreach (var s in sessions) s.Dispose(); }
    }
    private static void CheckVolume(double expected, string message) => WithMusic(s => Assert(Math.Abs(s.Volume - expected) < 0.01, $"{message} (actual {s.Volume:F3}, expected {expected:F3})"));
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
}

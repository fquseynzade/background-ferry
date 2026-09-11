using BackgroundFerry.Core;

namespace BackgroundFerry.Windows;

public sealed record AppLevel(string Process, double Peak, double Volume, bool Active, bool Overridden, int ProcessId = 0);

/// <summary>Single-threaded owner; all methods called on the same COM-initialized thread.</summary>
public sealed class MixEngine : IDisposable
{
    private List<AudioSession> sessions = [];
    private readonly Dictionary<string, VolumeLease> leases = [];
    private readonly RecoveryJournal journal = new();
    private readonly DuckingEnvelope envelope = new();
    private readonly AudioNotifications notifications = new();
    private MixSettings settings = new();
    private double refreshMs = 1000;
    public bool Enabled { get; private set; }
    public double Gain => envelope.Gain;
    public string Phase => Enabled ? envelope.Phase : "Off";
    public double PriorityPeak { get; private set; }
    public int OverrideCount => leases.Values.Count(l => l.Overridden);
    public IReadOnlyList<AppLevel> Levels { get; private set; } = [];

    public void Start(MixSettings options)
    {
        options.Validate();
        Stop();
        settings = options;
        envelope.Reset();
        Enabled = true;
        refreshMs = 1000;
    }

    public void Tick(double elapsedMs)
    {
        refreshMs += elapsedMs;
        if (refreshMs >= 1000)
        {
            var fresh = AudioCatalog.Enumerate(notifications);
            // Restore handles that are leaving the catalog before releasing them.
            var ids = fresh.Select(s => s.Id).ToHashSet();
            foreach (var old in sessions)
            {
                if (!ids.Contains(old.Id))
                {
                    try
                    {
                        // The enumerator may omit sessions delivered by notification.
                        if (notifications.ContainsDevice(old.DeviceId) && !old.Expired) { fresh.Add(old); continue; }
                    }
                    catch (System.Runtime.InteropServices.COMException) { }
                    Restore(old);
                }
                old.Dispose();
            }
            sessions = fresh;
            refreshMs = 0;
        }

        PriorityPeak = 0;
        var readings = new List<(AudioSession Session, double Peak, double Volume, bool Active)>();
        foreach (var session in sessions)
        {
            try
            {
                bool active = session.Active;
                double peak = active && !session.Muted ? session.Peak : 0;
                double volume = session.Volume;
                readings.Add((session, peak, volume, active));
                if (settings.PriorityProcesses.Contains(session.ProcessName, StringComparer.OrdinalIgnoreCase))
                    PriorityPeak = Math.Max(PriorityPeak, peak);
            }
            catch (System.Runtime.InteropServices.COMException) { refreshMs = 1000; }
        }

        if (Enabled)
        {
            double gain = envelope.Step(PriorityPeak, elapsedMs, settings);
            foreach (var reading in readings)
            {
                var session = reading.Session;
                if (!string.Equals(session.ProcessName, settings.MusicProcess, StringComparison.OrdinalIgnoreCase)) continue;
                if (!leases.TryGetValue(session.Id, out var lease)) leases[session.Id] = lease = new(reading.Volume);
                if (!lease.Observe(reading.Volume)) { journal.Forget(session.Id); continue; }
                double target = lease.Target(gain);
                if (Math.Abs(target - reading.Volume) < 0.001) continue;
                journal.Prepare(session.Id, lease, target);
                try { session.SetVolume(target); lease.Written(target); }
                catch (System.Runtime.InteropServices.COMException) { refreshMs = 1000; }
            }
        }

        Levels = readings.GroupBy(r => r.Session.ProcessName).Select(g => new AppLevel(g.Key,
            g.Max(r => r.Peak), g.Max(r => r.Volume), g.Any(r => r.Active),
            g.Any(r => leases.TryGetValue(r.Session.Id, out var l) && l.Overridden),
            g.First().Session.ProcessId)).OrderBy(x => x.Process).ToArray();
    }

    private void Restore(AudioSession session)
    {
        if (!leases.TryGetValue(session.Id, out var lease)) return;
        try
        {
            if (lease.CanRestore(session.Volume)) session.SetVolume(lease.Original);
            journal.Forget(session.Id);
            leases.Remove(session.Id);
        }
        catch (System.Runtime.InteropServices.COMException) { /* Keep durable recovery record. */ }
        catch (IOException ex) { Storage.Log(ex); }
        catch (UnauthorizedAccessException ex) { Storage.Log(ex); }
    }

    public void Stop()
    {
        Enabled = false;
        foreach (var session in sessions) Restore(session);
        leases.Clear();
        envelope.Reset();
    }

    public void Dispose()
    {
        try { Stop(); }
        finally { foreach (var session in sessions) session.Dispose(); sessions.Clear(); notifications.Dispose(); }
    }
}

using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace BackgroundFerry.Windows;

internal sealed class AudioNotifications : IDisposable
{
    private readonly Dictionary<string, (IAudioSessionManager2 Manager, SessionNotification Callback)> subscriptions = [];
    private readonly ConcurrentQueue<(string Device, IntPtr Session)> pending = new();
    private readonly HashSet<string> activeDevices = [];
    public void BeginCycle() => activeDevices.Clear();
    public bool ContainsDevice(string id) => activeDevices.Contains(id);
    public bool Observe(string id, IAudioSessionManager2 manager)
    {
        activeDevices.Add(id);
        if (subscriptions.ContainsKey(id)) return false;
        var callback = new SessionNotification(id, pending);
        AudioSession.Check(manager.RegisterSessionNotification(callback));
        subscriptions[id] = (manager, callback);
        return true; // Transfer this manager RCW reference to the subscription.
    }
    public void EndCycle()
    {
        foreach (string id in subscriptions.Keys.Where(id => !activeDevices.Contains(id)).ToArray()) Remove(id);
    }
    private void Remove(string id)
    {
        var sub = subscriptions[id];
        sub.Callback.Close();
        try { sub.Manager.UnregisterSessionNotification(sub.Callback); }
        finally { AudioSession.Release(sub.Manager); subscriptions.Remove(id); }
    }
    public List<AudioSession> Drain()
    {
        var sessions = new List<AudioSession>();
        while (pending.TryDequeue(out var next))
        {
            object? obj = null;
            try
            {
                if (!activeDevices.Contains(next.Device)) continue;
                obj = Marshal.GetObjectForIUnknown(next.Session);
                var control = (IAudioSessionControl2)obj;
                AudioSession.Check(control.GetProcessId(out uint pid));
                AudioSession.Check(control.GetState(out int state));
                if (pid == 0 || pid == Environment.ProcessId || state == 2 || control.IsSystemSoundsSession() == 0) continue;
                sessions.Add(new AudioSession(control, next.Device));
                obj = null;
            }
            catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException or InvalidCastException) { }
            finally { AudioSession.Release(obj); Marshal.Release(next.Session); }
        }
        return sessions;
    }
    public void Dispose()
    {
        foreach (string id in subscriptions.Keys.ToArray()) Remove(id);
        while (pending.TryDequeue(out var next)) Marshal.Release(next.Session);
    }
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class SessionNotification : IAudioSessionNotification
{
    private readonly string device;
    private readonly ConcurrentQueue<(string Device, IntPtr Session)> pending;
    private readonly object gate = new();
    private bool closed;
    internal SessionNotification(string device, ConcurrentQueue<(string Device, IntPtr Session)> pending) { this.device = device; this.pending = pending; }
    public int OnSessionCreated(IntPtr session)
    {
        lock (gate)
        {
            if (closed || session == IntPtr.Zero) return 0;
            Marshal.AddRef(session);
            pending.Enqueue((device, session));
        }
        return 0;
    }
    internal void Close() { lock (gate) closed = true; }
}

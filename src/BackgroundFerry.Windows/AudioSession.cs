using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BackgroundFerry.Windows;

public sealed class AudioSession : IDisposable
{
    private readonly IAudioSessionControl2 control;
    private readonly ISimpleAudioVolume volume;
    private readonly IAudioMeterInformation meter;
    private static Guid eventContext = new("973cc3a8-3ea7-4a3c-9c1e-81e24bc69533");
    public string Id { get; }
    public string ProcessName { get; }
    public int ProcessId { get; }
    public string DeviceId { get; }

    internal AudioSession(IAudioSessionControl2 control, string deviceId)
    {
        this.control = control;
        volume = (ISimpleAudioVolume)control;
        meter = (IAudioMeterInformation)control;
        Check(control.GetSessionInstanceIdentifier(out string id));
        Check(control.GetProcessId(out uint pid));
        Id = deviceId + "|" + id;
        DeviceId = deviceId;
        ProcessId = (int)pid;
        using var process = Process.GetProcessById(ProcessId);
        ProcessName = process.ProcessName.ToLowerInvariant();
    }

    public float Volume { get { Check(volume.GetMasterVolume(out float v)); return v; } }
    public bool Muted { get { Check(volume.GetMute(out bool v)); return v; } }
    public bool Active { get { Check(control.GetState(out int state)); return state == 1; } }
    public bool Expired { get { Check(control.GetState(out int state)); return state == 2; } }
    public float Peak { get { Check(meter.GetPeakValue(out float v)); return v; } }
    public void SetVolume(double value) => Check(volume.SetMasterVolume((float)Math.Clamp(value, 0, 1), ref eventContext));
    public void Dispose() => Release(control);
    internal static void Check(int hr) => Marshal.ThrowExceptionForHR(hr);
    internal static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
}

public static class AudioCatalog
{
    /// <summary>Returns owned handles for all active render endpoints. Use/dispose on calling thread.</summary>
    public static List<AudioSession> Enumerate() => Enumerate(null);
    internal static List<AudioSession> Enumerate(AudioNotifications? notifications)
    {
        var sessions = new List<AudioSession>();
        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? devices = null;
        try
        {
            notifications?.BeginCycle();
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorClass();
            AudioSession.Check(enumerator.EnumAudioEndpoints(0, 1, out devices));
            AudioSession.Check(devices.GetCount(out uint count));
            for (uint d = 0; d < count; d++)
            {
                IMMDevice? device = null;
                object? managerObject = null;
                IAudioSessionEnumerator? list = null;
                try
                {
                    AudioSession.Check(devices.Item(d, out device));
                    AudioSession.Check(device.GetId(out string deviceId));
                    Guid iid = typeof(IAudioSessionManager2).GUID;
                    AudioSession.Check(device.Activate(ref iid, 23, IntPtr.Zero, out managerObject));
                    var manager = (IAudioSessionManager2)managerObject;
                    if (notifications?.Observe(deviceId, manager) == true) managerObject = null;
                    AudioSession.Check(manager.GetSessionEnumerator(out list));
                    AudioSession.Check(list.GetCount(out int sessionCount));
                    for (int i = 0; i < sessionCount; i++)
                    {
                        IAudioSessionControl2? control = null;
                        try
                        {
                            AudioSession.Check(list.GetSession(i, out control));
                            AudioSession.Check(control.GetProcessId(out uint pid));
                            AudioSession.Check(control.GetState(out int state));
                            if (control.IsSystemSoundsSession() == 0 || pid == 0 || pid == Environment.ProcessId || state == 2) continue;
                            sessions.Add(new AudioSession(control, deviceId));
                            control = null; // Ownership transferred.
                        }
                        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidCastException or InvalidOperationException) { }
                        finally { AudioSession.Release(control); }
                    }
                }
                catch (COMException) { /* A device can disappear during enumeration. */ }
                finally { AudioSession.Release(list); AudioSession.Release(managerObject); AudioSession.Release(device); }
            }
            notifications?.EndCycle();
            if (notifications != null) sessions.AddRange(notifications.Drain());
            // A newly created session can arrive both through enumeration and notification.
            var unique = new Dictionary<string, AudioSession>();
            foreach (var session in sessions) { if (!unique.TryAdd(session.Id, session)) session.Dispose(); }
            return unique.Values.ToList();
        }
        catch { foreach (var s in sessions) s.Dispose(); throw; }
        finally { AudioSession.Release(devices); AudioSession.Release(enumerator); }
    }
}

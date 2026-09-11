using System.Text.Json;
using BackgroundFerry.Core;

namespace BackgroundFerry.Windows;

public static class Storage
{
    public static string DirectoryPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BackgroundFerry");
    public static string SettingsPath => Path.Combine(DirectoryPath, "settings.json");
    public static string RecoveryPath => Path.Combine(DirectoryPath, "recovery.json");
    private static readonly JsonSerializerOptions json = new() { WriteIndented = true };

    public static MixSettings LoadSettings()
    {
        if (!File.Exists(SettingsPath)) return new();
        var result = JsonSerializer.Deserialize<MixSettings>(File.ReadAllText(SettingsPath)) ?? new();
        result.Validate(false);
        return result;
    }

    public static void SaveSettings(MixSettings settings) { settings.Validate(false); Write(SettingsPath, settings); }
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, json));
        File.Move(temp, path, true);
    }
    public static void Log(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            string path = Path.Combine(DirectoryPath, "errors.log");
            if (File.Exists(path) && new FileInfo(path).Length > 1_000_000) File.Move(path, path + ".old", true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {ex}\n");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

public sealed record RecoveryEntry(string Id, double Original, double Last, double Pending);

public sealed class RecoveryJournal
{
    private readonly Dictionary<string, RecoveryEntry> entries = [];
    public RecoveryJournal()
    {
        if (!File.Exists(Storage.RecoveryPath)) return;
        foreach (var entry in JsonSerializer.Deserialize<RecoveryEntry[]>(File.ReadAllText(Storage.RecoveryPath)) ?? [])
            entries[entry.Id] = entry;
    }
    public void Prepare(string id, VolumeLease lease, double next)
    {
        entries[id] = new(id, lease.Original, lease.LastWritten, next);
        Flush(); // Persist BEFORE touching another application's volume.
    }
    public void Forget(string id) { if (entries.Remove(id)) Flush(); }
    private void Flush() => Storage.Write(Storage.RecoveryPath, entries.Values.ToArray());

    public static int Recover()
    {
        if (!File.Exists(Storage.RecoveryPath)) return 0;
        var entries = JsonSerializer.Deserialize<RecoveryEntry[]>(File.ReadAllText(Storage.RecoveryPath)) ?? [];
        if (entries.Length == 0) return 0;
        var sessions = AudioCatalog.Enumerate();
        var remaining = new List<RecoveryEntry>();
        int restored = 0;
        try
        {
            foreach (var entry in entries)
            {
                var session = sessions.Find(s => s.Id == entry.Id);
                if (session == null) { remaining.Add(entry); continue; }
                try
                {
                    double actual = session.Volume;
                    if (double.IsFinite(entry.Original) && entry.Original >= 0 && entry.Original <= 1 &&
                        (Math.Abs(actual - entry.Last) <= VolumeLease.Tolerance || Math.Abs(actual - entry.Pending) <= VolumeLease.Tolerance))
                    { session.SetVolume(entry.Original); restored++; }
                    // A changed volume belongs to the user: leave it alone and drop the entry.
                }
                catch (System.Runtime.InteropServices.COMException) { remaining.Add(entry); }
            }
            Storage.Write(Storage.RecoveryPath, remaining);
        }
        finally { foreach (var s in sessions) s.Dispose(); }
        return restored;
    }
}

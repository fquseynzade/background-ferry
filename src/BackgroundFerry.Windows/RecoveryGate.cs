using System.Security.Cryptography;
using System.Text;

namespace BackgroundFerry.Windows;

/// <summary>Prevents an old watchdog from restoring over a newly started app.</summary>
public sealed class RecoveryGate : IDisposable
{
    private readonly Mutex mutex;
    public bool Acquired { get; }
    public RecoveryGate(int timeoutMs)
    {
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(Storage.DirectoryPath).ToUpperInvariant())))[..24];
        mutex = new Mutex(false, @"Local\BackgroundFerry.Recovery." + key);
        try { Acquired = mutex.WaitOne(timeoutMs); }
        catch (AbandonedMutexException) { Acquired = true; }
    }
    public void Dispose() { if (Acquired) mutex.ReleaseMutex(); mutex.Dispose(); }
}

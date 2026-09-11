using System.Collections.Concurrent;
using System.Diagnostics;
using BackgroundFerry.Core;

namespace BackgroundFerry.Windows;

public sealed record MixSnapshot(bool Enabled, double Gain, string Phase, double PriorityPeak, int OverrideCount, IReadOnlyList<AppLevel> Levels, string? Error);

/// <summary>Owns all live COM audio objects on a dedicated MTA thread. UI reads immutable snapshots.</summary>
public sealed class MixService : IDisposable
{
    private readonly BlockingCollection<Action<MixEngine>> commands = new();
    private readonly Thread thread;
    private readonly MixEngine engine = new();
    private MixSnapshot snapshot = new(false, 1, "Off", 0, 0, [], null);
    private bool disposed;
    public MixSnapshot Snapshot => Volatile.Read(ref snapshot);
    public bool Enabled => Snapshot.Enabled;
    public double Gain => Snapshot.Gain;
    public string Phase => Snapshot.Phase;
    public double PriorityPeak => Snapshot.PriorityPeak;
    public int OverrideCount => Snapshot.OverrideCount;
    public IReadOnlyList<AppLevel> Levels => Snapshot.Levels;

    public MixService()
    {
        thread = new Thread(Run) { IsBackground = true, Name = "Background Ferry audio" };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
    }
    public void Start(MixSettings settings) => Invoke(engine => engine.Start(settings));
    public void Stop() => Invoke(engine => engine.Stop());
    private void Invoke(Action<MixEngine> action)
    {
        if (disposed) return;
        var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        commands.Add(engine =>
        {
            try { action(engine); Publish(engine, null); result.SetResult(); }
            catch (Exception ex) { result.SetException(ex); }
        });
        result.Task.GetAwaiter().GetResult();
    }
    private void Publish(MixEngine engine, string? error) => Volatile.Write(ref snapshot,
        new(engine.Enabled, engine.Gain, engine.Phase, engine.PriorityPeak, engine.OverrideCount, engine.Levels, error));
    private void Run()
    {
        var clock = Stopwatch.StartNew();
        double last = 0;
        while (!commands.IsCompleted)
        {
            if (commands.TryTake(out var action, 50)) action(engine);
            double now = clock.Elapsed.TotalMilliseconds;
            try { engine.Tick(now - last); Publish(engine, null); }
            catch (Exception ex)
            {
                try { engine.Stop(); } catch (Exception restoreError) { Storage.Log(restoreError); }
                Storage.Log(ex); Publish(engine, ex.Message);
                Thread.Sleep(500);
            }
            last = now;
        }
        try { engine.Dispose(); } catch (Exception ex) { Storage.Log(ex); }
    }
    public void Dispose()
    {
        if (disposed) return;
        Stop(); disposed = true; commands.CompleteAdding(); thread.Join(); commands.Dispose();
    }
}

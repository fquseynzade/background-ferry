namespace BackgroundFerry.Core;

public enum MixMode { Steady, Adaptive }

public sealed record MixSettings
{
    public string MusicProcess { get; init; } = "";
    public string[] PriorityProcesses { get; init; } = [];
    public MixMode Mode { get; init; } = MixMode.Steady;
    // Relative to each session's volume when enabled, not an absolute Windows level.
    public double DuckPercent { get; init; } = 20;
    public double ThresholdDb { get; init; } = -42;
    public double AttackMs { get; init; } = 180;
    public double HoldMs { get; init; } = 900;
    public double ReleaseMs { get; init; } = 1400;
    public bool StartWithWindows { get; init; }
    public bool StartMinimized { get; init; }

    public void Validate(bool requireSources = true)
    {
        if (requireSources && (string.IsNullOrWhiteSpace(MusicProcess) || PriorityProcesses.Length == 0))
            throw new ArgumentException("Choose music and at least one priority application.");
        if (PriorityProcesses.Any(p => string.Equals(p, MusicProcess, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Music and priority must be different applications.");
        if (!Enum.IsDefined(Mode)) throw new ArgumentException("Unknown mix mode.");
        Range(DuckPercent, 1, 60, nameof(DuckPercent));
        Range(ThresholdDb, -70, -10, nameof(ThresholdDb));
        Range(AttackMs, 50, 1000, nameof(AttackMs));
        Range(HoldMs, 200, 5000, nameof(HoldMs));
        Range(ReleaseMs, 200, 5000, nameof(ReleaseMs));
    }

    private static void Range(double x, double min, double max, string name)
    {
        if (!double.IsFinite(x) || x < min || x > max)
            throw new ArgumentOutOfRangeException(name, $"Expected {min}–{max}.");
    }
}

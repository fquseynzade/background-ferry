namespace BackgroundFerry.Core;

/// <summary>One session's reversible volume ownership. User changes always win.</summary>
public sealed class VolumeLease(double original)
{
    public double Original { get; } = Math.Clamp(original, 0, 1);
    public double LastWritten { get; private set; } = Math.Clamp(original, 0, 1);
    public bool Overridden { get; private set; }
    public const double Tolerance = 0.002;
    public bool Observe(double actual)
    {
        if (Math.Abs(actual - LastWritten) > Tolerance) Overridden = true;
        return !Overridden;
    }
    public double Target(double gain) => Original * Math.Clamp(gain, 0, 1);
    public void Written(double value) => LastWritten = value;
    public bool CanRestore(double actual) => !Overridden && Math.Abs(actual - LastWritten) <= Tolerance;
}

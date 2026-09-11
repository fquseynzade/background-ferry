namespace BackgroundFerry.Core;

/// <summary>Peak-driven control envelope; not a speech recognizer or a LUFS meter.</summary>
public sealed class DuckingEnvelope
{
    private double quietMs;
    private double heldTarget = 1;
    private bool active;
    public double Gain { get; private set; } = 1;
    public string Phase { get; private set; } = "Ready";

    public double Step(double peak, double elapsedMs, MixSettings settings)
    {
        if (!double.IsFinite(peak)) peak = 0;
        peak = Math.Clamp(peak, 0, 1);
        elapsedMs = double.IsFinite(elapsedMs) ? Math.Clamp(elapsedMs, 0, 250) : 0;
        double db = 20 * Math.Log10(Math.Max(peak, 0.000001));
        // Hysteresis keeps low-level noise from repeatedly opening/closing the gate.
        bool audible = db >= settings.ThresholdDb - (active ? 4 : 0);
        double target;
        if (audible)
        {
            active = true;
            quietMs = 0;
            double floor = settings.DuckPercent / 100;
            // Quiet content stays ducked too: adaptive mode only moves within a small band.
            double ceiling = Math.Min(0.65, floor * 1.65);
            double strength = Math.Clamp((db - settings.ThresholdDb) / 30, 0, 1);
            target = settings.Mode == MixMode.Steady ? floor : ceiling + (floor - ceiling) * strength;
            heldTarget = target;
            Phase = "Ducking";
        }
        else
        {
            active = false;
            quietMs += elapsedMs;
            target = quietMs < settings.HoldMs ? heldTarget : 1;
            Phase = quietMs < settings.HoldMs && heldTarget < 1 ? "Holding" : "Returning";
        }
        // Attack/release are approximately the time to complete 95% of a transition.
        double duration = target < Gain ? settings.AttackMs : settings.ReleaseMs;
        Gain += (target - Gain) * (1 - Math.Exp(-3 * elapsedMs / duration));
        Gain = Math.Clamp(Gain, 0, 1);
        if (Math.Abs(Gain - target) < 0.0005) Gain = target;
        if (Gain == 1 && target == 1) Phase = "Music free";
        return Gain;
    }

    public void Reset() { Gain = 1; heldTarget = 1; quietMs = 0; active = false; Phase = "Ready"; }
}

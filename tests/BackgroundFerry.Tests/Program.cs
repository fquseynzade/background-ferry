using BackgroundFerry.Core;

int passed = 0;
var settings = new MixSettings { MusicProcess = "music", PriorityProcesses = ["browser"] };
void Test(string name, Action test) { test(); Console.WriteLine("PASS " + name); passed++; }
void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
void Near(double actual, double expected, double tolerance = 0.005) => Assert(Math.Abs(actual - expected) <= tolerance, $"Expected {expected}, got {actual}");
double Run(DuckingEnvelope e, double peak, int ms, MixSettings? s = null) { for (int t = 0; t < ms; t += 50) e.Step(peak, 50, s ?? settings); return e.Gain; }

Test("silence never raises music above original", () => Near(Run(new(), 0, 10000), 1));
Test("sustained priority converges to selected steady gain", () => Near(Run(new(), 0.2, 1000), 0.2));
Test("first attack is smooth, not an instant jump", () => { var e = new DuckingEnvelope(); double gain = e.Step(0.2, 50, settings); Assert(gain > 0.2 && gain < 1); });
Test("short pauses do not bring music back", () => { var e = new DuckingEnvelope(); Run(e, 0.2, 1000); Near(Run(e, 0, 600), 0.2); });
Test("long silence restores music", () => { var e = new DuckingEnvelope(); Run(e, 0.2, 1000); Near(Run(e, 0, 5000), 1); });
Test("return is gradual after hold", () => { var e = new DuckingEnvelope(); Run(e, 0.2, 1000); double gain = Run(e, 0, 1050); Assert(gain > 0.2 && gain < 0.8); });
Test("adaptive mode is bounded even for quiet priority", () => { var s = settings with { Mode = MixMode.Adaptive }; double quiet = Run(new(), 0.009, 4000, s); double loud = Run(new(), 0.8, 4000, s); Assert(quiet <= 0.33 && quiet >= 0.2 && loud < quiet); });
Test("gate ignores signals below threshold", () => Near(Run(new(), 0.0001, 5000), 1));
Test("reset starts with unattenuated music", () => { var e = new DuckingEnvelope(); Run(e, 1, 1000); e.Reset(); Near(e.Gain, 1); Near(Run(e, 0, 200), 1); });
Test("invalid samples and elapsed time do not produce NaN", () => { var e = new DuckingEnvelope(); e.Step(double.NaN, double.NaN, settings); e.Step(double.PositiveInfinity, -50, settings); Near(e.Gain, 1); });
Test("gain multiplies baseline instead of replacing it", () => Near(new VolumeLease(0.5).Target(0.2), 0.1));
Test("zero baseline stays silent", () => Near(new VolumeLease(0).Target(1), 0));
Test("self-written changes retain ownership", () => { var l = new VolumeLease(0.5); l.Written(0.1); Assert(l.Observe(0.10001)); Assert(l.CanRestore(0.1)); });
Test("manual override is sticky and never restored", () => { var l = new VolumeLease(0.5); l.Written(0.1); Assert(!l.Observe(0.3)); Assert(!l.Observe(0.1)); Assert(!l.CanRestore(0.1)); });
Test("same process cannot be music and priority", () => { try { (settings with { PriorityProcesses = ["MUSIC"] }).Validate(); throw new Exception("Accepted conflicting sources"); } catch (ArgumentException) { } });
Test("non-finite settings are rejected", () => { try { (settings with { AttackMs = double.NaN }).Validate(); throw new Exception("Accepted NaN"); } catch (ArgumentOutOfRangeException) { } });
Console.WriteLine($"{passed} tests passed.");

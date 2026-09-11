using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using BackgroundFerry.App;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main()
    {
        try
        {
            static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
            var path = Environment.ProcessPath!;
            var actual = await Task.Run(() => AppIdentityReader.ReadFile(path, "fallback"));
            Check(actual.Icon.IsFrozen, "native icon is safe to bind across threads");
            Check(actual.Icon != AppIdentityReader.FallbackIcon, "real executable icon is extracted");
            Check(actual.Name != "fallback" && actual.Name.Length > 0, "executable description supplies display name");
            var missing = AppIdentityReader.ReadFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe"), "Unknown player");
            Check(missing.Icon == AppIdentityReader.FallbackIcon && missing.Name == "Unknown player", "unknown executable retains readable fallback");
            using var self = Process.GetCurrentProcess();
            Check(AppIdentityReader.Read(self.Id, "not-this-process") is null, "reused or mismatched PID cannot supply wrong metadata");
            Check(AppIdentityReader.Read(-1, "missing") is null, "exited process is tolerated");
            Console.WriteLine("All app identity checks passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

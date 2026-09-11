using System.Runtime.InteropServices;

// A quiet, test-owned waveOut stream. Never touches system volume or another app.
internal static class Tone
{
    [StructLayout(LayoutKind.Sequential)] private struct Format { public ushort Tag, Channels; public uint Rate, BytesPerSecond; public ushort BlockAlign, Bits, Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Header { public IntPtr Data; public uint Length, Recorded; public UIntPtr User; public uint Flags, Loops; public IntPtr Next; public UIntPtr Reserved; }
    [DllImport("winmm.dll")] private static extern uint waveOutOpen(out IntPtr handle, uint device, ref Format format, IntPtr callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")] private static extern uint waveOutPrepareHeader(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutWrite(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutReset(IntPtr handle);
    [DllImport("winmm.dll")] private static extern uint waveOutUnprepareHeader(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutClose(IntPtr handle);
    public static int Main(string[] args)
    {
        var format = new Format { Tag = 1, Channels = 1, Rate = 48000, BytesPerSecond = 96000, BlockAlign = 2, Bits = 16 };
        uint result = waveOutOpen(out var handle, uint.MaxValue, ref format, IntPtr.Zero, IntPtr.Zero, 0);
        if (result != 0) { Console.Error.WriteLine($"waveOutOpen failed: {result}"); return 1; }
        var samples = new short[24000];
        double frequency = args.Length > 0 ? double.Parse(args[0]) : 220;
        for (int i = 0; i < samples.Length; i++) samples[i] = (short)(Math.Sin(2 * Math.PI * frequency * i / 48000) * 500);
        IntPtr data = Marshal.AllocHGlobal(samples.Length * 2);
        Marshal.Copy(samples, 0, data, samples.Length);
        var header = new Header { Data = data, Length = (uint)(samples.Length * 2), Flags = 4 | 8, Loops = 10000 };
        uint size = (uint)Marshal.SizeOf<Header>();
        IntPtr ptr = Marshal.AllocHGlobal((int)size);
        Marshal.StructureToPtr(header, ptr, false);
        try
        {
            if (waveOutPrepareHeader(handle, ptr, size) != 0 || waveOutWrite(handle, ptr, size) != 0) return 2;
            Console.WriteLine("READY");
            Console.ReadLine();
        }
        finally { waveOutReset(handle); waveOutUnprepareHeader(handle, ptr, size); waveOutClose(handle); Marshal.FreeHGlobal(ptr); Marshal.FreeHGlobal(data); }
        return 0;
    }
}

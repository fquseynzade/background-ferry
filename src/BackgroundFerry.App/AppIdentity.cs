using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BackgroundFerry.App;

public sealed record AppIdentity(string Name, ImageSource Icon);

/// <summary>Reads installed executable metadata off the UI/audio threads. No service logo list or network.</summary>
public static class AppIdentityReader
{
    private static readonly ConcurrentDictionary<string, AppIdentity> Cache = new(StringComparer.OrdinalIgnoreCase);
    public static ImageSource FallbackIcon { get; } = CreateFallback();

    public static AppIdentity? Read(int processId, string expectedProcess)
    {
        var identity = ReadProcess(processId, expectedProcess);
        if (identity is not null) return identity;
        // Chromium audio sessions can belong to a restricted utility process.
        // Its ordinary browser process has the same executable and readable metadata.
        Process[] siblings;
        try { siblings = Process.GetProcessesByName(expectedProcess); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException) { return null; }
        try
        {
            foreach (var sibling in siblings)
            {
                if (sibling.Id == processId) continue;
                identity = ReadProcess(sibling.Id, expectedProcess);
                if (identity is not null) return identity;
            }
            return null;
        }
        finally { foreach (var sibling in siblings) sibling.Dispose(); }
    }

    private static AppIdentity? ReadProcess(int processId, string expectedProcess)
    {
        try
        {
            string? path = ReadExecutablePath(processId, expectedProcess);
            if (path is null) return null;
            return ReadFile(path, expectedProcess);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException or UnauthorizedAccessException)
        { return null; }
    }

    public static string? ReadExecutablePath(int processId, string expectedProcess)
    {
        // MainModule also requests PROCESS_VM_READ, which browser sandboxes may deny.
        // QueryFullProcessImageName needs only PROCESS_QUERY_LIMITED_INFORMATION.
        using var handle = OpenProcess(0x1000, false, processId);
        if (handle.IsInvalid) return null;
        var buffer = new StringBuilder(32768);
        int size = buffer.Capacity;
        if (!QueryFullProcessImageName(handle, 0, buffer, ref size)) return null;
        string path = buffer.ToString();
        // Compare the queried file name to prevent attributing a reused PID to another app.
        return string.Equals(Path.GetFileNameWithoutExtension(path), expectedProcess, StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);
    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, int flags, StringBuilder path, ref int size);

    public static AppIdentity ReadFile(string path, string fallbackName)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return new(fallbackName, FallbackIcon);
            string key = info.FullName + "|" + info.LastWriteTimeUtc.Ticks;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            string name = fallbackName;
            try
            {
                var version = FileVersionInfo.GetVersionInfo(path);
                name = CleanName(version.FileDescription) ?? CleanName(version.ProductName) ?? fallbackName;
            }
            catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException) { }
            ImageSource image = FallbackIcon;
            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon is not null)
                {
                    var bitmap = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
                    bitmap.Freeze(); image = bitmap;
                }
            }
            catch (Exception ex) when (ex is Win32Exception or ArgumentException or IOException or ExternalException) { }
            var result = new AppIdentity(name, image);
            // Bounded memory: icons are only an in-memory convenience cache.
            if (Cache.Count >= 256) Cache.Clear();
            if (image != FallbackIcon) Cache.TryAdd(key, result);
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new(fallbackName, FallbackIcon); }
    }

    private static string? CleanName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        name = name.Replace("\r", " ").Replace("\n", " ").Trim();
        return name.Length > 100 ? name[..100] : name;
    }

    private static ImageSource CreateFallback()
    {
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(48, 63, 62)), null, new Rect(0, 0, 32, 32), 7, 7);
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(140, 230, 190)), 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawLine(pen, new Point(10, 13), new Point(10, 19));
            dc.DrawLine(pen, new Point(16, 8), new Point(16, 24));
            dc.DrawLine(pen, new Point(22, 11), new Point(22, 21));
        }
        drawing.Freeze();
        var image = new DrawingImage(drawing); image.Freeze(); return image;
    }
}

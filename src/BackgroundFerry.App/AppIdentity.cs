using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
        try
        {
            using var process = Process.GetProcessById(processId);
            // Avoid attributing an icon to a reused PID.
            if (!string.Equals(process.ProcessName, expectedProcess, StringComparison.OrdinalIgnoreCase)) return null;
            string? path = process.MainModule?.FileName;
            if (string.IsNullOrEmpty(path)) return null;
            return ReadFile(path, expectedProcess);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException or UnauthorizedAccessException)
        { return null; }
    }

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
            Cache.TryAdd(key, result);
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

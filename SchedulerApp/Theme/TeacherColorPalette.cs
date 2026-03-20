using System;
using System.Collections.Concurrent;
using Avalonia.Media;

namespace SchedulerApp.Theme;

public readonly record struct TeacherColorSwatch(
    byte BaseR, byte BaseG, byte BaseB,
    byte BackgroundR, byte BackgroundG, byte BackgroundB,
    byte BorderR, byte BorderG, byte BorderB
)
{
    public IBrush BaseBrush => new SolidColorBrush(Color.FromRgb(BaseR, BaseG, BaseB));
    public IBrush BackgroundBrush => new SolidColorBrush(Color.FromRgb(BackgroundR, BackgroundG, BackgroundB));
    public IBrush BorderBrush => new SolidColorBrush(Color.FromRgb(BorderR, BorderG, BorderB));
}

public static class TeacherColorPalette
{
    private static readonly ConcurrentDictionary<string, TeacherColorSwatch> Cache = new(StringComparer.Ordinal);

    public static TeacherColorSwatch Get(string teacherId, string? colorHex = null)
    {
        var id = teacherId ?? string.Empty;
        var normalized = NormalizeHex(colorHex);
        var key = $"{id}|{normalized ?? string.Empty}";
        return Cache.GetOrAdd(key, _ => Create(id, normalized));
    }

    private static TeacherColorSwatch Create(string teacherId, string? normalizedHex)
    {
        (byte r, byte g, byte b) = normalizedHex is null
            ? HslToRgb((int)(Fnv1a32(teacherId) % 360u), 0.62, 0.45)
            : ParseHexRgb(normalizedHex);
        var (bgR, bgG, bgB) = BlendToWhite(r, g, b, 0.88);
        var (bdR, bdG, bdB) = BlendToWhite(r, g, b, 0.68);
        return new TeacherColorSwatch(r, g, b, bgR, bgG, bgB, bdR, bdG, bdB);
    }

    private static string? NormalizeHex(string? colorHex)
    {
        var s = colorHex?.Trim();
        if (string.IsNullOrWhiteSpace(s))
            return null;
        if (string.Equals(s, "auto", StringComparison.OrdinalIgnoreCase))
            return null;

        if (s.StartsWith("#", StringComparison.Ordinal))
            s = s[1..];

        if (s.Length != 6)
            return null;

        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            var isHex =
                (c >= '0' && c <= '9') ||
                (c >= 'a' && c <= 'f') ||
                (c >= 'A' && c <= 'F');
            if (!isHex)
                return null;
        }

        return "#" + s.ToUpperInvariant();
    }

    private static (byte r, byte g, byte b) ParseHexRgb(string normalizedHex)
    {
        var s = normalizedHex.AsSpan(1, 6);
        return (
            Convert.ToByte(s[..2].ToString(), 16),
            Convert.ToByte(s.Slice(2, 2).ToString(), 16),
            Convert.ToByte(s.Slice(4, 2).ToString(), 16)
        );
    }

    private static uint Fnv1a32(string s)
    {
        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            var hash = offset;
            for (var i = 0; i < s.Length; i++)
            {
                hash ^= s[i];
                hash *= prime;
            }
            return hash;
        }
    }

    private static (byte r, byte g, byte b) BlendToWhite(byte r, byte g, byte b, double t)
    {
        var rr = (byte)Math.Clamp((int)Math.Round(r + (255 - r) * t), 0, 255);
        var gg = (byte)Math.Clamp((int)Math.Round(g + (255 - g) * t), 0, 255);
        var bb = (byte)Math.Clamp((int)Math.Round(b + (255 - b) * t), 0, 255);
        return (rr, gg, bb);
    }

    private static (byte r, byte g, byte b) HslToRgb(int h, double s, double l)
    {
        var hh = ((h % 360) + 360) % 360;
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs((hh / 60.0) % 2 - 1));
        var m = l - c / 2;

        (double r1, double g1, double b1) = hh switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x)
        };

        var r = (byte)Math.Clamp((int)Math.Round((r1 + m) * 255), 0, 255);
        var g = (byte)Math.Clamp((int)Math.Round((g1 + m) * 255), 0, 255);
        var b = (byte)Math.Clamp((int)Math.Round((b1 + m) * 255), 0, 255);
        return (r, g, b);
    }
}

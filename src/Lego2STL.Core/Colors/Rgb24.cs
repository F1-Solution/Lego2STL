using System.Globalization;

namespace Lego2STL.Core.Colors;

/// <summary>
/// An 8-bit-per-channel sRGB colour, with the CIELAB conversion needed to compare
/// a reference colour against pixels sampled from a rendered part.
/// </summary>
/// <remarks>
/// Perceptual distance matters here rather than raw RGB distance: shading in the PDF's
/// renders moves colours a long way in RGB while keeping them recognisable, and some
/// genuinely distinct LEGO colours (Light Gray vs Light Bluish Gray) sit only a few
/// RGB units apart. Delta-E in Lab space is what makes "these two are indistinguishable,
/// so abstain" a defensible statement.
/// </remarks>
public readonly record struct Rgb24(byte R, byte G, byte B)
{
    public static Rgb24 Parse(string hex)
    {
        if (!TryParse(hex, out var rgb))
        {
            throw new FormatException($"'{hex}' is not a 6-digit hex colour (with or without a leading '#').");
        }

        return rgb;
    }

    public static bool TryParse(string? hex, out Rgb24 rgb)
    {
        rgb = default;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        var s = hex.Trim();
        if (s.StartsWith('#'))
        {
            s = s[1..];
        }

        if (s.Length != 6)
        {
            return false;
        }

        if (!byte.TryParse(s.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
            !byte.TryParse(s.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
            !byte.TryParse(s.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        rgb = new Rgb24(r, g, b);
        return true;
    }

    /// <summary>Uppercase six-digit hex with a leading '#', as written to the CSV.</summary>
    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>CIE76 colour difference. Roughly: under ~2.3 is a just-noticeable difference.</summary>
    public double DeltaE(Rgb24 other)
    {
        var (l1, a1, b1) = ToLab();
        var (l2, a2, b2) = other.ToLab();
        var dl = l1 - l2;
        var da = a1 - a2;
        var db = b1 - b2;
        return Math.Sqrt((dl * dl) + (da * da) + (db * db));
    }

    /// <summary>Converts to CIELAB under the D65 illuminant.</summary>
    public (double L, double A, double B) ToLab()
    {
        var (x, y, z) = ToXyz();

        // D65 reference white.
        var fx = PivotXyz(x / 0.95047);
        var fy = PivotXyz(y / 1.00000);
        var fz = PivotXyz(z / 1.08883);

        return ((116.0 * fy) - 16.0, 500.0 * (fx - fy), 200.0 * (fy - fz));
    }

    private (double X, double Y, double Z) ToXyz()
    {
        var r = Linearise(R / 255.0);
        var g = Linearise(G / 255.0);
        var b = Linearise(B / 255.0);

        return (
            (r * 0.4124564) + (g * 0.3575761) + (b * 0.1804375),
            (r * 0.2126729) + (g * 0.7151522) + (b * 0.0721750),
            (r * 0.0193339) + (g * 0.1191920) + (b * 0.9503041));
    }

    /// <summary>Undoes the sRGB transfer function.</summary>
    private static double Linearise(double c) =>
        c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    private static double PivotXyz(double t) =>
        t > 0.008856 ? Math.Cbrt(t) : ((903.3 * t) + 16.0) / 116.0;
}

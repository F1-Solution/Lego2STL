namespace Lego2STL.Core.Extraction;

/// <summary>
/// Turns a glyph's pixels into a fixed-size grey patch, so two glyphs can be compared
/// regardless of how many pixels each happens to occupy.
/// </summary>
public static class GlyphPatch
{
    /// <summary>Patch width. Wide enough for the widest glyph without squashing narrow ones flat.</summary>
    public const int Width = 16;

    /// <summary>Patch height, taken from the measured glyph height of 17-18 px plus headroom.</summary>
    public const int Height = 24;

    public const int Length = Width * Height;

    /// <summary>
    /// Samples a glyph into a <see cref="Length"/>-element patch, values from 0 to 1.
    /// </summary>
    /// <remarks>
    /// Area averaging rather than point sampling: a glyph is only a dozen pixels across, so
    /// every source pixel has to contribute or thin strokes disappear and the comparison
    /// becomes noise.
    /// </remarks>
    public static float[] Sample(InkMask ink, PixelBounds glyph)
    {
        ArgumentNullException.ThrowIfNull(ink);

        var patch = new float[Length];
        var sourceWidth = glyph.Width;
        var sourceHeight = glyph.Height;

        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return patch;
        }

        for (var ty = 0; ty < Height; ty++)
        {
            // Source rows covered by this patch row.
            var y0 = glyph.Top + (ty * sourceHeight / Height);
            var y1 = glyph.Top + (((ty + 1) * sourceHeight / Height) - 1);
            if (y1 < y0)
            {
                y1 = y0;
            }

            for (var tx = 0; tx < Width; tx++)
            {
                var x0 = glyph.Left + (tx * sourceWidth / Width);
                var x1 = glyph.Left + (((tx + 1) * sourceWidth / Width) - 1);
                if (x1 < x0)
                {
                    x1 = x0;
                }

                var set = 0;
                var total = 0;

                for (var y = y0; y <= y1; y++)
                {
                    for (var x = x0; x <= x1; x++)
                    {
                        total++;
                        if (x >= 0 && y >= 0 && x < ink.Width && y < ink.Height && ink[x, y])
                        {
                            set++;
                        }
                    }
                }

                patch[(ty * Width) + tx] = total == 0 ? 0f : set / (float)total;
            }
        }

        return patch;
    }

    /// <summary>Sum of squared differences between two patches. Lower is a better match.</summary>
    public static double Distance(float[] a, float[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Length != b.Length)
        {
            throw new ArgumentException("Patches must be the same size.", nameof(b));
        }

        double sum = 0;
        for (var i = 0; i < a.Length; i++)
        {
            var d = a[i] - b[i];
            sum += d * d;
        }

        return sum;
    }
}

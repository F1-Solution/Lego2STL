using SkiaSharp;

namespace Lego2STL.Core.Extraction;

/// <summary>
/// A one-bit-per-pixel view of a page: true where there is ink.
/// </summary>
public sealed class InkMask
{
    private readonly bool[] _pixels;

    private InkMask(bool[] pixels, int width, int height)
    {
        _pixels = pixels;
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public bool this[int x, int y] => _pixels[(y * Width) + x];

    /// <summary>
    /// Marks every pixel darker than <paramref name="threshold"/> on a 0-255 luminance scale.
    /// </summary>
    /// <remarks>
    /// A single global threshold is adequate here, and deliberately chosen over adaptive
    /// thresholding: these pages are synthetic renders on pure white paper, so ink and
    /// paper are separated by a very wide margin. Measured on the reference document, label
    /// text and part renders sit below luminance 90 while paper sits above 235.
    /// </remarks>
    public static InkMask FromBitmap(SKBitmap bitmap, byte threshold = 128)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var width = bitmap.Width;
        var height = bitmap.Height;
        var pixels = new bool[width * height];

        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            for (var x = 0; x < width; x++)
            {
                var c = bitmap.GetPixel(x, y);

                // Rec. 601 luma, integer arithmetic.
                var luma = ((299 * c.Red) + (587 * c.Green) + (114 * c.Blue)) / 1000;
                pixels[row + x] = luma < threshold;
            }
        }

        return new InkMask(pixels, width, height);
    }

    /// <summary>An empty mask of the same size.</summary>
    public static InkMask Empty(int width, int height) => new(new bool[width * height], width, height);

    /// <summary>Fills a rectangle. Used to turn glyph bounding boxes into solid blocks.</summary>
    public void Fill(PixelBounds box)
    {
        var left = Math.Max(0, box.Left);
        var right = Math.Min(Width - 1, box.Right);
        var top = Math.Max(0, box.Top);
        var bottom = Math.Min(Height - 1, box.Bottom);

        for (var y = top; y <= bottom; y++)
        {
            var row = y * Width;
            for (var x = left; x <= right; x++)
            {
                _pixels[row + x] = true;
            }
        }
    }

    /// <summary>
    /// Grows the mask by <paramref name="radiusX"/> horizontally and
    /// <paramref name="radiusY"/> vertically, so that nearby marks merge into one blob.
    /// </summary>
    /// <remarks>
    /// This is what groups the glyphs of a label together, and the radii are the parameter
    /// that decides where one label ends and the next begins. Separable, because a
    /// rectangular structuring element factors into a horizontal then a vertical pass.
    /// </remarks>
    public InkMask Dilate(int radiusY, int radiusX)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radiusY);
        ArgumentOutOfRangeException.ThrowIfNegative(radiusX);

        var horizontal = DilateHorizontally(_pixels, Width, Height, radiusX);
        var both = DilateVertically(horizontal, Width, Height, radiusY);
        return new InkMask(both, Width, Height);
    }

    private static bool[] DilateHorizontally(bool[] source, int width, int height, int radius)
    {
        if (radius == 0)
        {
            return (bool[])source.Clone();
        }

        var result = new bool[source.Length];

        for (var y = 0; y < height; y++)
        {
            var row = y * width;

            // Sliding count of set pixels in the window, so cost is independent of radius.
            var set = 0;
            for (var x = 0; x <= Math.Min(radius, width - 1); x++)
            {
                if (source[row + x])
                {
                    set++;
                }
            }

            for (var x = 0; x < width; x++)
            {
                result[row + x] = set > 0;

                var leaving = x - radius;
                var entering = x + radius + 1;

                if (leaving >= 0 && source[row + leaving])
                {
                    set--;
                }

                if (entering < width && source[row + entering])
                {
                    set++;
                }
            }
        }

        return result;
    }

    private static bool[] DilateVertically(bool[] source, int width, int height, int radius)
    {
        if (radius == 0)
        {
            return source;
        }

        var result = new bool[source.Length];

        for (var x = 0; x < width; x++)
        {
            var set = 0;
            for (var y = 0; y <= Math.Min(radius, height - 1); y++)
            {
                if (source[(y * width) + x])
                {
                    set++;
                }
            }

            for (var y = 0; y < height; y++)
            {
                result[(y * width) + x] = set > 0;

                var leaving = y - radius;
                var entering = y + radius + 1;

                if (leaving >= 0 && source[(leaving * width) + x])
                {
                    set--;
                }

                if (entering < height && source[(entering * width) + x])
                {
                    set++;
                }
            }
        }

        return result;
    }

    /// <summary>True when any pixel is set on the given row within the given columns.</summary>
    public bool AnyInRow(int y, int left, int right)
    {
        var row = y * Width;
        for (var x = Math.Max(0, left); x <= Math.Min(Width - 1, right); x++)
        {
            if (_pixels[row + x])
            {
                return true;
            }
        }

        return false;
    }
}

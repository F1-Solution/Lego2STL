using Lego2STL.Core.Extraction;
using SkiaSharp;

namespace Lego2STL.Core.Ocr;

/// <summary>
/// Cuts a single line of text out of a page, ready for the text recogniser.
/// </summary>
/// <remarks>
/// <para>
/// Two details here were established by measurement and both matter more than they look.
/// </para>
/// <para>
/// First, the crop is taken at the page's own resolution and never enlarged. Enlarging
/// makes recognition worse, not better: on the reference document a crop scaled up four
/// times lost the colour code entirely, while the same crop at its native size read
/// perfectly. There is no extra detail to recover, so interpolating only adds artefacts.
/// </para>
/// <para>
/// Second, the crop is placed on a generous white border. Without it the recogniser
/// invented a trailing digit on several labels ("32250, 11" came back as "32250, 111") and
/// returned nothing at all for two others. With it, both problems disappeared.
/// </para>
/// </remarks>
public static class RowCrop
{
    /// <summary>Pixels of real page kept around the glyphs, to avoid clipping antialiasing.</summary>
    public const int DefaultPadding = 2;

    /// <summary>Width of the blank border added around the crop.</summary>
    public const int DefaultMargin = 18;

    /// <summary>
    /// Returns the given region of the page on a white border. The caller owns the result.
    /// </summary>
    public static SKBitmap Extract(
        SKBitmap page,
        PixelBounds region,
        int padding = DefaultPadding,
        int margin = DefaultMargin)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentOutOfRangeException.ThrowIfNegative(padding);
        ArgumentOutOfRangeException.ThrowIfNegative(margin);

        var padded = region.Inflate(padding, page.Width, page.Height);

        var target = new SKBitmap(padded.Width + (2 * margin), padded.Height + (2 * margin));

        using var canvas = new SKCanvas(target);
        canvas.Clear(SKColors.White);

        // Extract then blit at 1:1, so no resampling can happen at all. Scaling is exactly
        // what must not occur here: an enlarged crop reads worse than the original.
        var source = new SKRectI(padded.Left, padded.Top, padded.Right + 1, padded.Bottom + 1);

        using var subset = new SKBitmap();
        if (!page.ExtractSubset(subset, source))
        {
            target.Dispose();
            throw new InvalidOperationException(
                $"Could not take the region {padded} out of a {page.Width}x{page.Height} page.");
        }

        canvas.DrawBitmap(subset, new SKPoint(margin, margin), SKSamplingOptions.Default);

        return target;
    }

    /// <summary>Encodes a crop as PNG, for the review images shown when a reading is uncertain.</summary>
    public static byte[] ToPng(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}

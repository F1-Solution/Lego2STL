using Lego2STL.Core.Extraction;
using Lego2STL.Core.Pdf;

namespace Lego2STL.Core.Ocr;

/// <summary>
/// One catalogue entry as first read: whatever the recogniser returned, what could be
/// parsed from it, and the character shapes, kept for the second pass.
/// </summary>
/// <param name="Label">Where on the page it came from.</param>
/// <param name="RawText">Exactly what the recogniser returned, lines separated by newlines.</param>
/// <param name="Quantity">The count, or null when the recogniser did not produce a usable quantity line.</param>
/// <param name="PartNumber">The part number, or null when the part line could not be parsed.</param>
/// <param name="ColorCode">The colour number in the input's own scheme, or null.</param>
/// <param name="QuantityGlyphs">Sampled shapes of the upper line's characters.</param>
/// <param name="PartGlyphs">Sampled shapes of the lower line's characters.</param>
public sealed record LabelSample(
    PartLabel Label,
    string RawText,
    int? Quantity,
    string? PartNumber,
    int? ColorCode,
    IReadOnlyList<float[]> QuantityGlyphs,
    IReadOnlyList<float[]> PartGlyphs)
{
    public bool IsComplete => Quantity is not null && PartNumber is not null && ColorCode is not null;

    public bool HasPart => PartNumber is not null && ColorCode is not null;
}

/// <summary>
/// Reads one catalogue entry with the text recogniser, and samples its character shapes.
/// </summary>
/// <remarks>
/// The entry is given to the recogniser as a single crop rather than line by line, because
/// that measured better: on the reference document a whole-entry crop yielded 45 complete
/// readings out of 53 against 33 for separate lines, and every part line either way. What
/// the recogniser misses is filled in afterwards by <see cref="GlyphTemplateSet"/>.
/// </remarks>
public sealed class LabelReader
{
    private readonly IOcrEngine _engine;
    private readonly int _cropMargin;

    public LabelReader(IOcrEngine engine, int cropMargin = RowCrop.DefaultMargin)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _cropMargin = cropMargin;
    }

    /// <summary>Reads one entry. <paramref name="ink"/> must be the mask of the same page.</summary>
    public async Task<LabelSample> ReadAsync(
        PageImage page,
        InkMask ink,
        PartLabel label,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(ink);
        ArgumentNullException.ThrowIfNull(label);

        string text;
        using (var crop = RowCrop.Extract(page.Bitmap, label.Bounds, margin: _cropMargin))
        {
            text = await _engine.ReadAsync(crop, cancellationToken).ConfigureAwait(false);
        }

        var (quantity, partNumber, colorCode) = LabelTextGrammar.Scan(text);

        return new LabelSample(
            label,
            text,
            quantity,
            partNumber,
            colorCode,
            Sample(ink, label.QuantityRow),
            Sample(ink, label.PartRow));
    }

    private static IReadOnlyList<float[]> Sample(InkMask ink, TextRow row) =>
        row.Glyphs.Select(g => GlyphPatch.Sample(ink, g.Bounds)).ToList();
}

namespace Lego2STL.Core.Extraction;

/// <summary>One line of text found on a page, with the glyphs that make it up.</summary>
public sealed record TextRow(PixelBounds Bounds, IReadOnlyList<InkComponent> Glyphs)
{
    public int InkPixels => Glyphs.Sum(g => g.PixelCount);

    /// <summary>Mean ink per glyph. Real text runs far denser than render detail.</summary>
    public double MeanGlyphInk => Glyphs.Count == 0 ? 0 : InkPixels / (double)Glyphs.Count;
}

/// <summary>
/// A catalogue entry located on a page: a quantity line above a part-and-colour line.
/// </summary>
/// <remarks>
/// Only geometry at this stage; nothing has been read yet. Keeping location and recognition
/// apart is what makes the pipeline testable, and it is also what makes recognition work:
/// the OCR engine is close to useless on a whole page and close to perfect on a single
/// cropped line.
/// </remarks>
public sealed record PartLabel(int PageNumber, PixelBounds Bounds, IReadOnlyList<TextRow> Rows)
{
    /// <summary>The upper line, which carries the quantity.</summary>
    public TextRow QuantityRow => Rows[0];

    /// <summary>The lower line, which carries the part number and colour code.</summary>
    public TextRow PartRow => Rows[^1];

    /// <summary>Reading order on the page: top to bottom, then left to right.</summary>
    public static int CompareByReadingOrder(PartLabel a, PartLabel b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var byTop = a.Bounds.Top.CompareTo(b.Bounds.Top);
        return byTop != 0 ? byTop : a.Bounds.Left.CompareTo(b.Bounds.Left);
    }
}

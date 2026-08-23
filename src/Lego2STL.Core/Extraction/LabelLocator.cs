using Lego2STL.Core.Pdf;

namespace Lego2STL.Core.Extraction;

/// <summary>
/// Finds the catalogue entries on a page geometrically, before anything is read.
/// </summary>
/// <remarks>
/// <para>
/// The order of operations is the whole point. Handing a full page to an OCR engine produces
/// nonsense - on the reference document it returned three characters for a page holding five
/// labels - while handing it a single cropped line produces exactly the right text. So the
/// page is segmented first, using only the shapes of the marks on it.
/// </para>
/// <para>
/// Steps: find every region of ink; keep the ones the size of a glyph; replace each with a
/// solid block; grow the blocks until the glyphs of one entry merge and neighbouring entries
/// do not; then split each blob back into text lines with a horizontal projection, and keep
/// the pairs of lines that look like text.
/// </para>
/// </remarks>
public sealed class LabelLocator
{
    private readonly LabelLocatorOptions _options;

    public LabelLocator(LabelLocatorOptions? options = null) =>
        _options = options ?? new LabelLocatorOptions();

    /// <summary>Locates the catalogue entries on one page, in reading order.</summary>
    public IReadOnlyList<PartLabel> Locate(PageImage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var ink = InkMask.FromBitmap(page.Bitmap, _options.InkThreshold);
        var glyphs = ConnectedComponents.Find(ink).Where(IsGlyphSized).ToList();

        if (glyphs.Count == 0)
        {
            return [];
        }

        var labels = new List<PartLabel>();

        foreach (var blob in FindBlobs(ink, glyphs))
        {
            labels.AddRange(ExtractLabels(page.PageNumber, ink, blob));
        }

        labels.Sort(PartLabel.CompareByReadingOrder);
        return labels;
    }

    /// <summary>Glyph-sized regions of ink, which is what makes part renders and headings drop out.</summary>
    private bool IsGlyphSized(InkComponent c) =>
        c.Height >= _options.MinGlyphHeight &&
        c.Height <= _options.MaxGlyphHeight &&
        c.Width >= _options.MinGlyphWidth &&
        c.Width <= _options.MaxGlyphWidth &&
        c.PixelCount >= _options.MinGlyphPixels;

    /// <summary>
    /// Groups glyphs into clusters by turning each into a solid block, growing the blocks
    /// until neighbours touch, and taking the connected regions of the result.
    /// </summary>
    private List<List<InkComponent>> FindBlobs(InkMask ink, List<InkComponent> glyphs)
    {
        var blocks = InkMask.Empty(ink.Width, ink.Height);
        foreach (var glyph in glyphs)
        {
            blocks.Fill(glyph.Bounds);
        }

        var grown = blocks.Dilate(_options.DilateY, _options.DilateX);

        // Assign each glyph to the blob that contains its centre. Cheaper and less
        // ambiguous than testing every glyph against every blob rectangle.
        var blobIndexByBounds = new List<PixelBounds>();
        foreach (var blob in ConnectedComponents.Find(grown))
        {
            blobIndexByBounds.Add(blob.Bounds);
        }

        var buckets = new List<List<InkComponent>>();
        for (var i = 0; i < blobIndexByBounds.Count; i++)
        {
            buckets.Add([]);
        }

        foreach (var glyph in glyphs)
        {
            var centreX = (glyph.Bounds.Left + glyph.Bounds.Right) / 2;
            var centreY = (glyph.Bounds.Top + glyph.Bounds.Bottom) / 2;

            for (var i = 0; i < blobIndexByBounds.Count; i++)
            {
                var b = blobIndexByBounds[i];
                if (centreX >= b.Left && centreX <= b.Right && centreY >= b.Top && centreY <= b.Bottom)
                {
                    buckets[i].Add(glyph);
                    break;
                }
            }
        }

        return buckets.Where(b => b.Count >= _options.MinGlyphsPerBlob).ToList();
    }

    /// <summary>
    /// Splits one blob into text lines and returns the line groups that look like a
    /// catalogue entry.
    /// </summary>
    private IEnumerable<PartLabel> ExtractLabels(int pageNumber, InkMask ink, List<InkComponent> blob)
    {
        var rows = SplitIntoRows(blob);

        foreach (var group in GroupRowsIntoLabels(rows))
        {
            if (group.Count != _options.RowsPerLabel)
            {
                continue;
            }

            // Marks inside a part's line art can pass the glyph size filter and cluster into
            // something the right shape. Real text is far denser, so density is what
            // separates them.
            if (group.Any(r => r.MeanGlyphInk < _options.MinMeanGlyphInk))
            {
                continue;
            }

            yield return new PartLabel(pageNumber, PixelBounds.Around(group.Select(r => r.Bounds)), group);
        }
    }

    /// <summary>
    /// Splits a blob into text lines using a horizontal projection of its glyph blocks:
    /// a run of rows containing ink is a line, and a gap ends it.
    /// </summary>
    private List<TextRow> SplitIntoRows(List<InkComponent> blob)
    {
        var bounds = PixelBounds.Around(blob.Select(g => g.Bounds));

        // Project the glyph blocks, not the original ink, so that a descending comma still
        // belongs to the line its digits are on.
        var occupied = new bool[bounds.Height];
        foreach (var glyph in blob)
        {
            for (var y = glyph.Bounds.Top; y <= glyph.Bounds.Bottom; y++)
            {
                occupied[y - bounds.Top] = true;
            }
        }

        var rows = new List<TextRow>();
        var runStart = -1;

        for (var i = 0; i <= occupied.Length; i++)
        {
            var inRun = i < occupied.Length && occupied[i];

            if (inRun && runStart < 0)
            {
                runStart = i;
            }
            else if (!inRun && runStart >= 0)
            {
                AddRow(rows, blob, bounds.Top + runStart, bounds.Top + i - 1);
                runStart = -1;
            }
        }

        return rows;
    }

    private void AddRow(List<TextRow> rows, List<InkComponent> blob, int top, int bottom)
    {
        if (bottom - top + 1 < _options.MinRowHeight)
        {
            return;
        }

        var glyphs = blob
            .Where(g => g.Bounds.Top >= top - 1 && g.Bounds.Bottom <= bottom + 1)
            .OrderBy(g => g.Bounds.Left)
            .ToList();

        if (glyphs.Count == 0)
        {
            return;
        }

        rows.Add(new TextRow(PixelBounds.Around(glyphs.Select(g => g.Bounds)), glyphs));
    }

    /// <summary>
    /// Groups consecutive lines that are close enough vertically to belong to one entry.
    /// </summary>
    /// <remarks>
    /// Measured on the reference document: the gap between an entry's own two lines is
    /// consistently 7 px, while the gap up to unrelated marks is 19 px or more. That
    /// separation is what makes a fixed threshold safe here.
    /// </remarks>
    private List<List<TextRow>> GroupRowsIntoLabels(List<TextRow> rows)
    {
        var groups = new List<List<TextRow>>();
        if (rows.Count == 0)
        {
            return groups;
        }

        var current = new List<TextRow> { rows[0] };

        foreach (var row in rows.Skip(1))
        {
            var gap = row.Bounds.Top - current[^1].Bounds.Bottom;
            if (gap <= _options.MaxLineGap)
            {
                current.Add(row);
            }
            else
            {
                groups.Add(current);
                current = [row];
            }
        }

        groups.Add(current);
        return groups;
    }
}

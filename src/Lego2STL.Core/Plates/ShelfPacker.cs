using System.Globalization;
using System.Numerics;

namespace Lego2STL.Core.Plates;

/// <summary>Something to place on a plate, and how much floor it takes.</summary>
/// <param name="PartNumber">Which shape it is; several copies share one.</param>
/// <param name="Footprint">Width and depth in millimetres.</param>
/// <param name="Height">How tall, so a part too tall for the machine can be reported.</param>
public sealed record PackableItem(string PartNumber, Vector2 Footprint, float Height);

/// <summary>An item placed on a plate, at the near-left corner of its footprint.</summary>
public sealed record PlacedItem(PackableItem Item, float X, float Y);

/// <summary>One plate's worth of placements.</summary>
public sealed record PackedPlate(int Number, IReadOnlyList<PlacedItem> Items, Vector2 Used)
{
    public int PieceCount => Items.Count;

    public string DescribeUsed() =>
        string.Create(CultureInfo.InvariantCulture, $"{Used.X:0.#} x {Used.Y:0.#}");
}

/// <summary>An item no plate could take, and why.</summary>
public sealed record OversizedItem(PackableItem Item, bool TooTall);

/// <summary>What came out of packing.</summary>
public sealed record PackingResult(
    IReadOnlyList<PackedPlate> Plates,
    IReadOnlyList<OversizedItem> Oversized);

/// <summary>How much room to leave.</summary>
public sealed record PackingOptions
{
    public PrintBed Bed { get; init; } = PrintBeds.Default;

    /// <summary>Gap between neighbouring parts, so they can be separated after printing.</summary>
    public float Spacing { get; init; } = 3f;

    /// <summary>
    /// Gap between the outermost parts and the edge of the bed. Clips and the nozzle wiper
    /// live at the edges of most beds, so the whole surface is never really usable.
    /// </summary>
    public float Margin { get; init; } = 5f;
}

/// <summary>
/// Arranges parts on plates by laying them out in rows.
/// </summary>
/// <remarks>
/// <para>
/// Items are sorted by depth and laid left to right along a row; when the next one will not
/// fit, a new row starts above the tallest so far, and when a row will not fit either, a new
/// plate starts. Sorting first is what makes this work: it groups items of similar depth into
/// the same row, so little is wasted above the short ones.
/// </para>
/// <para>
/// A cleverer packer exists, but not for this input. These are LEGO parts, whose footprints
/// come in a handful of sizes because they are all built on the same stud grid, and rows of
/// near-identical depth is precisely the case where laying out in rows loses almost nothing.
/// The gain would be a few percent of one plate, for several times the code and a much harder
/// job of explaining why a part ended up where it did.
/// </para>
/// </remarks>
public static class ShelfPacker
{
    public static PackingResult Pack(IEnumerable<PackableItem> items, PackingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        var o = options ?? new PackingOptions();

        var usableWidth = o.Bed.Width - (2f * o.Margin);
        var usableDepth = o.Bed.Depth - (2f * o.Margin);

        var oversized = new List<OversizedItem>();
        var toPlace = new List<PackableItem>();

        foreach (var item in items)
        {
            var tooTall = item.Height > o.Bed.Height;
            var tooBig = item.Footprint.X > usableWidth || item.Footprint.Y > usableDepth;

            if (tooTall || tooBig)
            {
                oversized.Add(new OversizedItem(item, tooTall));
                continue;
            }

            toPlace.Add(item);
        }

        // Deepest first, then widest, then by part number so that two runs of the same list
        // lay out identically. A layout that moved between runs would make the plate files
        // impossible to compare.
        toPlace.Sort((a, b) =>
        {
            var byDepth = b.Footprint.Y.CompareTo(a.Footprint.Y);
            if (byDepth != 0)
            {
                return byDepth;
            }

            var byWidth = b.Footprint.X.CompareTo(a.Footprint.X);
            return byWidth != 0
                ? byWidth
                : string.CompareOrdinal(a.PartNumber, b.PartNumber);
        });

        var plates = new List<PackedPlate>();
        var placed = new List<PlacedItem>();

        var cursorX = o.Margin;
        var rowBottom = o.Margin;
        var rowDepth = 0f;
        var usedX = 0f;

        void FinishPlate()
        {
            if (placed.Count == 0)
            {
                return;
            }

            plates.Add(new PackedPlate(
                plates.Count + 1,
                [.. placed],
                new Vector2(usedX - o.Margin, rowBottom + rowDepth - o.Margin)));

            placed.Clear();
            cursorX = o.Margin;
            rowBottom = o.Margin;
            rowDepth = 0f;
            usedX = 0f;
        }

        foreach (var item in toPlace)
        {
            // Does it still fit along the current row?
            var needsNewRow = placed.Count > 0
                              && cursorX + item.Footprint.X > o.Margin + usableWidth;

            if (needsNewRow)
            {
                rowBottom += rowDepth + o.Spacing;
                rowDepth = 0f;
                cursorX = o.Margin;
            }

            // Does the row still fit on this plate?
            if (placed.Count > 0 && rowBottom + item.Footprint.Y > o.Margin + usableDepth)
            {
                FinishPlate();
            }

            placed.Add(new PlacedItem(item, cursorX, rowBottom));

            cursorX += item.Footprint.X + o.Spacing;
            usedX = Math.Max(usedX, cursorX - o.Spacing);
            rowDepth = Math.Max(rowDepth, item.Footprint.Y);
        }

        FinishPlate();

        return new PackingResult(plates, oversized);
    }
}

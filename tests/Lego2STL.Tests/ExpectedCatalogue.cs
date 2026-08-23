namespace Lego2STL.Tests;

/// <summary>One expected catalogue entry, transcribed from the page by eye.</summary>
public sealed record CatalogueEntry(int Page, int Quantity, string PartNumber, int ColorCode)
{
    public override string ToString() => $"p{Page} {Quantity}x {PartNumber},{ColorCode}";
}

/// <summary>
/// The reference document's catalogue, read off pages 2-5 by hand.
/// </summary>
/// <remarks>
/// This is the yardstick for the extraction stage, and it was produced by looking at the
/// pages rather than by running the tool, so it is an independent check rather than a
/// snapshot of current behaviour. Colour numbers are BrickLink's, which is what the document
/// prints.
/// </remarks>
public static class ExpectedCatalogue
{
    public static IReadOnlyList<CatalogueEntry> Entries { get; } =
    [
        // Page 2 - all black
        new(2, 1, "6628", 11),
        new(2, 2, "32013", 11),
        new(2, 3, "6632", 11),
        new(2, 6, "3705", 11),
        new(2, 2, "32250", 11),
        new(2, 15, "2780", 11),
        new(2, 8, "60483", 11),
        new(2, 8, "42003", 11),
        new(2, 3, "32140", 11),
        new(2, 7, "4459", 11),
        new(2, 2, "3700", 11),
        new(2, 8, "32523", 11),
        new(2, 2, "32526", 11),
        new(2, 2, "6536", 11),
        new(2, 3, "41678", 11),
        new(2, 2, "32054", 11),
        new(2, 1, "32017", 11),
        new(2, 2, "43857", 11),
        new(2, 1, "32014", 11),
        new(2, 1, "32449", 11),
        new(2, 4, "32316", 11),
        new(2, 1, "32348", 11),

        // Page 3 - all black
        new(3, 5, "32524", 11),
        new(3, 5, "32525", 11),
        new(3, 1, "32271", 11),
        new(3, 1, "3708", 11),
        new(3, 1, "40490", 11),

        // Page 4 - black, greys and red
        new(4, 2, "32018", 11),
        new(4, 6, "32278", 11),
        new(4, 2, "10928", 85),
        new(4, 2, "87083", 85),
        new(4, 10, "4265c", 9),
        new(4, 2, "3713", 9),
        new(4, 3, "2736", 9),
        new(4, 6, "4519", 9),
        new(4, 1, "18651", 9),
        new(4, 1, "32054", 9),
        new(4, 1, "61184", 9),
        new(4, 1, "18654", 86),
        new(4, 2, "32062", 5),
        new(4, 3, "32054", 5),
        new(4, 1, "32316", 5),
        new(4, 1, "32524", 5),
        new(4, 1, "3713", 5),

        // Page 5 - red, brown, tan and blue
        new(5, 1, "40490", 5),
        new(5, 2, "32525", 5),
        new(5, 1, "32278", 5),
        new(5, 1, "15462", 8),
        new(5, 2, "32556", 2),
        new(5, 9, "43093", 7),
        new(5, 38, "32556", 7),
        new(5, 2, "6558", 7),
        new(5, 2, "4274", 7),
    ];

    public static IEnumerable<CatalogueEntry> ForPage(int page) => Entries.Where(e => e.Page == page);

    /// <summary>
    /// The distinct part numbers, which is how many geometry files the run should produce:
    /// colour does not change a part's shape, and several parts appear in more than one
    /// colour (32054 in three, 32556 and 40490 in two each).
    /// </summary>
    public static IReadOnlyCollection<string> DistinctPartNumbers { get; } =
        Entries.Select(e => e.PartNumber).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

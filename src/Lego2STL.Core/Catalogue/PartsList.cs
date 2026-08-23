using Lego2STL.Core.Colors;
using Lego2STL.Core.Ocr;

namespace Lego2STL.Core.Catalogue;

/// <summary>A parts list, with a note of anything that had to be decided while building it.</summary>
public sealed record PartsList(IReadOnlyList<PartEntry> Entries, IReadOnlyList<string> Notes)
{
    public int TotalPieces => Entries.Sum(e => e.Quantity);

    /// <summary>
    /// Distinct part numbers, ignoring colour. This is how many shapes the run will produce:
    /// colour does not change a part's geometry.
    /// </summary>
    public IReadOnlyList<string> DistinctPartNumbers { get; } =
        Entries.Select(e => e.PartNumber)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}

/// <summary>
/// Turns what was read off the pages into a numbered parts list.
/// </summary>
/// <remarks>
/// Two things happen here. Colour numbers are translated into BrickLink's numbering
/// whatever the source used, so that the column always means the same thing; and rows for
/// the same part in the same colour are added together, which is what makes an overlapping
/// page range harmless and keeps the list free of duplicates.
/// </remarks>
public static class PartsListBuilder
{
    public static PartsList Build(
        IEnumerable<CatalogueReading> readings,
        ColorTable colors,
        ColorScheme sourceScheme)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(colors);

        var notes = new List<string>();
        var merged = new List<PartEntry>();
        var byKey = new Dictionary<(string, int), int>();   // key -> index into merged

        foreach (var reading in readings)
        {
            if (!colors.TryGet(sourceScheme, reading.ColorCode, out var color))
            {
                throw new InvalidOperationException(
                    $"Page {reading.Page}: part {reading.PartNumber} has colour {reading.ColorCode}, " +
                    $"which is not a known {sourceScheme} colour. " +
                    (sourceScheme == ColorScheme.BrickLink
                        ? "If the document uses a different numbering, say so with --color-scheme."
                        : $"Is --color-scheme {sourceScheme} right for this document?"));
            }

            if (color.BrickLinkId is not { } brickLinkId)
            {
                throw new InvalidOperationException(
                    $"Page {reading.Page}: part {reading.PartNumber} is '{color.Name}', which has no " +
                    "BrickLink colour number, so it cannot be written to that column.");
            }

            var key = (reading.PartNumber.ToLowerInvariant(), brickLinkId);

            if (byKey.TryGetValue(key, out var existingIndex))
            {
                var existing = merged[existingIndex];
                merged[existingIndex] = existing with { Quantity = existing.Quantity + reading.Quantity };

                notes.Add(
                    $"{reading.PartNumber} in {color.Name} appears more than once; " +
                    $"quantities added to give {merged[existingIndex].Quantity}.");
                continue;
            }

            byKey[key] = merged.Count;
            merged.Add(new PartEntry(
                Id: 0,                       // numbered below, once the order is final
                PartNumber: reading.PartNumber,
                BrickLinkColorCode: brickLinkId,
                ColorName: color.Name,
                Rgb: color.Rgb,
                Quantity: reading.Quantity));
        }

        var numbered = merged
            .Select((entry, index) => entry with { Id = index + 1 })
            .ToList();

        return new PartsList(numbered, notes);
    }
}

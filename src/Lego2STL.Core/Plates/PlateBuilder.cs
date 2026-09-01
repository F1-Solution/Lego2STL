using System.Globalization;
using System.Numerics;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.Text;

namespace Lego2STL.Core.Plates;

/// <summary>A plate that was written, and what went on it.</summary>
/// <param name="ColorName">
/// The colour as the run's language says it, because this is what the file is named after and
/// what the report prints. The BrickLink number beside it is what to match a plate to a parts
/// list entry on, since that does not change with the language.
/// </param>
public sealed record BuiltPlate(
    string FileName,
    string ColorName,
    int BrickLinkColorCode,
    Rgb24 Rgb,
    int Number,
    int PieceCount,
    string Footprint);

/// <summary>A part no plate could take, with the measurements that ruled it out.</summary>
/// <param name="TooTall">
/// Whether it was the height rather than the footprint. Kept apart because a taller bed and a
/// smaller scale are different answers.
/// </param>
public sealed record SkippedPart(
    string PartNumber,
    float Width,
    float Depth,
    float Height,
    bool TooTall);

/// <summary>Everything the plate stage produced.</summary>
/// <param name="Plates">One entry per file written, in the order they were written.</param>
/// <param name="Skipped">
/// Parts left off the plates because no plate could take them, each with the reason. Their
/// shape files still exist, so this is a note rather than a loss.
/// </param>
public sealed record PlateBuildResult(
    IReadOnlyList<BuiltPlate> Plates,
    IReadOnlyList<SkippedPart> Skipped)
{
    public int ColorCount => Plates.Select(p => p.ColorName).Distinct(StringComparer.Ordinal).Count();

    public int PieceCount => Plates.Sum(p => p.PieceCount);
}

/// <summary>
/// Turns a parts list and a set of shapes into plates ready to print, one colour at a time.
/// </summary>
/// <remarks>
/// <para>
/// Grouping by colour is what makes the output usable rather than merely correct. A printer
/// with one nozzle prints one colour per job, so a plate mixing black and red is a plate that
/// has to be taken apart again before anything can be printed. One plate per colour means each
/// file is a job.
/// </para>
/// <para>
/// Quantities are honoured: a list asking for eight of a pin puts eight of them on the plate,
/// because the point of the parts list is how many are needed.
/// </para>
/// </remarks>
public static class PlateBuilder
{
    public static async Task<PlateBuildResult> WriteAsync(
        PartsList list,
        IReadOnlyDictionary<string, IndexedMesh> shapesByPart,
        string directory,
        PackingOptions? options = null,
        DisplayLanguage language = DisplayLanguages.Fallback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(shapesByPart);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var o = options ?? new PackingOptions();
        var words = Strings.For(language);

        Directory.CreateDirectory(directory);

        var written = new List<BuiltPlate>();
        var skipped = new List<SkippedPart>();

        foreach (var colorGroup in GroupByColor(list))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var items = new List<PlateItem>();

            foreach (var entry in colorGroup.Entries)
            {
                if (!shapesByPart.TryGetValue(entry.PartNumber, out var mesh) || mesh.TriangleCount == 0)
                {
                    // No shape for it. The build stage has already said so; not repeated here.
                    continue;
                }

                items.Add(new PlateItem(entry.PartNumber, mesh, entry.Quantity));
            }

            if (items.Count == 0)
            {
                continue;
            }

            // The colour is named in the run's language here and nowhere earlier: the file
            // name, the plate's own title and the report all come from this one wording.
            var colorName = ColorNames.For(language, colorGroup.ColorName);

            var result = await PlateWriter
                .WritePlatesAsync(items, colorName, colorName, colorGroup.Rgb, directory, o, cancellationToken)
                .ConfigureAwait(false);

            skipped.AddRange(result.Skipped);

            foreach (var plate in result.Plates)
            {
                written.Add(new BuiltPlate(
                    plate.FileName,
                    colorName,
                    colorGroup.BrickLinkColorCode,
                    colorGroup.Rgb,
                    plate.Number,
                    plate.PieceCount,
                    plate.Footprint));
            }
        }

        return new PlateBuildResult(written, skipped);
    }

    private sealed record ColorGroup(
        string ColorName,
        int BrickLinkColorCode,
        Rgb24 Rgb,
        IReadOnlyList<PartEntry> Entries);

    /// <summary>
    /// The list by colour, biggest first, so that the plate numbering starts with the colour
    /// there is most of. Entries within a colour keep the order they were read in. The tie is
    /// broken on the stored name rather than the translated one, so the same list produces the
    /// plates in the same order whatever language it is run in.
    /// </summary>
    private static IEnumerable<ColorGroup> GroupByColor(PartsList list) =>
        list.Entries
            .GroupBy(e => e.BrickLinkColorCode)
            .Select(g => new ColorGroup(
                g.First().ColorName,
                g.Key,
                g.First().Rgb,
                [.. g.OrderBy(e => e.Id)]))
            .OrderByDescending(g => g.Entries.Sum(e => e.Quantity))
            .ThenBy(g => g.ColorName, StringComparer.Ordinal);

    /// <summary>Why a part is not on any plate, said the way the report prints it.</summary>
    public static string Describe(SkippedPart part, Strings words, PrintBed bed)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(bed);

        return part.TooTall
            ? words.Format(
                TextKey.ErrPartTooTallForBed,
                part.PartNumber,
                part.Height.ToString("0.#", CultureInfo.InvariantCulture),
                bed.Height.ToString("0.#", CultureInfo.InvariantCulture))
            : words.Format(
                TextKey.ErrPlateTooSmall,
                part.PartNumber,
                string.Create(CultureInfo.InvariantCulture, $"{part.Width:0.#} x {part.Depth:0.#} mm"),
                bed.Name);
    }
}

using System.Globalization;
using System.Numerics;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;

namespace Lego2STL.Core.Plates;

/// <summary>A plate that was written, and what went on it.</summary>
public sealed record BuiltPlate(
    string FileName,
    string ColorName,
    Rgb24 Rgb,
    int Number,
    int PieceCount,
    string Footprint);

/// <summary>Everything the plate stage produced.</summary>
/// <param name="Plates">One entry per file written, in the order they were written.</param>
/// <param name="Skipped">
/// Parts left off the plates because no plate could take them, each with the reason. Their
/// shape files still exist, so this is a note rather than a loss.
/// </param>
public sealed record PlateBuildResult(
    IReadOnlyList<BuiltPlate> Plates,
    IReadOnlyList<string> Skipped)
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(shapesByPart);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var o = options ?? new PackingOptions();

        Directory.CreateDirectory(directory);

        var written = new List<BuiltPlate>();
        var skipped = new List<string>();

        foreach (var colorGroup in GroupByColor(list))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var items = new List<PackableItem>();

            foreach (var entry in colorGroup.Entries)
            {
                if (!shapesByPart.TryGetValue(entry.PartNumber, out var mesh) || mesh.TriangleCount == 0)
                {
                    // No shape for it. The build stage has already said so; not repeated here.
                    continue;
                }

                var (min, max) = mesh.Bounds();
                var size = max - min;
                var item = new PackableItem(
                    entry.PartNumber,
                    new Vector2(size.X, size.Y),
                    size.Z);

                for (var i = 0; i < entry.Quantity; i++)
                {
                    items.Add(item);
                }
            }

            if (items.Count == 0)
            {
                continue;
            }

            var packed = ShelfPacker.Pack(items, o);

            foreach (var over in packed.Oversized.DistinctBy(x => x.Item.PartNumber))
            {
                skipped.Add(Describe(over, o.Bed));
            }

            foreach (var plate in packed.Plates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var name = FileNameFor(colorGroup.ColorName, plate.Number, packed.Plates.Count);
                var contents = Contents(name, colorGroup, plate, shapesByPart);

                await ThreeMfWriter
                    .WriteFileAsync(Path.Combine(directory, name), contents, cancellationToken)
                    .ConfigureAwait(false);

                written.Add(new BuiltPlate(
                    name,
                    colorGroup.ColorName,
                    colorGroup.Rgb,
                    plate.Number,
                    plate.PieceCount,
                    plate.DescribeUsed()));
            }
        }

        return new PlateBuildResult(written, skipped);
    }

    private sealed record ColorGroup(string ColorName, Rgb24 Rgb, IReadOnlyList<PartEntry> Entries);

    /// <summary>
    /// The list by colour, biggest first, so that the plate numbering starts with the colour
    /// there is most of. Entries within a colour keep the order they were read in.
    /// </summary>
    private static IEnumerable<ColorGroup> GroupByColor(PartsList list) =>
        list.Entries
            .GroupBy(e => e.BrickLinkColorCode)
            .Select(g => new ColorGroup(
                g.First().ColorName,
                g.First().Rgb,
                [.. g.OrderBy(e => e.Id)]))
            .OrderByDescending(g => g.Entries.Sum(e => e.Quantity))
            .ThenBy(g => g.ColorName, StringComparer.Ordinal);

    /// <summary>
    /// One entry per distinct shape, carrying every place a copy of it sits, so that the file
    /// holds each mesh once however many copies are on the plate.
    /// </summary>
    private static PlateContents Contents(
        string name,
        ColorGroup group,
        PackedPlate plate,
        IReadOnlyDictionary<string, IndexedMesh> shapesByPart)
    {
        var objects = new List<PlateObject>();

        foreach (var byPart in plate.Items.GroupBy(p => p.Item.PartNumber, StringComparer.Ordinal))
        {
            var mesh = shapesByPart[byPart.Key];
            var (min, _) = mesh.Bounds();

            // Placements are the near-left corner of the footprint, and a shape sits wherever
            // its own origin left it, so shift by the corner of its box to land it exactly.
            var positions = byPart
                .Select(p => new Vector2(p.X - min.X, p.Y - min.Y))
                .ToList();

            objects.Add(new PlateObject(byPart.Key, mesh, positions));
        }

        return new PlateContents(name, group.ColorName, group.Rgb, objects);
    }

    private static string FileNameFor(string colorName, int number, int total)
    {
        var slug = Slug(colorName);
        return total == 1
            ? $"{slug}.3mf"
            : string.Create(CultureInfo.InvariantCulture, $"{slug}-{number}.3mf");
    }

    /// <summary>A colour's name as a file name: lower case, words joined by hyphens.</summary>
    private static string Slug(string name)
    {
        var slug = new string([.. name
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')]);

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        slug = slug.Trim('-');
        return slug.Length == 0 ? "colour" : slug;
    }

    private static string Describe(OversizedItem over, PrintBed bed) =>
        over.TooTall
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{over.Item.PartNumber} stands {over.Item.Height:0.#} mm tall, more than the " +
                $"{bed.Height:0.#} mm this printer has.")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{over.Item.PartNumber} measures {over.Item.Footprint.X:0.#} x " +
                $"{over.Item.Footprint.Y:0.#} mm and does not fit a {bed.Name} bed.");
}

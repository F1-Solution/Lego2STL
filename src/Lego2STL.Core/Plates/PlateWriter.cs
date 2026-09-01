using System.Numerics;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;

namespace Lego2STL.Core.Plates;

/// <summary>One shape to go on a plate, under whatever name the caller knows it by.</summary>
/// <param name="Label">
/// What to call it. A part number when a parts list is being plated, and something else entirely
/// when it is not - a calibration plate carries one part at six clearances, which are six labels.
/// </param>
public sealed record PlateItem(string Label, IndexedMesh Mesh, int Quantity);

/// <summary>A plate file that was written.</summary>
public sealed record WrittenPlate(string FileName, int Number, int PieceCount, string Footprint);

/// <summary>What one call produced: the files, and whatever no bed could take.</summary>
public sealed record PlateWriteResult(
    IReadOnlyList<WrittenPlate> Plates,
    IReadOnlyList<SkippedPart> Skipped);

/// <summary>
/// Arranges named shapes onto plates and writes them, all in one colour.
/// </summary>
/// <remarks>
/// <para>
/// The half of the plate stage that knows nothing about parts lists. Grouping a set by colour,
/// honouring quantities from a catalogue and naming files after a translated colour are the other
/// half, and they live in <see cref="PlateBuilder"/> on top of this.
/// </para>
/// <para>
/// Split apart because a calibration plate carries the same part several times at several
/// clearances, and the only handle the old entry point offered was a dictionary keyed by part
/// number, which cannot say that. Nothing below ever checked that a label was a real part number.
/// </para>
/// </remarks>
public static class PlateWriter
{
    /// <param name="fileStem">What the files are named after, before any plate number.</param>
    /// <param name="colorName">The colour as the caller words it, written into the plate itself.</param>
    public static async Task<PlateWriteResult> WritePlatesAsync(
        IReadOnlyList<PlateItem> items,
        string fileStem,
        string colorName,
        Rgb24 rgb,
        string directory,
        PackingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileStem);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        Directory.CreateDirectory(directory);

        var meshes = new Dictionary<string, IndexedMesh>(StringComparer.Ordinal);
        var packable = new List<PackableItem>();

        foreach (var item in items)
        {
            if (item.Mesh.TriangleCount == 0)
            {
                continue;
            }

            meshes[item.Label] = item.Mesh;

            var (min, max) = item.Mesh.Bounds();
            var size = max - min;
            var one = new PackableItem(item.Label, new Vector2(size.X, size.Y), size.Z);

            for (var i = 0; i < item.Quantity; i++)
            {
                packable.Add(one);
            }
        }

        var packed = ShelfPacker.Pack(packable, options ?? new PackingOptions());
        var skipped = new List<SkippedPart>();

        foreach (var over in packed.Oversized.DistinctBy(x => x.Item.PartNumber, StringComparer.Ordinal))
        {
            skipped.Add(new SkippedPart(
                over.Item.PartNumber,
                over.Item.Footprint.X,
                over.Item.Footprint.Y,
                over.Item.Height,
                over.TooTall));
        }

        var written = new List<WrittenPlate>();

        foreach (var plate in packed.Plates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = PlateFileName.For(fileStem, plate.Number, packed.Plates.Count);

            await ThreeMfWriter
                .WriteFileAsync(
                    Path.Combine(directory, name),
                    Contents(name, colorName, rgb, plate, meshes),
                    cancellationToken)
                .ConfigureAwait(false);

            written.Add(new WrittenPlate(name, plate.Number, plate.PieceCount, plate.DescribeUsed()));
        }

        return new PlateWriteResult(written, skipped);
    }

    /// <summary>
    /// One entry per distinct shape, carrying every place a copy of it sits, so that the file
    /// holds each mesh once however many copies are on the plate.
    /// </summary>
    private static PlateContents Contents(
        string name,
        string colorName,
        Rgb24 rgb,
        PackedPlate plate,
        IReadOnlyDictionary<string, IndexedMesh> meshes)
    {
        var objects = new List<PlateObject>();

        foreach (var byLabel in plate.Items.GroupBy(p => p.Item.PartNumber, StringComparer.Ordinal))
        {
            var mesh = meshes[byLabel.Key];
            var (min, _) = mesh.Bounds();

            // Placements are the near-left corner of the footprint, and a shape sits wherever
            // its own origin left it, so shift by the corner of its box to land it exactly.
            var positions = byLabel
                .Select(p => new Vector2(p.X - min.X, p.Y - min.Y))
                .ToList();

            objects.Add(new PlateObject(byLabel.Key, mesh, positions));
        }

        return new PlateContents(name, colorName, rgb, objects);
    }
}

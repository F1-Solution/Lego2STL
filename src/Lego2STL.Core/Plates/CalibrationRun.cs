using System.Numerics;
using System.Text;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.LDraw;
using Lego2STL.Core.Text;

namespace Lego2STL.Core.Plates;

/// <summary>What a calibration produced, and what it could not.</summary>
public sealed record CalibrationResult(int PieceCount, IReadOnlyList<string> Missing);

/// <summary>
/// Builds a calibration plate and everything that goes beside it, into one folder.
/// </summary>
/// <remarks>
/// In Core rather than in the command, because the window offers the same thing on a button and
/// two callers each assembling the plate would be two plates that agree only for as long as
/// someone keeps them in step.
/// </remarks>
public static class CalibrationRun
{
    /// <summary>The grey a plate of test pieces is written in; nothing here is a real colour.</summary>
    private static Rgb24 Neutral => Rgb24.Parse("#C8C8C8");

    public static async Task<CalibrationResult> WriteAsync(
        IReadOnlyDictionary<string, PartMesh> sources,
        IReadOnlyList<double> steps,
        string printer,
        string directory,
        Strings words,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(words);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var (items, missing) = CalibrationSet.Items(sources, steps, new MeshPipelineOptions());

        if (items.Count == 0)
        {
            return new CalibrationResult(0, missing);
        }

        var packing = new PackingOptions
        {
            Bed = PrintBeds.TryGetByName(printer, out var bed) ? bed : PrintBeds.Default,
        };

        var written = await PlateWriter
            .WritePlatesAsync(
                items,
                "calibration",
                words[TextKey.CalibrationTitle],
                Neutral,
                directory,
                packing,
                cancellationToken)
            .ConfigureAwait(false);

        if (ProcessPreset.For(printer) is { } preset)
        {
            await File.WriteAllTextAsync(
                    Path.Combine(directory, "Lego2STL.json"), preset, cancellationToken)
                .ConfigureAwait(false);
        }

        // The packer is asked where things went rather than told: the sheet's map has to be true,
        // and the order the pieces were handed over is not the order they sit in.
        var packed = ShelfPacker.Pack(
            [.. items.SelectMany(i => Enumerable.Repeat(
                new PackableItem(i.Label, Footprint(i.Mesh), Height(i.Mesh)), i.Quantity))],
            packing);

        await File.WriteAllTextAsync(
                Path.Combine(directory, "how-to-use-these.txt"),
                CalibrationNotes.Write(
                    packed.Plates.Count > 0 ? packed.Plates[0] : new PackedPlate(1, [], Vector2.Zero),
                    missing,
                    printer,
                    words),
                new UTF8Encoding(true),
                cancellationToken)
            .ConfigureAwait(false);

        return new CalibrationResult(written.Plates.Sum(p => p.PieceCount), missing);
    }

    private static Vector2 Footprint(IndexedMesh mesh)
    {
        var (min, max) = mesh.Bounds();
        return new Vector2(max.X - min.X, max.Y - min.Y);
    }

    private static float Height(IndexedMesh mesh)
    {
        var (min, max) = mesh.Bounds();
        return max.Z - min.Z;
    }
}

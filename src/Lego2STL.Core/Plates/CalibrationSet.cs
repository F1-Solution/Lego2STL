using System.Globalization;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.LDraw;

namespace Lego2STL.Core.Plates;

/// <summary>
/// What goes on a calibration plate, and at which clearances.
/// </summary>
/// <remarks>
/// <para>
/// Three mating pairs, because a fit is a property of two parts and not of one, and each pair
/// tests a different joint: an axle in a bush, a stud in a tube, a pin in a Technic hole. The
/// clearance applies to both halves, so a pair at 0.15 has 0.30 mm of gap - which is exactly what
/// a real build produces, where both parts come off the same machine at the same setting.
/// </para>
/// <para>
/// One number comes out of all this, not one per pair. The pipeline insets every face of every
/// part by a single figure and has no way to treat a stud differently from an axle, so the extra
/// pairs are here to check one figure against several joints rather than to produce several.
/// </para>
/// <para>
/// The wide plate is not part of the matrix. It tests warping, which no clearance value changes,
/// and printing it at six clearances would spend bed and filament varying something along an axis
/// that does not affect it. It is here once because it is the check that says whether any of the
/// other readings mean anything.
/// </para>
/// </remarks>
public static class CalibrationSet
{
    /// <summary>The mating pairs, and how many of each go on at every clearance.</summary>
    private static readonly (string PartNumber, int Count)[] Matrix =
    [
        ("3705", 1),   // Technic Axle 4
        ("4265c", 1),  // Technic Bush
        ("3003", 1),   // Brick 2 x 2: the base a stud fit is pressed onto
        ("3003", 1),   // Brick 2 x 2, again: the stud fit needs something to go into
        ("3700", 1),   // Technic Brick 1 x 2 with hole
        ("3673", 1),   // Technic Pin
    ];

    /// <summary>The wide plate that says whether the bed and the first layer are right at all.</summary>
    private const string Witness = "3035";

    /// <summary>
    /// The clearances tried, in millimetres.
    /// </summary>
    /// <remarks>
    /// Here rather than on the command, because the window builds the same plate and two copies
    /// of six numbers is one copy too many.
    /// </remarks>
    public static IReadOnlyList<double> DefaultSteps { get; } = [0.00, 0.05, 0.10, 0.15, 0.20, 0.25];

    public static string WitnessLabel => LabelFor(Witness, 0.0);

    /// <summary>Everything the plate needs from the library, each named once.</summary>
    public static IReadOnlyList<string> PartNumbers =>
        [.. Matrix.Select(m => m.PartNumber).Append(Witness).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <param name="sources">What the library gave back, by part number. A gap here is a missing part.</param>
    /// <param name="template">The pipeline options to build each piece with; its clearance is replaced.</param>
    public static (IReadOnlyList<PlateItem> Items, IReadOnlyList<string> Missing) Items(
        IReadOnlyDictionary<string, PartMesh> sources,
        IReadOnlyList<double> steps,
        MeshPipelineOptions template)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(template);

        var items = new List<PlateItem>();
        var missing = new List<string>();

        foreach (var (partNumber, count) in Matrix)
        {
            if (!sources.TryGetValue(partNumber, out var source))
            {
                missing.Add(partNumber);
                continue;
            }

            foreach (var step in steps)
            {
                items.Add(new PlateItem(LabelFor(partNumber, step), Built(source, template, step), count));
            }
        }

        if (sources.TryGetValue(Witness, out var wide))
        {
            items.Add(new PlateItem(WitnessLabel, Built(wide, template, 0.0), 1));
        }
        else
        {
            missing.Add(Witness);
        }

        return (items, missing);
    }

    private static IndexedMesh Built(PartMesh source, MeshPipelineOptions template, double step) =>
        MeshPipeline.Prepare(source, template with
        {
            // Covered whatever was asked for: a calibration piece that silently came out at true
            // size would send the whole exercise wrong.
            FillGaps = true,
            ClearanceMillimetres = (float)step,
        }).Mesh;

    private static string LabelFor(string partNumber, double step) =>
        string.Create(CultureInfo.InvariantCulture, $"{partNumber}-{step:0.00}mm");
}

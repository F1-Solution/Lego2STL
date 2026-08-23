using System.Numerics;
using Lego2STL.Core.LDraw;

namespace Lego2STL.Core.Geometry;

/// <summary>How the geometry should be prepared.</summary>
public sealed record MeshPipelineOptions
{
    /// <summary>Distance within which two corners are treated as the same point.</summary>
    public float WeldTolerance { get; init; } = VertexWelder.DefaultTolerance;

    /// <summary>Close seams where a corner lies part-way along another triangle's edge.</summary>
    public bool RepairSeams { get; init; } = true;

    /// <summary>
    /// Millimetres per source unit. The source measures in units of 0.4 mm, so a standard
    /// brick is 20 units and comes out 8 mm wide.
    /// </summary>
    public float MillimetresPerUnit { get; init; } = 0.4f;

    /// <summary>
    /// Stand the shape up with its lowest point on zero and centred left to right, so it
    /// arrives on a print bed ready to slice. Turn off to keep the source's own origin.
    /// </summary>
    public bool PlaceOnBed { get; init; } = true;

    /// <summary>Extra scaling, as a percentage. 100 means true size.</summary>
    public float ScalePercent { get; init; } = 100f;
}

/// <summary>A prepared shape, with what was done to it and how closed it came out.</summary>
public sealed record PreparedMesh(
    string PartNumber,
    string? Title,
    IndexedMesh Mesh,
    MeshQuality Quality,
    MeshQuality QualityBeforeRepair,
    int SeamsClosed,
    int DegenerateTrianglesRemoved,
    string? MovedTo,
    IReadOnlyList<string> MissingReferences)
{
    public (Vector3 Min, Vector3 Max) Bounds => Mesh.Bounds();

    public Vector3 Size
    {
        get
        {
            var (min, max) = Bounds;
            return max - min;
        }
    }

    public string DescribeSize()
    {
        var s = Size;
        return $"{s.X:0.###} x {s.Y:0.###} x {s.Z:0.###} mm";
    }
}

/// <summary>
/// Prepares a part's surfaces for output: join, tidy, close seams, then place in millimetres.
/// </summary>
/// <remarks>
/// The order matters. Corners have to be joined before anything can tell whether the surface
/// is closed; seams can only be closed once corners are shared; and the measurement worth
/// reporting is the one taken after the exact repairs and before any scaling, so that it
/// describes the shape rather than the units.
/// </remarks>
public static class MeshPipeline
{
    public static PreparedMesh Prepare(PartMesh part, MeshPipelineOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(part);

        var o = options ?? new MeshPipelineOptions();

        var welded = VertexWelder.Weld(part.Triangles, o.WeldTolerance);
        var tidied = welded.WithoutDegenerateTriangles(out var degenerateRemoved);

        var before = MeshAnalysis.Measure(tidied);

        var seamsClosed = 0;
        var repaired = o.RepairSeams
            ? TJunctionRepair.Repair(tidied, out seamsClosed, o.WeldTolerance)
            : tidied;

        var quality = MeshAnalysis.Measure(repaired);

        var placed = Place(repaired, o);

        return new PreparedMesh(
            part.Reference,
            part.Title,
            placed,
            quality,
            before,
            seamsClosed,
            degenerateRemoved,
            part.MovedTo,
            part.MissingReferences);
    }

    /// <summary>
    /// Converts to millimetres, stands the shape upright, and optionally sets it on the bed.
    /// </summary>
    /// <remarks>
    /// The source's vertical axis points the opposite way to the one every printing tool
    /// expects, so a straight unit conversion arrives lying upside down and has to be turned
    /// by hand every time. Turning it here costs nothing and makes the files usable as they are.
    /// </remarks>
    private static IndexedMesh Place(IndexedMesh mesh, MeshPipelineOptions o)
    {
        var scale = o.MillimetresPerUnit * (o.ScalePercent / 100f);

        // Scale, then rotate so that the source's downward axis becomes upward.
        var transform =
            Matrix4x4.CreateScale(scale) *
            Matrix4x4.CreateRotationX(-MathF.PI / 2f);

        var placed = mesh.Transformed(transform);

        if (!o.PlaceOnBed || placed.VertexCount == 0)
        {
            return placed;
        }

        var (min, max) = placed.Bounds();
        var centre = (min + max) / 2f;

        return placed.Transformed(Matrix4x4.CreateTranslation(-centre.X, -centre.Y, -min.Z));
    }
}

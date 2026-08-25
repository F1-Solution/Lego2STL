using System.Numerics;

namespace Lego2STL.Core.Geometry;

/// <summary>
/// Merges corners that sit at the same place, so neighbouring triangles come to share them.
/// </summary>
/// <remarks>
/// <para>
/// Necessary because the source geometry states every triangle's corners independently. Two
/// triangles meeting along an edge write that edge's endpoints twice, at coordinates that
/// agree to the last digit but are separate numbers, so nothing downstream can tell the
/// surface is joined until they are merged.
/// </para>
/// <para>
/// The merge snaps coordinates to a grid, but looks in the neighbouring grid cells as well as
/// its own. That extra look is the whole point: snapping alone is unstable, because two
/// corners a fraction apart can fall either side of a cell boundary and never meet. Measured
/// on real parts, plain rounding made things worse at some tolerances than at coarser ones -
/// the count of unclosed edges went up as the tolerance grew - which is the signature of
/// exactly that problem.
/// </para>
/// </remarks>
public static class VertexWelder
{
    /// <summary>
    /// Default merge distance, in the units the source geometry uses, where one unit is
    /// 0.4 mm. This is 0.4 micrometres: far below any real feature, so it merges only
    /// corners that were meant to be the same point.
    /// </summary>
    public const float DefaultTolerance = 1e-3f;

    /// <summary>
    /// The same figure as a double, for settings and command lines. Still source units, not
    /// millimetres: the merge happens before anything is converted, so a figure typed on the
    /// command line is compared against the source's own coordinates. Widening the float
    /// instead prints its binary tail, and 0.0010000000474974513 in a help page is noise.
    /// </summary>
    public const double DefaultToleranceUnits = 1e-3;

    public static IndexedMesh Weld(IEnumerable<Triangle> triangles, float tolerance = DefaultTolerance)
    {
        ArgumentNullException.ThrowIfNull(triangles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tolerance);

        var vertices = new List<Vector3>();
        var cells = new Dictionary<(int X, int Y, int Z), List<int>>();
        var indexed = new List<IndexedTriangle>();

        var toleranceSquared = tolerance * tolerance;

        foreach (var triangle in triangles)
        {
            var a = IndexOf(triangle.A);
            var b = IndexOf(triangle.B);
            var c = IndexOf(triangle.C);

            indexed.Add(new IndexedTriangle(a, b, c));
        }

        return new IndexedMesh(vertices, indexed);

        int IndexOf(Vector3 point)
        {
            var cell = CellOf(point, tolerance);

            // Look in this cell and all 26 neighbours, so a point near a boundary still finds
            // its twin on the other side.
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dz = -1; dz <= 1; dz++)
                    {
                        var neighbour = (cell.X + dx, cell.Y + dy, cell.Z + dz);
                        if (!cells.TryGetValue(neighbour, out var candidates))
                        {
                            continue;
                        }

                        foreach (var candidate in candidates)
                        {
                            if (Vector3.DistanceSquared(vertices[candidate], point) <= toleranceSquared)
                            {
                                return candidate;
                            }
                        }
                    }
                }
            }

            var index = vertices.Count;
            vertices.Add(point);

            if (!cells.TryGetValue(cell, out var bucket))
            {
                bucket = [];
                cells[cell] = bucket;
            }

            bucket.Add(index);
            return index;
        }
    }

    private static (int X, int Y, int Z) CellOf(Vector3 point, float tolerance) => (
        (int)MathF.Floor(point.X / tolerance),
        (int)MathF.Floor(point.Y / tolerance),
        (int)MathF.Floor(point.Z / tolerance));
}

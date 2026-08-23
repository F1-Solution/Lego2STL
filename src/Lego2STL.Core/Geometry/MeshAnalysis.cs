namespace Lego2STL.Core.Geometry;

/// <summary>What is known about how closed a mesh is.</summary>
/// <param name="TriangleCount">How many triangles.</param>
/// <param name="VertexCount">How many distinct corners.</param>
/// <param name="EdgeCount">How many distinct edges.</param>
/// <param name="OpenEdgeCount">
/// Edges belonging to only one triangle. Each is a hole in the surface, so zero means the
/// shape is closed.
/// </param>
/// <param name="OverusedEdgeCount">
/// Edges belonging to three or more triangles, which means surfaces meet in a way no solid
/// can. Measured on real parts this is consistently zero: their surfaces are clean apart
/// from holes.
/// </param>
public sealed record MeshQuality(
    int TriangleCount,
    int VertexCount,
    int EdgeCount,
    int OpenEdgeCount,
    int OverusedEdgeCount)
{
    /// <summary>True when every edge is shared by exactly two triangles.</summary>
    public bool IsClosed => OpenEdgeCount == 0 && OverusedEdgeCount == 0;

    public override string ToString() =>
        IsClosed
            ? $"{TriangleCount} triangles, closed"
            : $"{TriangleCount} triangles, {OpenEdgeCount} open edge(s)" +
              (OverusedEdgeCount > 0 ? $", {OverusedEdgeCount} shared by more than two faces" : "");
}

/// <summary>
/// Measures how closed a mesh is.
/// </summary>
/// <remarks>
/// Worth reporting per part rather than assuming, because the source geometry describes
/// surfaces for drawing rather than solids for making. Measured across real parts, some come
/// out already closed while others have hundreds of open edges, and knowing which is which is
/// the difference between trusting a file and finding out at the printer.
/// </remarks>
public static class MeshAnalysis
{
    public static MeshQuality Measure(IndexedMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var uses = CountEdgeUses(mesh);

        var open = 0;
        var overused = 0;

        foreach (var count in uses.Values)
        {
            if (count == 1)
            {
                open++;
            }
            else if (count > 2)
            {
                overused++;
            }
        }

        return new MeshQuality(mesh.TriangleCount, mesh.VertexCount, uses.Count, open, overused);
    }

    /// <summary>How many triangles use each edge.</summary>
    public static Dictionary<(int Low, int High), int> CountEdgeUses(IndexedMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var uses = new Dictionary<(int, int), int>(mesh.TriangleCount * 3);

        foreach (var triangle in mesh.Triangles)
        {
            if (triangle.IsDegenerate)
            {
                continue;
            }

            foreach (var edge in triangle.Edges())
            {
                uses[edge] = uses.GetValueOrDefault(edge) + 1;
            }
        }

        return uses;
    }

    /// <summary>The edges belonging to only one triangle.</summary>
    public static List<(int Low, int High)> OpenEdges(IndexedMesh mesh) =>
        CountEdgeUses(mesh).Where(e => e.Value == 1).Select(e => e.Key).ToList();
}

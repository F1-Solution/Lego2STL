using System.Numerics;

namespace Lego2STL.Core.Geometry;

/// <summary>What filling the gaps in a surface achieved.</summary>
/// <param name="Mesh">The result, or the original when there was nothing to do.</param>
/// <param name="LoopsFilled">How many separate gaps were closed.</param>
/// <param name="TrianglesAdded">How many faces that took.</param>
/// <param name="LoopsLeftOpen">Gaps that could not be closed, usually because they branch.</param>
public sealed record BoundaryFillResult(
    IndexedMesh Mesh,
    int LoopsFilled,
    int TrianglesAdded,
    int LoopsLeftOpen);

/// <summary>
/// Closes the gaps left in a surface by covering each one over.
/// </summary>
/// <remarks>
/// <para>
/// The catalogue's shapes describe surfaces for drawing, not solids for making, so a part can
/// arrive with pieces of its surface simply absent - measured on the reference set, most of
/// them do. Splitting seams closes the ones caused by a corner sitting part-way along a
/// neighbour's edge, which is an exact repair that invents nothing. What is left is genuine
/// holes, and the only way to close those is to cover them.
/// </para>
/// <para>
/// The method is to walk the free edges into loops and lay a fan of faces across each, from a
/// point at its middle. This does invent surface, which is why it is asked for rather than
/// assumed; but a hole in a shape that should be solid is not information worth preserving,
/// and every slicer covers them anyway, silently and without saying which. Doing it here means
/// the report can say exactly how many gaps were covered and in which parts.
/// </para>
/// <para>
/// It also unlocks the clearance offset, which needs a shape with an inside before it can move
/// every face towards it.
/// </para>
/// </remarks>
public static class BoundaryFill
{
    /// <summary>A loop longer than this is not a gap; something is wrong with the surface.</summary>
    private const int MaxLoopLength = 100_000;

    public static BoundaryFillResult Fill(IndexedMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var boundary = DirectedBoundaryEdges(mesh);

        if (boundary.Count == 0)
        {
            return new BoundaryFillResult(mesh, 0, 0, 0);
        }

        var vertices = new List<Vector3>(mesh.Vertices);
        var triangles = new List<IndexedTriangle>(mesh.Triangles);

        var filled = 0;
        var added = 0;
        var leftOpen = 0;

        foreach (var loop in Loops(boundary, ref leftOpen))
        {
            if (loop.Count < 3)
            {
                leftOpen++;
                continue;
            }

            added += Cover(loop, vertices, triangles);
            filled++;
        }

        return added == 0
            ? new BoundaryFillResult(mesh, 0, 0, leftOpen)
            : new BoundaryFillResult(new IndexedMesh(vertices, triangles), filled, added, leftOpen);
    }

    /// <summary>
    /// The free edges, each keeping the direction the one face along it runs.
    /// </summary>
    /// <remarks>
    /// Direction is what makes the gap walkable. In a closed surface every edge is travelled
    /// once each way by the two faces sharing it; an edge travelled only one way is therefore
    /// missing its other face, and following those directions traces the outline of the gap.
    /// </remarks>
    private static Dictionary<int, List<int>> DirectedBoundaryEdges(IndexedMesh mesh)
    {
        var uses = MeshAnalysis.CountEdgeUses(mesh);
        var next = new Dictionary<int, List<int>>();

        foreach (var triangle in mesh.Triangles)
        {
            if (triangle.IsDegenerate)
            {
                continue;
            }

            AddIfBoundary(next, uses, triangle.A, triangle.B);
            AddIfBoundary(next, uses, triangle.B, triangle.C);
            AddIfBoundary(next, uses, triangle.C, triangle.A);
        }

        return next;
    }

    private static void AddIfBoundary(
        Dictionary<int, List<int>> next,
        Dictionary<(int Low, int High), int> uses,
        int from,
        int to)
    {
        if (uses.GetValueOrDefault(IndexedTriangle.Edge(from, to)) != 1)
        {
            return;
        }

        if (!next.TryGetValue(from, out var targets))
        {
            targets = [];
            next[from] = targets;
        }

        targets.Add(to);
    }

    /// <summary>
    /// Walks the free edges into closed loops, consuming each edge once.
    /// </summary>
    /// <remarks>
    /// A path that arrives back at a vertex it has already been through is not one gap but two
    /// meeting at a point. The ring that closes there is detached and covered on its own,
    /// because a single fan across both would use the edge from its centre to that vertex four
    /// times over - leaving a shape with no holes that still does not count as closed.
    /// Where a corner has several free edges leaving it, the lowest-numbered is taken, so that
    /// the same surface always produces the same loops and therefore the same file.
    /// </remarks>
    private static List<List<int>> Loops(Dictionary<int, List<int>> next, ref int leftOpen)
    {
        foreach (var targets in next.Values)
        {
            targets.Sort();
        }

        var loops = new List<List<int>>();
        var starts = next.Keys.Order().ToList();

        foreach (var start in starts)
        {
            while (next.TryGetValue(start, out var fromStart) && fromStart.Count > 0)
            {
                var loop = new List<int> { start };
                var where = new Dictionary<int, int> { [start] = 0 };
                var current = Take(next, start);

                var closed = false;

                while (loop.Count < MaxLoopLength)
                {
                    if (current == start)
                    {
                        closed = true;
                        break;
                    }

                    if (where.TryGetValue(current, out var earlier))
                    {
                        loops.Add(loop[earlier..]);

                        for (var i = earlier; i < loop.Count; i++)
                        {
                            where.Remove(loop[i]);
                        }

                        loop.RemoveRange(earlier, loop.Count - earlier);
                    }

                    where[current] = loop.Count;
                    loop.Add(current);

                    if (!next.TryGetValue(current, out var onward) || onward.Count == 0)
                    {
                        break;
                    }

                    current = Take(next, current);
                }

                if (closed)
                {
                    loops.Add(loop);
                }
                else
                {
                    // A chain that never came back: the surface branches here, and guessing a
                    // cover would invent the wrong thing. Counted and left alone.
                    leftOpen++;
                }
            }
        }

        return loops;
    }

    private static int Take(Dictionary<int, List<int>> next, int from)
    {
        var targets = next[from];
        var to = targets[0];
        targets.RemoveAt(0);
        return to;
    }

    /// <summary>
    /// Lays a fan of faces across one gap, from a new point at its middle.
    /// </summary>
    /// <remarks>
    /// A fan from the middle rather than from one of the corners, because a gap is often a ring
    /// - the mouth of a hole - and fanning from a corner of a ring produces slivers along one
    /// side. The middle gives faces of a sensible shape all the way round.
    /// </remarks>
    private static int Cover(List<int> loop, List<Vector3> vertices, List<IndexedTriangle> triangles)
    {
        var middle = Vector3.Zero;
        foreach (var index in loop)
        {
            middle += vertices[index];
        }

        middle /= loop.Count;

        var centre = vertices.Count;
        vertices.Add(middle);

        for (var i = 0; i < loop.Count; i++)
        {
            var from = loop[i];
            var to = loop[(i + 1) % loop.Count];

            // The loop runs the way the existing face runs, so the new face has to run the
            // other way along that edge for the two to agree about which side is outside.
            triangles.Add(new IndexedTriangle(centre, to, from));
        }

        return loop.Count;
    }
}

using System.Numerics;

namespace Lego2STL.Core.Geometry;

/// <summary>A triangle by vertex index. Order decides which way it faces.</summary>
public readonly record struct IndexedTriangle(int A, int B, int C)
{
    public IndexedTriangle Reversed() => new(C, B, A);

    /// <summary>True when two corners are the same vertex, so the triangle has no area.</summary>
    public bool IsDegenerate => A == B || B == C || A == C;

    public IEnumerable<int> Corners()
    {
        yield return A;
        yield return B;
        yield return C;
    }

    /// <summary>The three edges, each as an unordered pair so that neighbours match.</summary>
    public IEnumerable<(int Low, int High)> Edges()
    {
        yield return Edge(A, B);
        yield return Edge(B, C);
        yield return Edge(C, A);
    }

    public static (int Low, int High) Edge(int i, int j) => i < j ? (i, j) : (j, i);
}

/// <summary>
/// A mesh as a list of distinct vertices plus triangles referring to them.
/// </summary>
/// <remarks>
/// The point of indexing is that neighbouring triangles then share vertices, which is what
/// makes it possible to ask whether a surface is closed. Raw triangles carry their corners
/// independently, so two triangles meeting along an edge look unrelated no matter how exactly
/// their corners coincide.
/// </remarks>
public sealed class IndexedMesh
{
    public IndexedMesh(IReadOnlyList<Vector3> vertices, IReadOnlyList<IndexedTriangle> triangles)
    {
        Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        Triangles = triangles ?? throw new ArgumentNullException(nameof(triangles));
    }

    public IReadOnlyList<Vector3> Vertices { get; }

    public IReadOnlyList<IndexedTriangle> Triangles { get; }

    public int VertexCount => Vertices.Count;

    public int TriangleCount => Triangles.Count;

    public static IndexedMesh Empty { get; } = new([], []);

    public Vector3 Corner(int index) => Vertices[index];

    public Triangle ToTriangle(IndexedTriangle t) =>
        new(Vertices[t.A], Vertices[t.B], Vertices[t.C]);

    public IEnumerable<Triangle> ToTriangles() => Triangles.Select(ToTriangle);

    /// <summary>The smallest box containing every vertex.</summary>
    public (Vector3 Min, Vector3 Max) Bounds()
    {
        if (Vertices.Count == 0)
        {
            return (Vector3.Zero, Vector3.Zero);
        }

        var min = Vertices[0];
        var max = Vertices[0];

        foreach (var v in Vertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        return (min, max);
    }

    /// <summary>The same mesh with every vertex moved by a transform.</summary>
    public IndexedMesh Transformed(Matrix4x4 transform) =>
        new([.. Vertices.Select(v => Vector3.Transform(v, transform))], Triangles);

    /// <summary>Drops triangles whose corners do not enclose any area.</summary>
    public IndexedMesh WithoutDegenerateTriangles(out int removed)
    {
        var kept = new List<IndexedTriangle>(Triangles.Count);

        foreach (var t in Triangles)
        {
            if (t.IsDegenerate || ToTriangle(t).IsDegenerate())
            {
                continue;
            }

            kept.Add(t);
        }

        removed = Triangles.Count - kept.Count;
        return removed == 0 ? this : new IndexedMesh(Vertices, kept);
    }
}

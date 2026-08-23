using System.Numerics;

namespace Lego2STL.Core.Geometry;

/// <summary>A flat triangle. Corner order decides which way it faces.</summary>
public readonly record struct Triangle(Vector3 A, Vector3 B, Vector3 C)
{
    /// <summary>The same triangle facing the other way.</summary>
    public Triangle Reversed() => new(C, B, A);

    /// <summary>
    /// The outward direction, by the right-hand rule. Not normalised, so its length is twice
    /// the triangle's area, which is what makes it useful for weighting.
    /// </summary>
    public Vector3 RawNormal() => Vector3.Cross(B - A, C - A);

    /// <summary>The outward direction as a unit vector, or zero for a degenerate triangle.</summary>
    public Vector3 Normal()
    {
        var raw = RawNormal();
        var length = raw.Length();
        return length > 0 ? raw / length : Vector3.Zero;
    }

    public float Area() => RawNormal().Length() / 2f;

    /// <summary>
    /// True when the three corners do not enclose any area, either because two coincide or
    /// because all three are in line. Such triangles carry no surface and are dropped.
    /// </summary>
    public bool IsDegenerate(float tolerance = 1e-9f) => RawNormal().LengthSquared() <= tolerance;

    public Triangle Transformed(Matrix4x4 transform) => new(
        Vector3.Transform(A, transform),
        Vector3.Transform(B, transform),
        Vector3.Transform(C, transform));

    public IEnumerable<Vector3> Corners()
    {
        yield return A;
        yield return B;
        yield return C;
    }
}

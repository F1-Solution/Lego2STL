using System.Numerics;

namespace Lego2STL.Core.LDraw;

/// <summary>
/// Builds transforms from the numbers written in an LDraw reference line.
/// </summary>
/// <remarks>
/// <para>
/// This exists to hold one conversion that is silently wrong if done the obvious way.
/// </para>
/// <para>
/// An LDraw reference line lists twelve numbers, and applies them as
/// <c>x' = a*x + b*y + c*z + tx</c>. That treats a point as a column and multiplies the
/// matrix on its left. <see cref="Matrix4x4"/> does the opposite: it treats a point as a row
/// and multiplies on the right, with the translation in the fourth row. The two conventions
/// are transposes of each other, so the nine rotation numbers must be transposed on the way in.
/// </para>
/// <para>
/// What makes this dangerous rather than merely fiddly is that nothing catches it. A
/// transposed matrix is still a valid matrix, so no error is raised; and transposing does not
/// change a determinant, so the sign check that decides whether a shape has been mirrored
/// gives exactly the same answer either way. The only symptom is geometry quietly in the
/// wrong place. Measured with a deliberately lopsided matrix, the correct load gives
/// (13, 21, 26) and the obvious one gives (12, 14, 31), and both report the same determinant
/// of -2. Hence <see cref="LDrawMatrixTests"/>.
/// </para>
/// </remarks>
public static class LDrawMatrix
{
    /// <summary>
    /// Builds a transform from a reference line's twelve numbers, transposing the rotation
    /// so that it means the same thing under <see cref="Matrix4x4"/>'s convention.
    /// </summary>
    public static Matrix4x4 FromReferenceLine(
        float x, float y, float z,
        float a, float b, float c,
        float d, float e, float f,
        float g, float h, float i) =>
        new(
            a, d, g, 0,     // note the transpose: a d g down the first row, not a b c
            b, e, h, 0,
            c, f, i, 0,
            x, y, z, 1);

    /// <summary>
    /// Determinant of the rotation part. A negative value means the transform mirrors the
    /// shape, which reverses which way its faces point.
    /// </summary>
    public static float Determinant(
        float a, float b, float c,
        float d, float e, float f,
        float g, float h, float i) =>
        (a * ((e * i) - (f * h)))
        - (b * ((d * i) - (f * g)))
        + (c * ((d * h) - (e * g)));

    /// <summary>
    /// Combines a child's transform with its parent's, so the child's own coordinates end up
    /// in the parent's space.
    /// </summary>
    public static Matrix4x4 Combine(Matrix4x4 child, Matrix4x4 parent) => child * parent;

    /// <summary>Applies a transform to a point.</summary>
    public static Vector3 Apply(Matrix4x4 transform, Vector3 point) =>
        Vector3.Transform(point, transform);
}

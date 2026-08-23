using System.Numerics;

namespace Lego2STL.Core.Geometry;

/// <summary>The eigenvalues and eigenvectors of a symmetric 3x3 matrix.</summary>
internal readonly record struct Eigen3(
    float L0, Vector3 V0,
    float L1, Vector3 V1,
    float L2, Vector3 V2);

/// <summary>
/// Decomposes a symmetric 3x3 matrix, so a system that has no single answer can still be
/// solved for the smallest one.
/// </summary>
/// <remarks>
/// <para>
/// Needed by the clearance offset. Asking where a corner should move so that every face
/// through it comes in by a fixed distance is a set of linear equations, one per face, and
/// the number of them varies: three faces at a box corner fix the answer exactly, two along
/// an edge leave it free to slide along that edge, and one on a flat face leaves it free in
/// two directions. Twenty faces around a cylinder over-determine it.
/// </para>
/// <para>
/// Decomposing is what handles all of those the same way. Directions the faces genuinely
/// constrain get solved; directions they say nothing about are left alone, which is exactly
/// the "do not slide the corner sideways for no reason" behaviour wanted. A plain inverse
/// cannot do this, because in every case but the first the matrix has no inverse.
/// </para>
/// <para>
/// The method is Jacobi rotation: repeatedly cancel the largest off-diagonal entry with a
/// rotation until what is left is diagonal. For 3x3 it converges in a handful of sweeps and
/// is short enough to read.
/// </para>
/// </remarks>
internal static class SymmetricEigen3
{
    private const int MaxSweeps = 24;
    private const float Tiny = 1e-20f;

    /// <summary>
    /// Decomposes a symmetric matrix given by its upper triangle.
    /// </summary>
    public static Eigen3 Decompose(
        float a00, float a01, float a02,
        float a11, float a12,
        float a22)
    {
        // Working copy of the matrix, and the accumulated rotations.
        Span<float> a = [a00, a01, a02, a01, a11, a12, a02, a12, a22];
        Span<float> v = [1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f];

        for (var sweep = 0; sweep < MaxSweeps; sweep++)
        {
            var (p, q, largest) = LargestOffDiagonal(a);

            if (largest <= Tiny)
            {
                break;
            }

            Rotate(a, v, p, q);
        }

        return new Eigen3(
            a[0], new Vector3(v[0], v[3], v[6]),
            a[4], new Vector3(v[1], v[4], v[7]),
            a[8], new Vector3(v[2], v[5], v[8]));
    }

    private static (int P, int Q, float Size) LargestOffDiagonal(ReadOnlySpan<float> a)
    {
        var p = 0;
        var q = 1;
        var largest = MathF.Abs(a[1]);

        if (MathF.Abs(a[2]) > largest)
        {
            (p, q, largest) = (0, 2, MathF.Abs(a[2]));
        }

        if (MathF.Abs(a[5]) > largest)
        {
            (p, q, largest) = (1, 2, MathF.Abs(a[5]));
        }

        return (p, q, largest);
    }

    /// <summary>One Jacobi rotation, chosen to zero the entry at (p, q).</summary>
    private static void Rotate(Span<float> a, Span<float> v, int p, int q)
    {
        var apq = a[(p * 3) + q];
        var app = a[(p * 3) + p];
        var aqq = a[(q * 3) + q];

        var theta = (aqq - app) / (2f * apq);
        var sign = theta >= 0f ? 1f : -1f;
        var t = sign / ((sign * theta) + MathF.Sqrt((theta * theta) + 1f));

        var c = 1f / MathF.Sqrt((t * t) + 1f);
        var s = t * c;

        for (var k = 0; k < 3; k++)
        {
            var akp = a[(k * 3) + p];
            var akq = a[(k * 3) + q];
            a[(k * 3) + p] = (c * akp) - (s * akq);
            a[(k * 3) + q] = (s * akp) + (c * akq);
        }

        for (var k = 0; k < 3; k++)
        {
            var apk = a[(p * 3) + k];
            var aqk = a[(q * 3) + k];
            a[(p * 3) + k] = (c * apk) - (s * aqk);
            a[(q * 3) + k] = (s * apk) + (c * aqk);
        }

        for (var k = 0; k < 3; k++)
        {
            var vkp = v[(k * 3) + p];
            var vkq = v[(k * 3) + q];
            v[(k * 3) + p] = (c * vkp) - (s * vkq);
            v[(k * 3) + q] = (s * vkp) + (c * vkq);
        }
    }

    /// <summary>
    /// Solves the system for the smallest answer, ignoring directions the equations do not
    /// pin down. Those show up as eigenvalues near zero, and leaving them out is what stops
    /// a corner from being flung sideways by a direction nothing actually constrains.
    /// </summary>
    /// <param name="eigen">The decomposition of the system's matrix.</param>
    /// <param name="rightHandSide">The vector the system equals.</param>
    /// <param name="relativeTolerance">
    /// How small an eigenvalue counts as zero, as a fraction of the largest.
    /// </param>
    public static Vector3 SolveSmallest(
        Eigen3 eigen,
        Vector3 rightHandSide,
        float relativeTolerance = 1e-5f)
    {
        var largest = MathF.Max(
            MathF.Abs(eigen.L0), MathF.Max(MathF.Abs(eigen.L1), MathF.Abs(eigen.L2)));

        if (largest <= Tiny)
        {
            return Vector3.Zero;
        }

        var floor = largest * relativeTolerance;
        var answer = Vector3.Zero;

        Contribute(ref answer, eigen.L0, eigen.V0, rightHandSide, floor);
        Contribute(ref answer, eigen.L1, eigen.V1, rightHandSide, floor);
        Contribute(ref answer, eigen.L2, eigen.V2, rightHandSide, floor);

        return answer;
    }

    private static void Contribute(
        ref Vector3 answer, float eigenvalue, Vector3 direction, Vector3 rightHandSide, float floor)
    {
        if (MathF.Abs(eigenvalue) <= floor)
        {
            return;
        }

        answer += direction * (Vector3.Dot(direction, rightHandSide) / eigenvalue);
    }
}

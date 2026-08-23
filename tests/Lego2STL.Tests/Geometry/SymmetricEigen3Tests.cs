using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Geometry;

namespace Lego2STL.Tests.Geometry;

/// <summary>
/// The solver behind the clearance offset. Tested on its own because the cases that matter -
/// a system that pins the answer down, one that leaves it free to slide, one with no
/// constraint at all - are hard to tell apart from the outside, and getting the free ones
/// wrong moves corners sideways for no reason.
/// </summary>
public sealed class SymmetricEigen3Tests
{
    /// <summary>Rebuilds the matrix from its decomposition, which is the definition of correct.</summary>
    private static Matrix4x4 Rebuild(Eigen3 e) =>
        Outer(e.V0, e.L0) + Outer(e.V1, e.L1) + Outer(e.V2, e.L2);

    private static Matrix4x4 Outer(Vector3 v, float scale) => new(
        v.X * v.X * scale, v.X * v.Y * scale, v.X * v.Z * scale, 0,
        v.Y * v.X * scale, v.Y * v.Y * scale, v.Y * v.Z * scale, 0,
        v.Z * v.X * scale, v.Z * v.Y * scale, v.Z * v.Z * scale, 0,
        0, 0, 0, 0);

    [Fact]
    public void The_decomposition_rebuilds_the_matrix_it_came_from()
    {
        var e = SymmetricEigen3.Decompose(4f, 1f, -2f, 5f, 0.5f, 3f);
        var rebuilt = Rebuild(e);

        rebuilt.M11.Should().BeApproximately(4f, 1e-4f);
        rebuilt.M12.Should().BeApproximately(1f, 1e-4f);
        rebuilt.M13.Should().BeApproximately(-2f, 1e-4f);
        rebuilt.M22.Should().BeApproximately(5f, 1e-4f);
        rebuilt.M23.Should().BeApproximately(0.5f, 1e-4f);
        rebuilt.M33.Should().BeApproximately(3f, 1e-4f);
    }

    [Fact]
    public void The_eigenvectors_are_unit_length_and_at_right_angles()
    {
        var e = SymmetricEigen3.Decompose(4f, 1f, -2f, 5f, 0.5f, 3f);

        e.V0.Length().Should().BeApproximately(1f, 1e-4f);
        e.V1.Length().Should().BeApproximately(1f, 1e-4f);
        e.V2.Length().Should().BeApproximately(1f, 1e-4f);

        Vector3.Dot(e.V0, e.V1).Should().BeApproximately(0f, 1e-4f);
        Vector3.Dot(e.V0, e.V2).Should().BeApproximately(0f, 1e-4f);
        Vector3.Dot(e.V1, e.V2).Should().BeApproximately(0f, 1e-4f);
    }

    /// <summary>Three directions at right angles pin the answer down completely.</summary>
    [Fact]
    public void Three_independent_faces_give_exactly_one_answer()
    {
        // Faces pointing along -x, -y and -z, each asking to move in by 2.
        var answer = Solve([-Vector3.UnitX, -Vector3.UnitY, -Vector3.UnitZ], 2f);

        answer.X.Should().BeApproximately(2f, 1e-4f);
        answer.Y.Should().BeApproximately(2f, 1e-4f);
        answer.Z.Should().BeApproximately(2f, 1e-4f);
    }

    /// <summary>
    /// Two faces leave the corner free to slide along their shared edge. The smallest answer
    /// does not slide, which is what keeps an edge from shearing.
    /// </summary>
    [Fact]
    public void Two_faces_move_the_corner_only_where_they_require_it()
    {
        var answer = Solve([-Vector3.UnitX, -Vector3.UnitY], 1f);

        answer.X.Should().BeApproximately(1f, 1e-4f);
        answer.Y.Should().BeApproximately(1f, 1e-4f);
        answer.Z.Should().BeApproximately(0f, 1e-4f);
    }

    /// <summary>One face constrains one direction, and leaves the other two untouched.</summary>
    [Fact]
    public void One_face_moves_the_corner_straight_in()
    {
        var answer = Solve([-Vector3.UnitZ], 0.5f);

        answer.X.Should().BeApproximately(0f, 1e-4f);
        answer.Y.Should().BeApproximately(0f, 1e-4f);
        answer.Z.Should().BeApproximately(0.5f, 1e-4f);
    }

    /// <summary>
    /// A square face arriving as several coplanar triangles must not count more than once.
    /// Here the same direction repeated still moves the corner in by the requested distance.
    /// </summary>
    [Fact]
    public void Repeating_one_direction_does_not_move_the_corner_further()
    {
        var once = Solve([-Vector3.UnitZ], 0.5f);
        var thrice = Solve([-Vector3.UnitZ, -Vector3.UnitZ, -Vector3.UnitZ], 0.5f);

        thrice.Z.Should().BeApproximately(once.Z, 1e-4f);
    }

    [Fact]
    public void A_system_with_no_constraints_leaves_the_corner_alone()
    {
        SymmetricEigen3.SolveSmallest(
            SymmetricEigen3.Decompose(0f, 0f, 0f, 0f, 0f, 0f),
            new Vector3(1f, 2f, 3f))
            .Should().Be(Vector3.Zero);
    }

    /// <summary>Solves the same system the clearance offset builds, for the given faces.</summary>
    private static Vector3 Solve(IReadOnlyList<Vector3> normals, float distance)
    {
        float a00 = 0f, a01 = 0f, a02 = 0f, a11 = 0f, a12 = 0f, a22 = 0f;
        var rhs = Vector3.Zero;

        foreach (var n in normals)
        {
            a00 += n.X * n.X;
            a01 += n.X * n.Y;
            a02 += n.X * n.Z;
            a11 += n.Y * n.Y;
            a12 += n.Y * n.Z;
            a22 += n.Z * n.Z;
            rhs -= n * distance;
        }

        return SymmetricEigen3.SolveSmallest(
            SymmetricEigen3.Decompose(a00, a01, a02, a11, a12, a22), rhs);
    }
}

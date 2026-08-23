using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.LDraw;

namespace Lego2STL.Tests.LDraw;

/// <summary>
/// Guards the one conversion in this codebase that fails silently.
/// </summary>
/// <remarks>
/// An LDraw reference line means <c>x' = a*x + b*y + c*z + tx</c>, which is the opposite
/// convention to <see cref="Matrix4x4"/>, so the rotation has to be transposed on the way in.
/// Getting it wrong raises no error and does not change any determinant, so nothing else in
/// the pipeline can notice; the geometry is just quietly wrong. These tests use a deliberately
/// lopsided, non-orthogonal matrix, because the mistake is invisible with a symmetric one.
/// </remarks>
public sealed class LDrawMatrixTests
{
    // A rotation part chosen so that transposing it changes the answer:
    // a b c = 0 0 1 / d e f = 1 0 0 / g h i = 0 -2 0
    private const float A = 0, B = 0, C = 1;
    private const float D = 1, E = 0, F = 0;
    private const float G = 0, H = -2, I = 0;
    private const float Tx = 10, Ty = 20, Tz = 30;

    private static readonly Vector3 Point = new(1, 2, 3);

    /// <summary>What the LDraw specification says the numbers mean, written out directly.</summary>
    private static Vector3 ByTheSpecification(Vector3 p) => new(
        (A * p.X) + (B * p.Y) + (C * p.Z) + Tx,
        (D * p.X) + (E * p.Y) + (F * p.Z) + Ty,
        (G * p.X) + (H * p.Y) + (I * p.Z) + Tz);

    [Fact]
    public void A_transform_moves_a_point_where_the_specification_says_it_should()
    {
        var transform = LDrawMatrix.FromReferenceLine(Tx, Ty, Tz, A, B, C, D, E, F, G, H, I);

        LDrawMatrix.Apply(transform, Point).Should().Be(ByTheSpecification(Point));
    }

    [Fact]
    public void The_expected_answer_is_the_one_that_was_measured() =>
        ByTheSpecification(Point).Should().Be(new Vector3(13, 21, 26));

    /// <summary>
    /// The failure this guards against: loading the nine numbers in written order without
    /// transposing. Kept as a test so the wrong answer is on record.
    /// </summary>
    [Fact]
    public void Loading_the_rotation_without_transposing_gives_a_different_point()
    {
        var naive = new Matrix4x4(
            A, B, C, 0,
            D, E, F, 0,
            G, H, I, 0,
            Tx, Ty, Tz, 1);

        var wrong = Vector3.Transform(Point, naive);

        wrong.Should().Be(new Vector3(12, 14, 31));
        wrong.Should().NotBe(ByTheSpecification(Point));
    }

    /// <summary>
    /// Why the determinant check cannot be relied on to catch the mistake: transposing leaves
    /// it unchanged, so both loads report the same sign.
    /// </summary>
    [Fact]
    public void The_determinant_is_the_same_either_way_so_it_cannot_detect_the_mistake()
    {
        var correct = LDrawMatrix.FromReferenceLine(Tx, Ty, Tz, A, B, C, D, E, F, G, H, I);
        var naive = new Matrix4x4(A, B, C, 0, D, E, F, 0, G, H, I, 0, Tx, Ty, Tz, 1);

        var byHand = LDrawMatrix.Determinant(A, B, C, D, E, F, G, H, I);

        byHand.Should().Be(-2);
        correct.GetDeterminant().Should().BeApproximately(byHand, 1e-5f);
        naive.GetDeterminant().Should().BeApproximately(byHand, 1e-5f);
    }

    [Fact]
    public void An_identity_reference_leaves_a_point_alone()
    {
        var identity = LDrawMatrix.FromReferenceLine(0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1);

        LDrawMatrix.Apply(identity, Point).Should().Be(Point);
    }

    [Fact]
    public void A_pure_translation_adds_the_offset()
    {
        var translate = LDrawMatrix.FromReferenceLine(5, -6, 7, 1, 0, 0, 0, 1, 0, 0, 0, 1);

        LDrawMatrix.Apply(translate, Point).Should().Be(new Vector3(6, -4, 10));
    }

    /// <summary>
    /// Nesting has to apply the inner transform first. Composed the other way round, a part
    /// whose sub-parts are offset ends up scattered.
    /// </summary>
    [Fact]
    public void Nested_transforms_apply_the_inner_one_first()
    {
        var inner = LDrawMatrix.FromReferenceLine(1, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1);

        // Outer turns 90 degrees about Y, then shifts up by 100.
        var outer = LDrawMatrix.FromReferenceLine(0, 100, 0, 0, 0, 1, 0, 1, 0, -1, 0, 0);

        var combined = LDrawMatrix.Combine(inner, outer);

        var throughCombined = LDrawMatrix.Apply(combined, Vector3.Zero);
        var stepByStep = LDrawMatrix.Apply(outer, LDrawMatrix.Apply(inner, Vector3.Zero));

        throughCombined.Should().Be(stepByStep);
    }

    [Fact]
    public void A_mirroring_transform_has_a_negative_determinant() =>
        LDrawMatrix.Determinant(-1, 0, 0, 0, 1, 0, 0, 0, 1).Should().BeNegative();

    [Fact]
    public void A_plain_rotation_has_a_positive_determinant() =>
        LDrawMatrix.Determinant(0, 0, 1, 0, 1, 0, -1, 0, 0).Should().BePositive();
}

using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Geometry;

namespace Lego2STL.Tests.Geometry;

public sealed class BoundaryFillTests
{
    /// <summary>A closed box, built from two triangles per face.</summary>
    private static IndexedMesh Box()
    {
        Vector3[] corners =
        [
            new(-1, -1, -1), new(1, -1, -1), new(1, 1, -1), new(-1, 1, -1),
            new(-1, -1, 1), new(1, -1, 1), new(1, 1, 1), new(-1, 1, 1),
        ];

        IndexedTriangle[] faces =
        [
            new(0, 2, 1), new(0, 3, 2),
            new(4, 5, 6), new(4, 6, 7),
            new(0, 1, 5), new(0, 5, 4),
            new(1, 2, 6), new(1, 6, 5),
            new(2, 3, 7), new(2, 7, 6),
            new(3, 0, 4), new(3, 4, 7),
        ];

        return new IndexedMesh(corners, faces);
    }

    /// <summary>The same box with one square face missing, leaving a four-sided gap.</summary>
    private static IndexedMesh BoxWithOneFaceMissing()
    {
        var box = Box();
        return new IndexedMesh(box.Vertices, [.. box.Triangles.Skip(2)]);
    }

    [Fact]
    public void A_shape_that_is_already_closed_is_left_exactly_as_it_was()
    {
        var box = Box();

        var result = BoundaryFill.Fill(box);

        result.Mesh.Should().BeSameAs(box);
        result.LoopsFilled.Should().Be(0);
        result.TrianglesAdded.Should().Be(0);
    }

    [Fact]
    public void A_missing_face_is_covered_and_the_shape_becomes_closed()
    {
        var open = BoxWithOneFaceMissing();
        MeshAnalysis.Measure(open).IsClosed.Should().BeFalse();

        var result = BoundaryFill.Fill(open);

        result.LoopsFilled.Should().Be(1);
        MeshAnalysis.Measure(result.Mesh).IsClosed.Should().BeTrue();
    }

    [Fact]
    public void Covering_a_gap_does_not_move_the_shape()
    {
        var open = BoxWithOneFaceMissing();
        var (minBefore, maxBefore) = open.Bounds();

        var (minAfter, maxAfter) = BoundaryFill.Fill(open).Mesh.Bounds();

        minAfter.Should().Be(minBefore);
        maxAfter.Should().Be(maxBefore);
    }

    /// <summary>
    /// The new faces have to agree with the old about which side is outside, or the shape is
    /// closed but inside out along the patch, which is worse than leaving the hole.
    /// </summary>
    [Fact]
    public void The_covering_faces_point_the_same_way_as_the_rest()
    {
        var filled = BoundaryFill.Fill(BoxWithOneFaceMissing()).Mesh;

        // For a closed shape whose faces all point outwards, the signed volume is positive.
        SignedVolume(filled).Should().BePositive();
    }

    [Fact]
    public void Two_separate_gaps_are_both_covered()
    {
        var box = Box();

        // Drop the bottom face and the top face: two gaps that do not touch.
        var open = new IndexedMesh(box.Vertices, [.. box.Triangles.Skip(4)]);

        var result = BoundaryFill.Fill(open);

        result.LoopsFilled.Should().Be(2);
        MeshAnalysis.Measure(result.Mesh).IsClosed.Should().BeTrue();
    }

    [Fact]
    public void Covering_the_same_shape_twice_gives_the_same_result()
    {
        var first = BoundaryFill.Fill(BoxWithOneFaceMissing());
        var second = BoundaryFill.Fill(BoxWithOneFaceMissing());

        first.Mesh.Vertices.Should().Equal(second.Mesh.Vertices);
        first.Mesh.Triangles.Should().Equal(second.Mesh.Triangles);
    }

    /// <summary>
    /// Once a shape is closed the clearance offset will take it, which is the point of
    /// covering the gaps in the first place.
    /// </summary>
    [Fact]
    public void A_covered_shape_can_then_be_taken_in()
    {
        var filled = BoundaryFill.Fill(BoxWithOneFaceMissing()).Mesh;

        var result = ClearanceOffset.Apply(filled, 0.1f, MeshAnalysis.Measure(filled));

        result.Applied.Should().BeTrue();
    }

    /// <summary>Six times the volume, which is all the sign test needs.</summary>
    private static float SignedVolume(IndexedMesh mesh)
    {
        var total = 0f;

        foreach (var t in mesh.Triangles)
        {
            var a = mesh.Vertices[t.A];
            var b = mesh.Vertices[t.B];
            var c = mesh.Vertices[t.C];
            total += Vector3.Dot(a, Vector3.Cross(b, c));
        }

        return total;
    }
}

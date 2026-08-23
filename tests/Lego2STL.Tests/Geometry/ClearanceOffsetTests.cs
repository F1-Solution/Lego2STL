using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Geometry;

namespace Lego2STL.Tests.Geometry;

public sealed class ClearanceOffsetTests
{
    /// <summary>A closed box, which is the shape a clearance is meant for.</summary>
    private static IndexedMesh Box(float size)
    {
        var h = size / 2f;

        Vector3[] corners =
        [
            new(-h, -h, -h), new(h, -h, -h), new(h, h, -h), new(-h, h, -h),
            new(-h, -h, h), new(h, -h, h), new(h, h, h), new(-h, h, h),
        ];

        IndexedTriangle[] faces =
        [
            new(0, 2, 1), new(0, 3, 2),   // bottom
            new(4, 5, 6), new(4, 6, 7),   // top
            new(0, 1, 5), new(0, 5, 4),   // front
            new(1, 2, 6), new(1, 6, 5),   // right
            new(2, 3, 7), new(2, 7, 6),   // back
            new(3, 0, 4), new(3, 4, 7),   // left
        ];

        return new IndexedMesh(corners, faces);
    }

    /// <summary>A closed box with three different sides, so a cube's symmetry cannot hide an error.</summary>
    private static IndexedMesh Slab(float x, float y, float z)
    {
        var box = Box(2f);
        var scale = Matrix4x4.CreateScale(x / 2f, y / 2f, z / 2f);
        return box.Transformed(scale);
    }

    /// <summary>
    /// A closed cylinder standing on its end: a side of flat facets, and two ends fanned from
    /// a centre point.
    /// </summary>
    private static IndexedMesh Cylinder(float radius, float height, int sides)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<IndexedTriangle>();

        var h = height / 2f;

        for (var i = 0; i < sides; i++)
        {
            var angle = 2f * MathF.PI * i / sides;
            var x = radius * MathF.Cos(angle);
            var y = radius * MathF.Sin(angle);
            vertices.Add(new Vector3(x, y, -h));
            vertices.Add(new Vector3(x, y, h));
        }

        var bottomCentre = vertices.Count;
        vertices.Add(new Vector3(0, 0, -h));
        var topCentre = vertices.Count;
        vertices.Add(new Vector3(0, 0, h));

        for (var i = 0; i < sides; i++)
        {
            var next = (i + 1) % sides;

            var lowA = i * 2;
            var highA = (i * 2) + 1;
            var lowB = next * 2;
            var highB = (next * 2) + 1;

            triangles.Add(new IndexedTriangle(lowA, lowB, highB));
            triangles.Add(new IndexedTriangle(lowA, highB, highA));

            triangles.Add(new IndexedTriangle(bottomCentre, lowB, lowA));
            triangles.Add(new IndexedTriangle(topCentre, highA, highB));
        }

        return new IndexedMesh(vertices, triangles);
    }

    private static Vector3 SizeOf(IndexedMesh mesh)
    {
        var (min, max) = mesh.Bounds();
        return max - min;
    }

    [Fact]
    public void Every_face_comes_in_by_the_amount_asked_for()
    {
        var box = Box(20f);
        var result = ClearanceOffset.Apply(box, 0.15f, MeshAnalysis.Measure(box));

        result.Applied.Should().BeTrue();

        // Both faces of each pair move inward, so each span loses twice the clearance.
        var size = SizeOf(result.Mesh);
        size.X.Should().BeApproximately(20f - 0.3f, 0.001f);
        size.Y.Should().BeApproximately(20f - 0.3f, 0.001f);
        size.Z.Should().BeApproximately(20f - 0.3f, 0.001f);
    }

    [Fact]
    public void The_shape_stays_where_it_was_rather_than_drifting()
    {
        var box = Box(20f);
        var result = ClearanceOffset.Apply(box, 0.2f, MeshAnalysis.Measure(box));

        var (min, max) = result.Mesh.Bounds();
        var centre = (min + max) / 2f;

        centre.Length().Should().BeLessThan(0.001f);
    }

    [Fact]
    public void Asking_for_nothing_changes_nothing()
    {
        var box = Box(20f);
        var result = ClearanceOffset.Apply(box, 0f, MeshAnalysis.Measure(box));

        result.Applied.Should().BeFalse();
        result.Reason.Should().BeNull();
        result.Mesh.Should().BeSameAs(box);
    }

    [Fact]
    public void The_number_of_corners_and_faces_is_unchanged()
    {
        var box = Box(20f);
        var result = ClearanceOffset.Apply(box, 0.1f, MeshAnalysis.Measure(box));

        result.Mesh.VertexCount.Should().Be(box.VertexCount);
        result.Mesh.TriangleCount.Should().Be(box.TriangleCount);
    }

    /// <summary>
    /// A surface with holes has no inside along its boundary, so pulling the faces in would
    /// distort it rather than shrink it. Refusing and saying so beats a shape that is wrong
    /// in a way nobody sees until it is printed.
    /// </summary>
    [Fact]
    public void A_shape_with_holes_in_its_surface_is_left_alone_and_the_reason_given()
    {
        var box = Box(20f);
        var withHole = new IndexedMesh(box.Vertices, [.. box.Triangles.Skip(1)]);

        var result = ClearanceOffset.Apply(withHole, 0.15f, MeshAnalysis.Measure(withHole));

        result.Applied.Should().BeFalse();
        result.Reason.Should().Be("open");
        result.Mesh.Should().BeSameAs(withHole);
    }

    [Fact]
    public void A_part_thinner_than_the_clearance_would_take_is_left_alone()
    {
        var thin = Box(0.4f);

        var result = ClearanceOffset.Apply(thin, 0.3f, MeshAnalysis.Measure(thin));

        result.Applied.Should().BeFalse();
        result.Reason.Should().Be("thin");
    }

    [Fact]
    public void A_negative_clearance_is_refused()
    {
        var box = Box(20f);

        var act = () => ClearanceOffset.Apply(box, -0.1f, MeshAnalysis.Measure(box));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void The_shape_stays_closed()
    {
        var box = Box(20f);
        var result = ClearanceOffset.Apply(box, 0.15f, MeshAnalysis.Measure(box));

        MeshAnalysis.Measure(result.Mesh).IsClosed.Should().BeTrue();
    }

    /// <summary>
    /// The case that catches weighting by triangle count. A cylinder's side is many facets and
    /// its ends are two discs cut into fans, so the corners around the rim are touched by very
    /// different numbers of triangles depending on where they sit. The radius still has to come
    /// in by exactly the clearance.
    /// </summary>
    [Fact]
    public void A_round_part_loses_exactly_the_clearance_from_its_radius()
    {
        const float radius = 8f;
        const float clearance = 0.2f;

        var cylinder = Cylinder(radius, height: 10f, sides: 32);

        var result = ClearanceOffset.Apply(cylinder, clearance, MeshAnalysis.Measure(cylinder));

        result.Applied.Should().BeTrue();

        var size = SizeOf(result.Mesh);
        size.X.Should().BeApproximately(2f * (radius - clearance), 0.02f);
        size.Y.Should().BeApproximately(2f * (radius - clearance), 0.02f);
        size.Z.Should().BeApproximately(10f - (2f * clearance), 0.01f);
    }

    /// <summary>
    /// A box whose sides are all different, so an error that happens to cancel on a cube
    /// cannot hide. Each of the three spans has to lose exactly twice the clearance.
    /// </summary>
    [Theory]
    [InlineData(0.05f)]
    [InlineData(0.15f)]
    [InlineData(0.4f)]
    public void Each_span_loses_twice_the_clearance_whatever_the_amount(float clearance)
    {
        var slab = Slab(30f, 12f, 6f);

        var result = ClearanceOffset.Apply(slab, clearance, MeshAnalysis.Measure(slab));

        var size = SizeOf(result.Mesh);
        size.X.Should().BeApproximately(30f - (2f * clearance), 0.001f);
        size.Y.Should().BeApproximately(12f - (2f * clearance), 0.001f);
        size.Z.Should().BeApproximately(6f - (2f * clearance), 0.001f);
    }

    [Fact]
    public void The_thinnest_span_is_the_smallest_side_of_the_box()
    {
        var flat = new IndexedMesh(
            [new Vector3(0, 0, 0), new Vector3(30, 0, 0), new Vector3(0, 12, 0), new Vector3(0, 0, 4)],
            [new IndexedTriangle(0, 1, 2)]);

        ClearanceOffset.ThinnestSpan(flat).Should().Be(4f);
    }
}

using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Geometry;

namespace Lego2STL.Tests.Geometry;

/// <summary>
/// The repair is tested on a seam small enough to reason about completely: a square split in
/// half on one side and left whole on the other, which is the shape of the problem in real parts.
/// </summary>
public sealed class TJunctionRepairTests
{
    /// <summary>
    /// Two triangles on the left meeting one long edge on the right. The corner where the
    /// left pair meet lies half-way along the right triangle's edge, so the surfaces touch
    /// completely yet the long edge belongs to one triangle only.
    /// </summary>
    private static IReadOnlyList<Triangle> SeamWithAHangingCorner()
    {
        var bottomLeft = new Vector3(0, 0, 0);
        var middleLeft = new Vector3(0, 10, 0);
        var topLeft = new Vector3(0, 20, 0);
        var bottomRight = new Vector3(10, 0, 0);
        var topRight = new Vector3(10, 20, 0);

        return
        [
            // Left side, divided in two at middleLeft.
            new Triangle(bottomLeft, bottomRight, middleLeft),
            new Triangle(middleLeft, bottomRight, topRight),

            // Right side, one triangle whose edge runs the full height past middleLeft.
            new Triangle(bottomLeft, middleLeft, topLeft),
            new Triangle(middleLeft, topRight, topLeft),
        ];
    }

    [Fact]
    public void A_corner_lying_on_an_edge_is_found_and_the_edge_is_split()
    {
        // One triangle whose long edge passes through a corner belonging to two others.
        var mesh = VertexWelder.Weld(
        [
            new Triangle(new Vector3(0, 0, 0), new Vector3(0, 20, 0), new Vector3(-10, 10, 0)),
            new Triangle(new Vector3(0, 0, 0), new Vector3(10, 10, 0), new Vector3(0, 10, 0)),
            new Triangle(new Vector3(0, 10, 0), new Vector3(10, 10, 0), new Vector3(0, 20, 0)),
        ]);

        var before = MeshAnalysis.Measure(mesh);
        var repaired = TJunctionRepair.Repair(mesh, out var splits);
        var after = MeshAnalysis.Measure(repaired);

        splits.Should().Be(1);
        after.TriangleCount.Should().Be(before.TriangleCount + 1, "a split makes one triangle into two");
        after.OpenEdgeCount.Should().BeLessThan(before.OpenEdgeCount);
    }

    [Fact]
    public void Splitting_invents_no_new_positions()
    {
        var mesh = VertexWelder.Weld(SeamWithAHangingCorner());

        var repaired = TJunctionRepair.Repair(mesh, out _);

        repaired.Vertices.Should().BeEquivalentTo(mesh.Vertices,
            "the repair only re-divides existing triangles");
    }

    [Fact]
    public void Splitting_keeps_the_surface_facing_the_same_way()
    {
        var mesh = VertexWelder.Weld(SeamWithAHangingCorner());

        var before = mesh.ToTriangles().Where(t => !t.IsDegenerate()).Select(t => t.Normal()).ToList();
        var repaired = TJunctionRepair.Repair(mesh, out _);

        foreach (var normal in repaired.ToTriangles().Where(t => !t.IsDegenerate()).Select(t => t.Normal()))
        {
            before.Should().Contain(n => Vector3.Dot(n, normal) > 0.99f,
                "every piece still faces the way its original did");
        }
    }

    [Fact]
    public void Splitting_preserves_the_total_area()
    {
        var mesh = VertexWelder.Weld(SeamWithAHangingCorner());

        var before = mesh.ToTriangles().Sum(t => t.Area());
        var repaired = TJunctionRepair.Repair(mesh, out _);
        var after = repaired.ToTriangles().Sum(t => t.Area());

        after.Should().BeApproximately(before, before * 1e-4f);
    }

    [Fact]
    public void A_mesh_with_no_hanging_corners_is_returned_untouched()
    {
        var mesh = VertexWelder.Weld(
        [
            new Triangle(new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(0, 10, 0)),
            new Triangle(new Vector3(10, 0, 0), new Vector3(10, 10, 0), new Vector3(0, 10, 0)),
        ]);

        var repaired = TJunctionRepair.Repair(mesh, out var splits);

        splits.Should().Be(0);
        repaired.Should().BeSameAs(mesh);
    }

    [Fact]
    public void An_empty_mesh_is_handled()
    {
        var repaired = TJunctionRepair.Repair(IndexedMesh.Empty, out var splits);

        splits.Should().Be(0);
        repaired.TriangleCount.Should().Be(0);
    }

    /// <summary>Several corners on one edge are all split, not just the first.</summary>
    [Fact]
    public void An_edge_with_several_corners_on_it_is_split_at_each()
    {
        var mesh = VertexWelder.Weld(
        [
            // A tall triangle whose vertical edge runs from y=0 to y=30.
            new Triangle(new Vector3(0, 0, 0), new Vector3(0, 30, 0), new Vector3(-10, 15, 0)),

            // Three triangles on the other side, meeting the tall edge at y=10 and y=20.
            new Triangle(new Vector3(0, 0, 0), new Vector3(10, 5, 0), new Vector3(0, 10, 0)),
            new Triangle(new Vector3(0, 10, 0), new Vector3(10, 15, 0), new Vector3(0, 20, 0)),
            new Triangle(new Vector3(0, 20, 0), new Vector3(10, 25, 0), new Vector3(0, 30, 0)),
        ]);

        var repaired = TJunctionRepair.Repair(mesh, out var splits);

        splits.Should().Be(2, "the long edge is cut at both corners lying on it");
        repaired.TriangleCount.Should().Be(mesh.TriangleCount + 2);
    }

    /// <summary>A corner at an edge's end is where surfaces already meet, so nothing to do.</summary>
    [Fact]
    public void A_corner_at_the_end_of_an_edge_is_not_a_split()
    {
        var mesh = VertexWelder.Weld(
        [
            new Triangle(new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(0, 10, 0)),
            new Triangle(new Vector3(10, 0, 0), new Vector3(20, 0, 0), new Vector3(10, 10, 0)),
        ]);

        TJunctionRepair.Repair(mesh, out var splits);

        splits.Should().Be(0);
    }
}

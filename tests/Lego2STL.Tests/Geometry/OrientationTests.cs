using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.LDraw;

namespace Lego2STL.Tests.Geometry;

/// <summary>
/// Laying a part down the way its kind is printed.
/// </summary>
/// <remarks>
/// Every rule here changes nothing, and that is the whole point of them. The measurement that led
/// to this feature found that the parts a geometric score shouts about - plates and panels - are
/// the ones already lying correctly, and the one rule that was going to be a correction, rolling an
/// axle onto one arm of its cross, was measured on the six real axles of run 6324712 and rejected.
/// So the tests that assert nothing moved are not the supporting cast; they are the feature.
/// </remarks>
public sealed class OrientationTests
{
    /// <summary>A bar along X, four units square, standing for a part already lying flat.</summary>
    private static PartMesh ABarCalled(string title)
    {
        var t = new List<Triangle>();

        // A closed box from (0,-2,-2) to (40,2,2), as two triangles per face.
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            t.Add(new Triangle(a, b, c));
            t.Add(new Triangle(a, c, d));
        }

        Vector3 V(float x, float y, float z) => new(x, y, z);

        Quad(V(0, -2, -2), V(0, 2, -2), V(0, 2, 2), V(0, -2, 2));
        Quad(V(40, -2, -2), V(40, -2, 2), V(40, 2, 2), V(40, 2, -2));
        Quad(V(0, -2, -2), V(0, -2, 2), V(40, -2, 2), V(40, -2, -2));
        Quad(V(0, 2, -2), V(40, 2, -2), V(40, 2, 2), V(0, 2, 2));
        Quad(V(0, -2, 2), V(0, 2, 2), V(40, 2, 2), V(40, -2, 2));
        Quad(V(0, -2, -2), V(40, -2, -2), V(40, 2, -2), V(0, 2, -2));

        return new PartMesh("test", title, t, null, 1, []);
    }

    private static IReadOnlyList<Vector3> CornersOf(PartMesh part, bool orient) =>
        MeshPipeline.Prepare(part, new MeshPipelineOptions { Orient = orient }).Mesh.Vertices;

    /// <summary>Every rule in the table is a rule to leave the part exactly where it was.</summary>
    [Theory]
    [InlineData("Plate  2 x  4")]
    [InlineData("Tile  1 x  2")]
    [InlineData("Brick  2 x  2")]
    [InlineData("Technic Beam 15")]
    [InlineData("Technic Pin Long")]
    [InlineData("Technic Axle 10")]
    [InlineData("Technic Panel  5 x 11")]
    [InlineData("Wheel Rim 16 x 31")]
    public void A_part_whose_rule_confirms_what_the_pipeline_already_did_does_not_move(string title)
    {
        var part = ABarCalled(title);

        // Every corner, not the bounding box: a box compared with itself after a quarter turn
        // measures the same and would let a real rotation through unnoticed.
        CornersOf(part, orient: true).Should().Equal(CornersOf(part, orient: false));
    }

    /// <summary>
    /// Nothing in the table turns anything, and a change to that is a decision, not a tidy-up.
    /// </summary>
    /// <remarks>
    /// Asserted over the kinds rather than over a mesh, so that adding a turn fails here first,
    /// where the reason to think again is written down, instead of in some snapshot of a plate.
    /// The one turn this design proposed - rolling an axle 45 degrees onto one arm of its cross -
    /// was measured on six real axles and lowered the overhanging area for only four of them,
    /// because the roll leaves every underside face exactly on the 45 degree limit.
    /// </remarks>
    [Theory]
    [InlineData(PartKind.Unknown)]
    [InlineData(PartKind.Brick)]
    [InlineData(PartKind.Plate)]
    [InlineData(PartKind.Tile)]
    [InlineData(PartKind.Beam)]
    [InlineData(PartKind.Axle)]
    [InlineData(PartKind.Pin)]
    public void No_rule_turns_anything(PartKind kind) =>
        Orientation.For(kind).Should().BeNull();

    /// <summary>Turned off, nothing is laid down at all, whatever the part is.</summary>
    [Fact]
    public void Orientation_can_be_turned_off_entirely()
    {
        var part = ABarCalled("Technic Axle 10");

        MeshPipeline.Prepare(part, new MeshPipelineOptions { Orient = false })
            .LaidDown.Should().BeNull();
    }

    /// <summary>
    /// Which rule decided is recorded, because every other decision here is.
    /// </summary>
    /// <remarks>
    /// A rule that confirms is still a rule, and recording it is the only way the parts no rule
    /// reached can be read off a real run - which is how the table is meant to grow.
    /// </remarks>
    [Fact]
    public void Which_rule_laid_a_part_down_is_recorded()
    {
        MeshPipeline.Prepare(ABarCalled("Technic Axle 10")).LaidDown.Should().Be("Axle");
        MeshPipeline.Prepare(ABarCalled("Plate  2 x  4")).LaidDown.Should().Be("Plate");
        MeshPipeline.Prepare(ABarCalled("Wheel Rim 16 x 31")).LaidDown.Should().BeNull();
    }
}

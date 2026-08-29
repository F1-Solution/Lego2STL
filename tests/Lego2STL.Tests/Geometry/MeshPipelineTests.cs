using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.LDraw;
using Xunit;

namespace Lego2STL.Tests.Geometry;

/// <summary>
/// Preparing a part's surfaces, and what it is allowed to do to get them closed.
/// </summary>
/// <remarks>
/// The escalation exists because welding at the tolerance asked for leaves some parts open
/// while a slightly larger one closes them exactly - measured across run 6324712, 8 of the 33
/// parts with real holes. It is bounded well below what a nozzle can resolve, and it must never
/// touch a shape that was already closed, which is what the second test here is for.
/// </remarks>
public sealed class MeshPipelineTests
{
    /// <summary>A shape already closed is not re-prepared, so it cannot change at all.</summary>
    [Fact]
    public void A_shape_that_is_already_closed_is_left_exactly_as_it_was()
    {
        var part = ABox();

        var prepared = MeshPipeline.Prepare(part);

        prepared.Quality.IsClosed.Should().BeTrue("a box is closed to begin with");
        prepared.ClosedAtTolerance.Should().BeNull("nothing had to be escalated");
        prepared.Mesh.TriangleCount.Should().Be(part.Triangles.Count);
    }

    /// <summary>A gap too wide for the asked-for tolerance is closed by a larger one.</summary>
    [Fact]
    public void A_shape_still_open_is_tried_again_more_tolerantly()
    {
        var part = ABoxWithASliverFace(0.01f);

        MeshPipeline.Prepare(part, new MeshPipelineOptions { WeldTolerance = 1e-4f, FillGaps = false })
            .Quality.IsClosed.Should().BeFalse("the sliver leaves an edge shared three ways");

        var prepared = MeshPipeline.Prepare(part, new MeshPipelineOptions
        {
            WeldTolerance = 1e-4f,
        });

        prepared.Quality.IsClosed.Should().BeTrue();
        prepared.ClosedAtTolerance.Should().NotBeNull("it took more than was asked for");
        prepared.ClosedAtTolerance.Should().BeLessThanOrEqualTo(0.1f, "the ladder is bounded");
    }

    /// <summary>Turning repair off turns the escalation off with it.</summary>
    [Fact]
    public void Asking_for_no_repair_asks_for_no_escalation_either()
    {
        var part = ABoxWithASliverFace(0.01f);

        var prepared = MeshPipeline.Prepare(part, new MeshPipelineOptions
        {
            WeldTolerance = 1e-4f,
            FillGaps = false,
        });

        prepared.ClosedAtTolerance.Should().BeNull();
    }

    /// <summary>A closed box: eight corners, twelve triangles, every edge shared twice.</summary>
    private static PartMesh ABox() => new(
        Reference: "box",
        Title: "a box",
        Triangles: [.. BoxTriangles(Vector3.Zero)],
        MovedTo: null,
        FilesUsed: 1,
        MissingReferences: []);

    /// <summary>
    /// The same box with a sliver face laid over one of its edges, its free corner a hair away
    /// from one already there.
    /// </summary>
    /// <remarks>
    /// The kind of leftover the source is full of. At a tight tolerance the sliver survives and
    /// its edge is shared three ways, which no amount of covering gaps can put right; at a
    /// looser one its free corner merges with the one it sits on, the face flattens to nothing
    /// and is dropped, and the box is a box again.
    /// </remarks>
    private static PartMesh ABoxWithASliverFace(float by)
    {
        var triangles = BoxTriangles(Vector3.Zero).ToList();
        triangles.Add(new Triangle(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(by, by / 10f, 0)));

        return new PartMesh("sliver", "a box with a leftover face", triangles, null, 1, []);
    }

    private static IEnumerable<Triangle> BoxTriangles(Vector3 origin)
    {
        Vector3[] c =
        [
            origin + new Vector3(0, 0, 0), origin + new Vector3(1, 0, 0),
            origin + new Vector3(1, 1, 0), origin + new Vector3(0, 1, 0),
            origin + new Vector3(0, 0, 1), origin + new Vector3(1, 0, 1),
            origin + new Vector3(1, 1, 1), origin + new Vector3(0, 1, 1),
        ];

        int[][] faces =
        [
            [0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4],
            [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7],
        ];

        foreach (var f in faces)
        {
            yield return new Triangle(c[f[0]], c[f[1]], c[f[2]]);
            yield return new Triangle(c[f[0]], c[f[2]], c[f[3]]);
        }
    }

    /// <summary>A one-unit cube in source units, which should come out 0.4 mm.</summary>
    private static PartMesh UnitCube()
    {
        var triangles = new List<Triangle>();
        var corners = new[]
        {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0),
            new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1),
        };

        void Quad(int a, int b, int c, int d)
        {
            triangles.Add(new Triangle(corners[a], corners[b], corners[c]));
            triangles.Add(new Triangle(corners[a], corners[c], corners[d]));
        }

        Quad(0, 3, 2, 1);
        Quad(4, 5, 6, 7);
        Quad(0, 1, 5, 4);
        Quad(1, 2, 6, 5);
        Quad(2, 3, 7, 6);
        Quad(3, 0, 4, 7);

        return new PartMesh("cube", "Test cube", triangles, null, 1, []);
    }

    [Fact]
    public void Source_units_are_converted_to_millimetres()
    {
        var prepared = MeshPipeline.Prepare(UnitCube());

        // One source unit is 0.4 mm.
        prepared.Size.X.Should().BeApproximately(0.4f, 1e-4f);
        prepared.Size.Y.Should().BeApproximately(0.4f, 1e-4f);
        prepared.Size.Z.Should().BeApproximately(0.4f, 1e-4f);
    }

    /// <summary>
    /// A standard brick is 20 source units wide, and must come out at exactly 8 mm, because
    /// that is the spacing everything else is measured against.
    /// </summary>
    [Fact]
    public void A_standard_stud_spacing_comes_out_at_exactly_eight_millimetres()
    {
        var twentyUnits = new PartMesh("wide", null,
        [
            new Triangle(new Vector3(0, 0, 0), new Vector3(20, 0, 0), new Vector3(0, 1, 0)),
        ], null, 1, []);

        MeshPipeline.Prepare(twentyUnits).Size.X.Should().BeApproximately(8f, 1e-4f);
    }

    [Fact]
    public void The_shape_is_stood_on_zero_and_centred()
    {
        var prepared = MeshPipeline.Prepare(UnitCube());
        var (min, max) = prepared.Bounds;

        min.Z.Should().BeApproximately(0f, 1e-5f, "it should sit on the bed");
        (min.X + max.X).Should().BeApproximately(0f, 1e-5f, "centred left to right");
        (min.Y + max.Y).Should().BeApproximately(0f, 1e-5f, "centred front to back");
    }

    [Fact]
    public void Keeping_the_original_origin_does_not_move_the_shape()
    {
        var prepared = MeshPipeline.Prepare(UnitCube(), new MeshPipelineOptions { PlaceOnBed = false });

        prepared.Bounds.Min.Z.Should().NotBeApproximately(0f, 1e-6f);
    }

    [Fact]
    public void Scaling_multiplies_every_dimension()
    {
        var prepared = MeshPipeline.Prepare(UnitCube(), new MeshPipelineOptions { ScalePercent = 130f });

        prepared.Size.X.Should().BeApproximately(0.4f * 1.3f, 1e-4f);
    }

    [Fact]
    public void A_closed_shape_is_reported_as_closed()
    {
        MeshPipeline.Prepare(UnitCube()).Quality.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void The_measurement_before_repair_is_kept_so_the_repair_can_be_credited()
    {
        var prepared = MeshPipeline.Prepare(UnitCube());

        prepared.QualityBeforeRepair.TriangleCount.Should().Be(12);
        prepared.SeamsClosed.Should().Be(0, "a clean cube needs no repair");
    }
}

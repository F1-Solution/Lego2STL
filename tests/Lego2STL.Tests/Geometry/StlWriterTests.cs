using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.LDraw;

namespace Lego2STL.Tests.Geometry;

public sealed class StlWriterTests
{
    private static IndexedMesh OneTriangle() => VertexWelder.Weld(
    [
        new Triangle(new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(0, 10, 0)),
    ]);

    [Fact]
    public void The_compact_form_has_the_size_the_format_requires()
    {
        var bytes = StlWriter.WriteBinary(OneTriangle());

        // 80-byte header, a 4-byte count, then 50 bytes per triangle.
        bytes.Should().HaveCount(80 + 4 + 50);
        StlWriter.ReadBinaryTriangleCount(bytes).Should().Be(1);
    }

    [Fact]
    public void The_header_carries_the_part_name()
    {
        var bytes = StlWriter.WriteBinary(OneTriangle(), "32523");

        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("32523");
    }

    [Fact]
    public void Triangles_with_no_area_are_left_out()
    {
        var mesh = VertexWelder.Weld(
        [
            new Triangle(new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(0, 10, 0)),
            new Triangle(new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(20, 0, 0)),
        ]);

        StlWriter.ReadBinaryTriangleCount(StlWriter.WriteBinary(mesh)).Should().Be(1);
    }

    [Fact]
    public void The_readable_form_is_well_formed()
    {
        var text = StlWriter.WriteText(OneTriangle(), "part");

        text.Should().StartWith("solid part");
        text.Should().EndWith("endsolid part\n");
        text.Should().Contain("facet normal");
        text.Should().Contain("outer loop");

        // One triangle: one facet, three vertices.
        System.Text.RegularExpressions.Regex.Matches(text, "vertex").Should().HaveCount(3);
    }

    [Fact]
    public void An_empty_mesh_still_writes_a_valid_file()
    {
        var bytes = StlWriter.WriteBinary(IndexedMesh.Empty);

        bytes.Should().HaveCount(84);
        StlWriter.ReadBinaryTriangleCount(bytes).Should().Be(0);
    }
}

public sealed class MeshPipelineTests
{
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

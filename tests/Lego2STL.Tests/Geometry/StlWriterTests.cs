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

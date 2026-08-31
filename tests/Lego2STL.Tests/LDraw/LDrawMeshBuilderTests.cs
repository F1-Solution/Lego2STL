using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.LDraw;

namespace Lego2STL.Tests.LDraw;

/// <summary>
/// Tests the walk over references against files small enough that the right answer can be
/// worked out by hand.
/// </summary>
public sealed class LDrawMeshBuilderTests
{
    // The nine numbers of an unrotated reference; a line also carries x y z before them.
    private const string Identity = "1 0 0 0 1 0 0 0 1";

    [Fact]
    public async Task A_single_triangle_becomes_one_triangle()
    {
        var library = new FakeLDrawLibrary().Add("t.dat", "0 One triangle\n3 16 0 0 0  10 0 0  0 10 0\n");

        var mesh = await new LDrawMeshBuilder(library).BuildAsync("t");

        mesh.Triangles.Should().HaveCount(1);
        mesh.Triangles[0].Should().Be(new Core.Geometry.Triangle(
            new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(0, 10, 0)));
        mesh.Title.Should().Be("One triangle");
    }

    [Fact]
    public async Task A_four_cornered_face_becomes_two_triangles_sharing_a_diagonal()
    {
        var library = new FakeLDrawLibrary().Add("q.dat", "0 A quad\n4 16 0 0 0  10 0 0  10 10 0  0 10 0\n");

        var mesh = await new LDrawMeshBuilder(library).BuildAsync("q");

        mesh.Triangles.Should().HaveCount(2);

        // Both halves face the same way, so the quad is not folded.
        mesh.Triangles[0].Normal().Should().Be(mesh.Triangles[1].Normal());

        // Together they cover the whole square.
        mesh.Triangles.Sum(t => t.Area()).Should().BeApproximately(100f, 1e-3f);
    }

    /// <summary>Edge lines and conditional edge lines draw outlines; they are not surfaces.</summary>
    [Fact]
    public async Task Line_types_that_are_not_surfaces_are_ignored()
    {
        var library = new FakeLDrawLibrary().Add(
            "mixed.dat",
            "0 Mixed\n" +
            "2 24 0 0 0  10 0 0\n" +
            "5 24 0 0 0  10 0 0  0 10 0  10 10 0\n" +
            "3 16 0 0 0  10 0 0  0 10 0\n");

        var mesh = await new LDrawMeshBuilder(library).BuildAsync("mixed");

        mesh.Triangles.Should().HaveCount(1);
    }

    [Fact]
    public async Task A_reference_places_the_other_file_where_it_says()
    {
        var library = new FakeLDrawLibrary()
            .Add("root.dat", $"0 Root\n1 16 100 0 0 {Identity} child.dat\n")
            .Add("child.dat", "0 Child\n3 16 0 0 0  10 0 0  0 10 0\n");

        var mesh = await new LDrawMeshBuilder(library).BuildAsync("root");

        mesh.Triangles.Should().HaveCount(1);
        mesh.Triangles[0].A.Should().Be(new Vector3(100, 0, 0));
        mesh.Triangles[0].B.Should().Be(new Vector3(110, 0, 0));
        mesh.FilesUsed.Should().Be(2);
    }

    [Fact]
    public async Task Nested_references_compose_their_transforms()
    {
        var library = new FakeLDrawLibrary()
            .Add("a.dat", $"0 A\n1 16 100 0 0 {Identity} b.dat\n")
            .Add("b.dat", $"0 B\n1 16 0 50 0 {Identity} c.dat\n")
            .Add("c.dat", "0 C\n3 16 0 0 0  1 0 0  0 1 0\n");

        var mesh = await new LDrawMeshBuilder(library).BuildAsync("a");

        mesh.Triangles[0].A.Should().Be(new Vector3(100, 50, 0));
        mesh.FilesUsed.Should().Be(3);
    }

    /// <summary>
    /// A reference marked for inversion has its surfaces turned the other way, which is how a
    /// part reuses one primitive for both the outside and the inside of a wall.
    /// </summary>
    [Fact]
    public async Task An_inverted_reference_has_its_surfaces_reversed()
    {
        var plain = new FakeLDrawLibrary()
            .Add("plain.dat", $"0 Plain\n1 16 0 0 0 {Identity} face.dat\n")
            .Add("face.dat", "0 Face\n3 16 0 0 0  10 0 0  0 10 0\n");

        var inverted = new FakeLDrawLibrary()
            .Add("inv.dat", $"0 Inverted\n0 BFC INVERTNEXT\n1 16 0 0 0 {Identity} face.dat\n")
            .Add("face.dat", "0 Face\n3 16 0 0 0  10 0 0  0 10 0\n");

        var normal = await new LDrawMeshBuilder(plain).BuildAsync("plain");
        var flipped = await new LDrawMeshBuilder(inverted).BuildAsync("inv");

        flipped.Triangles[0].Normal().Should().Be(-normal.Triangles[0].Normal());
    }

    /// <summary>
    /// A mirroring transform reverses surfaces just as the explicit marker does, so a part
    /// built from mirrored halves still has every face pointing outwards.
    /// </summary>
    [Fact]
    public async Task A_mirroring_transform_also_reverses_surfaces()
    {
        var library = new FakeLDrawLibrary()
            .Add("m.dat", "0 Mirrored\n1 16 0 0 0 -1 0 0 0 1 0 0 0 1 face.dat\n")
            .Add("face.dat", "0 Face\n3 16 0 0 0  10 0 0  0 10 0\n");

        var mesh = await new LDrawMeshBuilder(library).BuildAsync("m");

        // Mirrored about x, the corners land on negative x; and because the mesh builder
        // reverses the winding, the surface still faces the same way as the original.
        mesh.Triangles[0].B.X.Should().Be(-10);
        mesh.Triangles[0].Normal().Z.Should().BePositive();
    }

    /// <summary>Both reversals together cancel out.</summary>
    [Fact]
    public async Task A_mirrored_and_marked_reference_is_not_reversed_twice()
    {
        var library = new FakeLDrawLibrary()
            .Add("m.dat", "0 Both\n0 BFC INVERTNEXT\n1 16 0 0 0 -1 0 0 0 1 0 0 0 1 face.dat\n")
            .Add("face.dat", "0 Face\n3 16 0 0 0  10 0 0  0 10 0\n");

        var mesh = await new LDrawMeshBuilder(library).BuildAsync("m");

        mesh.Triangles[0].Normal().Z.Should().BeNegative();
    }

    [Fact]
    public async Task A_file_declaring_its_surfaces_wound_the_other_way_has_them_reversed()
    {
        var ccw = new FakeLDrawLibrary().Add("f.dat", "0 F\n0 BFC CERTIFY CCW\n3 16 0 0 0  10 0 0  0 10 0\n");
        var cw = new FakeLDrawLibrary().Add("f.dat", "0 F\n0 BFC CERTIFY CW\n3 16 0 0 0  10 0 0  0 10 0\n");

        var a = await new LDrawMeshBuilder(ccw).BuildAsync("f");
        var b = await new LDrawMeshBuilder(cw).BuildAsync("f");

        b.Triangles[0].Normal().Should().Be(-a.Triangles[0].Normal());
    }

    /// <summary>The marker applies to one reference only, not to everything that follows.</summary>
    [Fact]
    public async Task The_inversion_marker_applies_only_to_the_next_reference()
    {
        var library = new FakeLDrawLibrary()
            .Add("r.dat",
                "0 R\n" +
                "0 BFC INVERTNEXT\n" +
                $"1 16 0 0 0 {Identity} face.dat\n" +
                $"1 16 0 0 100 {Identity} face.dat\n")
            .Add("face.dat", "0 Face\n3 16 0 0 0  10 0 0  0 10 0\n");

        var mesh = await new LDrawMeshBuilder(library).BuildAsync("r");

        mesh.Triangles.Should().HaveCount(2);
        mesh.Triangles[0].Normal().Should().Be(-mesh.Triangles[1].Normal());
    }

    [Fact]
    public async Task A_retired_number_reports_the_part_it_redirects_to()
    {
        var library = new FakeLDrawLibrary()
            .Add("old.dat", $"0 ~Moved to new\n0 Name: old.dat\n1 16 0 0 0 {Identity} new.dat\n")
            .Add("new.dat", "0 New part\n3 16 0 0 0  10 0 0  0 10 0\n");

        var mesh = await new LDrawMeshBuilder(library).BuildAsync("old");

        mesh.MovedTo.Should().Be("new");
        mesh.Triangles.Should().HaveCount(1, "the shape still comes through");
    }

    [Fact]
    public async Task A_missing_reference_is_reported_and_the_rest_still_builds()
    {
        var library = new FakeLDrawLibrary()
            .Add("r.dat", $"0 R\n3 16 0 0 0  10 0 0  0 10 0\n1 16 0 0 0 {Identity} absent.dat\n");

        var mesh = await new LDrawMeshBuilder(library).BuildAsync("r");

        mesh.Triangles.Should().HaveCount(1);
        mesh.MissingReferences.Should().ContainSingle().Which.Should().Contain("absent.dat");
    }

    [Fact]
    public async Task A_part_that_is_not_there_at_all_is_reported_as_such()
    {
        var act = () => new LDrawMeshBuilder(new FakeLDrawLibrary()).BuildAsync("nope");

        (await act.Should().ThrowAsync<LDrawPartNotFoundException>()).Which.PartNumber.Should().Be("nope");
    }

    /// <summary>A cycle in the data must not loop for ever.</summary>
    [Fact]
    public async Task A_file_that_refers_to_itself_is_stopped_and_reported()
    {
        var library = new FakeLDrawLibrary()
            .Add("loop.dat", $"0 Loop\n3 16 0 0 0  10 0 0  0 10 0\n1 16 0 0 0 {Identity} loop.dat\n");

        var mesh = await new LDrawMeshBuilder(library).BuildAsync("loop");

        mesh.Triangles.Should().HaveCount(1);
        mesh.MissingReferences.Should().Contain(m => m.Contains("refers to itself"));
    }

    [Fact]
    public async Task Two_files_referring_to_each_other_are_stopped()
    {
        var library = new FakeLDrawLibrary()
            .Add("a.dat", $"0 A\n1 16 0 0 0 {Identity} b.dat\n")
            .Add("b.dat", $"0 B\n1 16 0 0 0 {Identity} a.dat\n3 16 0 0 0  1 0 0  0 1 0\n");

        var mesh = await new LDrawMeshBuilder(library).BuildAsync("a");

        mesh.Triangles.Should().HaveCount(1);
        mesh.MissingReferences.Should().Contain(m => m.Contains("refers to itself"));
    }

    [Fact]
    public async Task A_repeated_primitive_is_parsed_once_but_placed_every_time()
    {
        var library = new FakeLDrawLibrary()
            .Add("r.dat",
                "0 R\n" +
                $"1 16 0 0 0 {Identity} p.dat\n" +
                $"1 16 20 0 0 {Identity} p.dat\n" +
                $"1 16 40 0 0 {Identity} p.dat\n")
            .Add("p.dat", "0 P\n3 16 0 0 0  10 0 0  0 10 0\n");

        var mesh = await new LDrawMeshBuilder(library).BuildAsync("r");

        mesh.Triangles.Should().HaveCount(3);
        library.Requested.Count(r => r.Contains("p.dat", StringComparison.OrdinalIgnoreCase))
            .Should().Be(1, "the parsed file is kept rather than read again");
    }

    [Fact]
    public async Task A_part_number_may_be_given_with_or_without_the_file_extension()
    {
        var library = new FakeLDrawLibrary().Add("3001.dat", "0 Brick\n3 16 0 0 0  10 0 0  0 10 0\n");

        var withoutExtension = await new LDrawMeshBuilder(library).BuildAsync("3001");
        var withExtension = await new LDrawMeshBuilder(library).BuildAsync("3001.dat");

        withoutExtension.Triangles.Should().HaveCount(1);
        withExtension.Triangles.Should().HaveCount(1);
    }

    /// <summary>
    /// A part that is only a redirection is described by the part it redirects to.
    /// </summary>
    /// <remarks>
    /// The mesh was always right - the builder follows the redirection when it expands the file -
    /// but the description recorded was the stub's own, "~Moved to 3023b", which says nothing
    /// about what the part is. Four of run 6324712's parts are like this, and the reader that
    /// works out what kind of part it is has only that description to go on.
    /// </remarks>
    [Fact]
    public async Task A_part_that_only_redirects_is_described_by_the_part_it_becomes()
    {
        var library = new FakeLDrawLibrary()
            .Add("3023.dat", $"0 ~Moved to 3023b\n1 16 0 0 0 {Identity} 3023b.dat\n")
            .Add("3023b.dat", "0 Plate  1 x  2\n3 16 0 0 0  10 0 0  0 10 0\n");

        var mesh = await new LDrawMeshBuilder(library).BuildAsync("3023");

        mesh.MovedTo.Should().Be("3023b");
        mesh.Title.Should().Be("Plate  1 x  2");
        mesh.Triangles.Should().ContainSingle("the geometry always came from the part it points at");
    }
}

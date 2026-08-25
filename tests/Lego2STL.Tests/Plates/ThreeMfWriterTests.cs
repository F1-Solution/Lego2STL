using System.IO.Compression;
using System.Numerics;
using System.Xml.Linq;
using FluentAssertions;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.Plates;

namespace Lego2STL.Tests.Plates;

/// <summary>
/// Checks the plate file against what the format requires, by reading it back the way a
/// slicer would: open the package, find the model through the relationship, and read it.
/// </summary>
public sealed class ThreeMfWriterTests
{
    private const string Core = "http://schemas.microsoft.com/3dmanufacturing/core/2015/02";

    private const string Material = "http://schemas.microsoft.com/3dmanufacturing/material/2015/02";

    /// <summary>A tetrahedron: the smallest closed shape, so a valid mesh to write.</summary>
    private static IndexedMesh Tetrahedron() =>
        new(
            [
                new Vector3(0, 0, 0),
                new Vector3(10, 0, 0),
                new Vector3(0, 10, 0),
                new Vector3(0, 0, 10),
            ],
            [
                new IndexedTriangle(0, 2, 1),
                new IndexedTriangle(0, 1, 3),
                new IndexedTriangle(0, 3, 2),
                new IndexedTriangle(1, 2, 3),
            ]);

    private static PlateContents Plate(params Vector2[] positions) =>
        new(
            "black.3mf",
            "Black",
            Rgb24.Parse("#05131D"),
            [new PlateObject("3705", Tetrahedron(), positions.Length == 0 ? [Vector2.Zero] : positions)]);

    private static XDocument ModelIn(byte[] bytes)
    {
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        using var stream = archive.GetEntry("3D/3dmodel.model")!.Open();
        return XDocument.Load(stream);
    }

    [Fact]
    public void The_package_holds_the_three_parts_the_format_requires()
    {
        using var archive = new ZipArchive(new MemoryStream(ThreeMfWriter.Write(Plate())), ZipArchiveMode.Read);

        archive.Entries.Select(e => e.FullName).Should().BeEquivalentTo(
            ["[Content_Types].xml", "_rels/.rels", "3D/3dmodel.model"]);
    }

    [Fact]
    public void The_relationship_points_at_the_model()
    {
        using var archive = new ZipArchive(new MemoryStream(ThreeMfWriter.Write(Plate())), ZipArchiveMode.Read);
        using var stream = archive.GetEntry("_rels/.rels")!.Open();

        var rels = XDocument.Load(stream);
        var target = rels.Root!.Elements().Single().Attribute("Target")!.Value;

        target.Should().Be("/3D/3dmodel.model");
    }

    [Fact]
    public void Measurements_are_in_millimetres()
    {
        ModelIn(ThreeMfWriter.Write(Plate())).Root!
            .Attribute("unit")!.Value.Should().Be("millimeter");
    }

    /// <summary>
    /// What the object points at is the colour group, because that is the one the slicers
    /// read. Pointing it at the base material instead is what left plates arriving grey.
    /// </summary>
    [Fact]
    public void The_plates_colour_is_carried_as_a_colour_group_the_object_points_at()
    {
        var model = ModelIn(ThreeMfWriter.Write(Plate()));

        var group = model.Descendants(XName.Get("colorgroup", Material)).Single();
        var colour = group.Elements(XName.Get("color", Material)).Single();

        colour.Attribute("color")!.Value.Should().Be("#05131DFF");

        var shape = model.Descendants(XName.Get("object", Core)).Single();
        shape.Attribute("pid")!.Value.Should().Be(group.Attribute("id")!.Value);
        shape.Attribute("pindex")!.Value.Should().Be("0");
    }

    /// <summary>
    /// Readers match the element as it is written rather than resolving the namespace, so the
    /// prefix is part of what has to be right.
    /// </summary>
    [Fact]
    public void The_colour_group_is_written_under_the_prefix_readers_look_for()
    {
        var model = ModelIn(ThreeMfWriter.Write(Plate()));
        var group = model.Descendants(XName.Get("colorgroup", Material)).Single();

        group.GetPrefixOfNamespace(Material).Should().Be("m");
    }

    /// <summary>
    /// The same colour is also stated in the core format's own terms, for a viewer that knows
    /// nothing of the materials extension.
    /// </summary>
    [Fact]
    public void The_plates_colour_is_also_stated_as_a_base_material()
    {
        var model = ModelIn(ThreeMfWriter.Write(Plate()));

        var materials = model.Descendants(XName.Get("basematerials", Core)).Single();
        var material = materials.Elements(XName.Get("base", Core)).Single();

        material.Attribute("name")!.Value.Should().Be("Black");
        material.Attribute("displaycolor")!.Value.Should().Be("#05131DFF");
    }

    /// <summary>Every resource in a model needs an id of its own, objects included.</summary>
    [Fact]
    public void No_two_resources_share_an_id()
    {
        var model = ModelIn(ThreeMfWriter.Write(Plate()));

        var ids = model.Descendants(XName.Get("resources", Core)).Single()
            .Elements()
            .Select(r => r.Attribute("id")!.Value)
            .ToList();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Every_corner_and_face_reaches_the_file()
    {
        var model = ModelIn(ThreeMfWriter.Write(Plate()));

        model.Descendants(XName.Get("vertex", Core)).Should().HaveCount(4);
        model.Descendants(XName.Get("triangle", Core)).Should().HaveCount(4);
    }

    [Fact]
    public void Every_face_names_corners_that_exist()
    {
        var model = ModelIn(ThreeMfWriter.Write(Plate()));
        var corners = model.Descendants(XName.Get("vertex", Core)).Count();

        foreach (var triangle in model.Descendants(XName.Get("triangle", Core)))
        {
            foreach (var corner in new[] { "v1", "v2", "v3" })
            {
                var index = int.Parse(triangle.Attribute(corner)!.Value);
                index.Should().BeInRange(0, corners - 1);
            }
        }
    }

    /// <summary>
    /// Several copies of one part share a single mesh, with a placement each. This is the
    /// difference between a small plate file and one that repeats the same geometry.
    /// </summary>
    [Fact]
    public void Copies_of_a_part_share_one_mesh_and_get_a_placement_each()
    {
        var model = ModelIn(ThreeMfWriter.Write(
            Plate(new Vector2(0, 0), new Vector2(20, 0), new Vector2(40, 0))));

        model.Descendants(XName.Get("object", Core)).Should().HaveCount(1);
        model.Descendants(XName.Get("item", Core)).Should().HaveCount(3);
    }

    [Fact]
    public void A_placement_moves_the_copy_to_its_own_spot()
    {
        var model = ModelIn(ThreeMfWriter.Write(Plate(new Vector2(12.5f, 30f))));

        model.Descendants(XName.Get("item", Core)).Single()
            .Attribute("transform")!.Value
            .Should().Be("1 0 0 0 1 0 0 0 1 12.5 30 0");
    }

    [Fact]
    public void A_placement_names_an_object_that_is_in_the_file()
    {
        var model = ModelIn(ThreeMfWriter.Write(Plate()));

        var ids = model.Descendants(XName.Get("object", Core))
            .Select(o => o.Attribute("id")!.Value)
            .ToList();

        foreach (var item in model.Descendants(XName.Get("item", Core)))
        {
            ids.Should().Contain(item.Attribute("objectid")!.Value);
        }
    }

    /// <summary>
    /// Writing the same plate twice has to give the same bytes, or nothing downstream can be
    /// compared. A zip records when it was made, so that has to be pinned.
    /// </summary>
    [Fact]
    public void Writing_the_same_plate_twice_gives_the_same_bytes()
    {
        ThreeMfWriter.Write(Plate()).Should().Equal(ThreeMfWriter.Write(Plate()));
    }

    [Fact]
    public void Faces_with_no_area_are_left_out()
    {
        var withDegenerate = new IndexedMesh(
            Tetrahedron().Vertices,
            [.. Tetrahedron().Triangles, new IndexedTriangle(1, 1, 2)]);

        var plate = new PlateContents(
            "x.3mf",
            "Black",
            Rgb24.Parse("#05131D"),
            [new PlateObject("3705", withDegenerate, [Vector2.Zero])]);

        ModelIn(ThreeMfWriter.Write(plate))
            .Descendants(XName.Get("triangle", Core)).Should().HaveCount(4);
    }
}

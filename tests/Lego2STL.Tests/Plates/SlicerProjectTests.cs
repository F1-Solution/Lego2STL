using System.Numerics;
using System.Text.Json;
using System.Xml.Linq;
using FluentAssertions;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.Plates;

namespace Lego2STL.Tests.Plates;

/// <summary>
/// The combined plate file as a Bambu Studio or OrcaSlicer project (#12).
/// </summary>
/// <remarks>
/// The slicers put a part on whichever plate it sits over, so the layout here has to be the one
/// they compute for themselves, to the millimetre: the column count, the gap and the direction
/// the rows run in were all read off their sources, and are pinned here against those.
/// </remarks>
public sealed class SlicerProjectTests
{
    private static readonly Rgb24 Black = Rgb24.Parse("#05131D");
    private static readonly Rgb24 Red = Rgb24.Parse("#C91A09");
    private static readonly Rgb24 NearlyRed = Rgb24.Parse("#D01012");

    private static SlicerProject A1(ProjectFormat format = ProjectFormat.BambuStudio) =>
        SlicerProject.For(format, "A1", PrintBeds.A1)!;

    private static PlateContents Plate(string colour, Rgb24 rgb) =>
        new($"{colour}.3mf", colour, rgb, [new PlateObject("3001", Tetrahedron(), [Vector2.Zero])]);

    private static IndexedMesh Tetrahedron() =>
        new(
            [new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(0, 10, 0), new Vector3(0, 0, 10)],
            [
                new IndexedTriangle(0, 2, 1),
                new IndexedTriangle(0, 1, 3),
                new IndexedTriangle(0, 3, 2),
                new IndexedTriangle(1, 2, 3),
            ]);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(9, 3)]
    [InlineData(10, 4)]
    [InlineData(23, 5)]
    public void Plates_are_laid_out_in_as_many_columns_as_the_slicers_use(int plates, int columns) =>
        SlicerProject.Columns(plates).Should().Be(columns);

    /// <summary>A fifth of a bed between plates, and the second row in front of the first.</summary>
    [Fact]
    public void Each_plate_sits_where_the_slicer_puts_it()
    {
        var project = A1();

        project.PlateOrigin(0, 6).Should().Be(Vector2.Zero);
        project.PlateOrigin(2, 6).X.Should().BeApproximately(2 * 256 * 1.2f, 0.01f);
        project.PlateOrigin(3, 6).Should().Be(new Vector2(0, -256 * 1.2f));
    }

    [Fact]
    public void A_plain_3mf_is_not_a_project() =>
        SlicerProject.For(ProjectFormat.Plain, "A1", PrintBeds.A1).Should().BeNull();

    [Fact]
    public void A_printer_with_no_presets_to_name_gets_a_plain_file() =>
        SlicerProject.For(ProjectFormat.BambuStudio, "300x300", PrintBeds.Parse("300x300")).Should().BeNull();

    /// <summary>
    /// A plate size of another machine's bed means another machine, so naming the chosen printer's
    /// presets would be a guess - and a bigger bed would spill off the slicer's plates besides.
    /// </summary>
    [Theory]
    [InlineData("300x300")]
    [InlineData("220x220")]
    public void A_plate_size_for_another_machine_gets_a_plain_file(string size) =>
        SlicerProject.For(ProjectFormat.BambuStudio, "A1", PrintBeds.Parse(size)).Should().BeNull();

    /// <summary>The command from #12: the A1's own bed typed out as a plate size.</summary>
    [Fact]
    public void A_plate_size_the_same_as_the_printers_bed_is_still_that_printer() =>
        SlicerProject.For(ProjectFormat.BambuStudio, "A1", PrintBeds.Parse("256x256"))!
            .Bed.Should().Be(PrintBeds.A1);

    /// <summary>One filament per colour, however many plates the colour fills.</summary>
    [Fact]
    public void Plates_of_the_same_colour_share_a_filament()
    {
        var (filaments, chosen) = A1().Filaments([Plate("Black", Black), Plate("Black", Black), Plate("Red", Red)]);

        filaments.Should().Equal(Black, Red);
        chosen.Should().Equal(0, 0, 1);
    }

    /// <summary>Past the slicer's limit a colour shares the filament of its nearest neighbour.</summary>
    [Fact]
    public void Colours_past_the_limit_take_the_nearest_filament()
    {
        var project = A1(ProjectFormat.OrcaSlicer);
        var plates = Enumerable.Range(0, project.FilamentLimit - 1)
            .Select(i => Plate($"Grey {i}", new Rgb24((byte)(i * 8), (byte)(i * 8), (byte)(i * 8))))
            .Append(Plate("Red", Red))
            .Append(Plate("Nearly red", NearlyRed))
            .ToList();

        var (filaments, chosen) = project.Filaments(plates);

        filaments.Should().HaveCount(16);
        chosen[^1].Should().Be(chosen[^2], "it is nearer red than any grey");
    }

    [Fact]
    public void Plates_are_named_after_their_colour_and_numbered_when_it_fills_several()
    {
        var plates = new[] { Plate("Nero", Black), Plate("Nero", Black), Plate("Rosso", Red) };
        var objects = new[]
        {
            new ProjectObject(3, "3001", 0, 2),
            new ProjectObject(4, "3001", 1, 1),
            new ProjectObject(5, "3001", 2, 1),
        };

        var config = XDocument.Parse(A1().ModelSettings(plates, objects, [0, 0, 1]));

        config.Root!.Elements("plate")
            .Select(p => Value(p, "plater_name"))
            .Should().Equal("Nero 1", "Nero 2", "Rosso");

        config.Root.Elements("object")
            .Select(o => Value(o, "extruder"))
            .Should().Equal("1", "1", "2");

        config.Root.Elements("plate").First().Elements("model_instance")
            .Should().HaveCount(2, "a plate lists every copy on it");
    }

    /// <summary>
    /// The presets are named and nothing about them is stated, so the slicer takes each one as
    /// the user's own installation has it.
    /// </summary>
    [Fact]
    public void The_project_names_the_printers_own_presets()
    {
        using var settings = JsonDocument.Parse(A1().ProjectSettings([Black, Red]));
        var root = settings.RootElement;

        root.GetProperty("printer_settings_id").GetString().Should().Be("Bambu Lab A1 0.4 nozzle");
        root.GetProperty("print_settings_id").GetString().Should().Be("0.16mm Optimal @BBL A1");
        root.GetProperty("filament_settings_id").EnumerateArray().Select(e => e.GetString())
            .Should().Equal("Bambu PLA Basic @BBL A1", "Bambu PLA Basic @BBL A1");
        root.GetProperty("filament_colour").EnumerateArray().Select(e => e.GetString())
            .Should().Equal("#05131D", "#C91A09");
        root.GetProperty("different_settings_to_system").GetArrayLength().Should().Be(4);
    }

    /// <summary>
    /// Bambu Studio cuts every filament list to the length of the diameters, preset names
    /// included, so without one diameter per filament only the first keeps its preset.
    /// </summary>
    [Fact]
    public void Every_filament_has_a_diameter_so_none_loses_its_preset()
    {
        using var settings = JsonDocument.Parse(A1().ProjectSettings([Black, Red, NearlyRed]));

        settings.RootElement.GetProperty("filament_diameter").GetArrayLength().Should().Be(3);
    }

    /// <summary>OrcaSlicer's P1S has no PLA Basic of its own and uses the X1C's.</summary>
    [Fact]
    public void OrcaSlicer_names_its_own_P1S_filament()
    {
        var bambu = SlicerProject.For(ProjectFormat.BambuStudio, "P1S", PrintBeds.P1S)!;
        var orca = SlicerProject.For(ProjectFormat.OrcaSlicer, "P1S", PrintBeds.P1S)!;

        bambu.ProjectSettings([Black]).Should().Contain("Bambu PLA Basic @BBL P1S 0.4 nozzle");
        orca.ProjectSettings([Black]).Should().Contain("Bambu PLA Basic @BBL X1C");
        orca.ModelMetadata().Should().Contain(("OrcaSlicer", "2.3.2"));
    }

    private static string? Value(XElement parent, string key) =>
        parent.Elements("metadata").FirstOrDefault(m => (string?)m.Attribute("key") == key)?
            .Attribute("value")?.Value;
}

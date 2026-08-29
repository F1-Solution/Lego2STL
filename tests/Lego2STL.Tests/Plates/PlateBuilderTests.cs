using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Text;

namespace Lego2STL.Tests.Plates;

/// <summary>
/// What the plate stage does when the shapes it is handed do not cover the whole list.
/// </summary>
/// <remarks>
/// A real set brings this on regularly: a handful of part numbers have no shape file in the
/// library, and the run used to answer by writing no plates at all. The pieces that did come
/// out are printable, so the plates are worth having; what the run must not do is let them
/// pass for the complete set, which is why the count is reported and the report names them.
/// </remarks>
public sealed class PlateBuilderTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "lego2stl-plates-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>A tetrahedron: the smallest closed shape, so a valid mesh to place.</summary>
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

    private static PartsList ListOf(params (string PartNumber, int Quantity)[] parts) =>
        new(
            [.. parts.Select((p, i) => new PartEntry(
                i + 1, p.PartNumber, 11, "Black", Rgb24.Parse("#05131D"), p.Quantity))],
            []);

    [Fact]
    public async Task A_part_with_no_shape_does_not_stop_the_others_being_plated()
    {
        var list = ListOf(("3005", 2), ("64870", 3));
        var shapes = new Dictionary<string, IndexedMesh>(StringComparer.OrdinalIgnoreCase)
        {
            ["3005"] = Tetrahedron(),
        };

        var result = await PlateBuilder.WriteAsync(list, shapes, _directory);

        result.Plates.Should().NotBeEmpty();
        result.PieceCount.Should().Be(2, "only the part that produced a shape can be placed");
        Directory.GetFiles(_directory, "*.3mf").Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_colour_whose_parts_all_failed_produces_no_plate_of_its_own()
    {
        var list = ListOf(("64870", 3));

        var result = await PlateBuilder.WriteAsync(
            list, new Dictionary<string, IndexedMesh>(StringComparer.OrdinalIgnoreCase), _directory);

        result.Plates.Should().BeEmpty();
        result.PieceCount.Should().Be(0);
    }

    /// <summary>
    /// The plate is named after its colour, so the name has to be the one the run is speaking.
    /// A folder of English file names beside an Italian parts list is the same file twice as
    /// far as anyone reading it is concerned.
    /// </summary>
    [Fact]
    public async Task A_plate_is_named_after_its_colour_in_the_chosen_language()
    {
        var list = ListOf(("3005", 1));
        var shapes = new Dictionary<string, IndexedMesh>(StringComparer.OrdinalIgnoreCase)
        {
            ["3005"] = Tetrahedron(),
        };

        var result = await PlateBuilder.WriteAsync(
            list, shapes, _directory, language: DisplayLanguage.Italian);

        var plate = result.Plates.Should().ContainSingle().Subject;

        plate.FileName.Should().Be("nero.3mf");
        plate.ColorName.Should().Be("Nero");
        plate.BrickLinkColorCode.Should().Be(11);
        File.Exists(Path.Combine(_directory, "nero.3mf")).Should().BeTrue();
    }

    [Fact]
    public async Task Every_copy_asked_for_reaches_a_plate()
    {
        var list = ListOf(("3005", 7), ("64870", 3));
        var shapes = new Dictionary<string, IndexedMesh>(StringComparer.OrdinalIgnoreCase)
        {
            ["3005"] = Tetrahedron(),
        };

        var result = await PlateBuilder.WriteAsync(list, shapes, _directory);

        result.PieceCount.Should().Be(7);
    }

    /// <summary>
    /// A part no plate can take is recorded with its measurements, not just described.
    /// </summary>
    /// <remarks>
    /// It used to be kept as a finished sentence, which is why nothing but the report could say
    /// anything about it - the catalogue could not offer a smaller scale because it did not
    /// know by how much the part missed.
    /// </remarks>
    [Fact]
    public async Task A_part_too_big_for_the_bed_is_recorded_with_its_size()
    {
        var list = new PartsList(
            [new PartEntry(1, "huge", 11, "Black", Rgb24.Parse("#05131D"), 1)], []);

        // 400 mm across, against a bed 256 mm wide.
        var shapes = new Dictionary<string, IndexedMesh>(StringComparer.OrdinalIgnoreCase)
        {
            ["huge"] = ABoxOf(400f, 10f, 10f),
        };

        var built = await PlateBuilder.WriteAsync(list, shapes, _directory);

        var skipped = built.Skipped.Should().ContainSingle().Subject;

        skipped.PartNumber.Should().Be("huge");
        skipped.Width.Should().BeApproximately(400f, 0.1f);
        skipped.TooTall.Should().BeFalse("it is wide, not tall");
    }

    private static IndexedMesh ABoxOf(float x, float y, float z) =>
        VertexWelder.Weld(
        [
            new Triangle(new Vector3(0, 0, 0), new Vector3(x, 0, 0), new Vector3(x, y, 0)),
            new Triangle(new Vector3(0, 0, 0), new Vector3(x, y, 0), new Vector3(0, y, 0)),
            new Triangle(new Vector3(0, 0, z), new Vector3(x, y, z), new Vector3(x, 0, z)),
            new Triangle(new Vector3(0, 0, z), new Vector3(0, y, z), new Vector3(x, y, z)),
        ]);
}

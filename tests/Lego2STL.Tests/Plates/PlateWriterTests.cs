using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.Plates;

namespace Lego2STL.Tests.Plates;

/// <summary>
/// Arranging named shapes onto plate files, with no parts list involved.
/// </summary>
/// <remarks>
/// This is the half of the plate stage that a calibration needs and a parts list does not. A
/// calibration plate carries the same part six times at six clearances, which a dictionary keyed
/// by part number cannot express, so the packing had to become reachable on its own.
/// </remarks>
public sealed class PlateWriterTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "lego2stl-platewriter-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>A tetrahedron: the smallest closed shape, so a valid mesh to place.</summary>
    private static IndexedMesh Tetrahedron(float size = 10f) =>
        new(
            [
                new Vector3(0, 0, 0),
                new Vector3(size, 0, 0),
                new Vector3(0, size, 0),
                new Vector3(0, 0, size),
            ],
            [
                new IndexedTriangle(0, 2, 1),
                new IndexedTriangle(0, 1, 3),
                new IndexedTriangle(0, 3, 2),
                new IndexedTriangle(1, 2, 3),
            ]);

    private static Rgb24 Grey => Rgb24.Parse("#C8C8C8");

    /// <summary>
    /// The labels are not part numbers, and nothing minds.
    /// </summary>
    /// <remarks>
    /// The point of the whole extraction. Three labels naming the same part at three clearances
    /// are three distinct things on the plate, which is exactly what a dictionary keyed by part
    /// number could not say.
    /// </remarks>
    [Fact]
    public async Task Labels_that_are_not_part_numbers_are_each_placed()
    {
        var items = new List<PlateItem>
        {
            new("3705-0.00mm", Tetrahedron(), 1),
            new("3705-0.05mm", Tetrahedron(), 1),
            new("3705-0.10mm", Tetrahedron(), 1),
        };

        var result = await PlateWriter.WritePlatesAsync(
            items, "calibration", "Calibration", Grey, _directory);

        result.Skipped.Should().BeEmpty();
        result.Plates.Should().ContainSingle();
        result.Plates[0].FileName.Should().Be("calibration.3mf");
        result.Plates[0].PieceCount.Should().Be(3);
        File.Exists(Path.Combine(_directory, "calibration.3mf")).Should().BeTrue();
    }

    /// <summary>A quantity puts that many copies on, as the parts-list layer has always relied on.</summary>
    [Fact]
    public async Task A_quantity_puts_that_many_copies_on()
    {
        var result = await PlateWriter.WritePlatesAsync(
            [new PlateItem("pin", Tetrahedron(), 7)], "one", "Black", Grey, _directory);

        result.Plates.Should().ContainSingle();
        result.Plates[0].PieceCount.Should().Be(7);
    }

    /// <summary>Something no bed can take is reported rather than dropped in silence.</summary>
    [Fact]
    public async Task A_shape_too_big_for_the_bed_is_reported()
    {
        var result = await PlateWriter.WritePlatesAsync(
            [new PlateItem("enormous", Tetrahedron(4000f), 1)], "one", "Black", Grey, _directory);

        result.Plates.Should().BeEmpty();
        result.Skipped.Should().ContainSingle(s => s.PartNumber == "enormous");
    }

    /// <summary>More than one plate's worth is numbered, because the file name has to differ.</summary>
    [Fact]
    public async Task More_than_one_plates_worth_is_numbered()
    {
        var many = Enumerable.Range(0, 900)
            .Select(i => new PlateItem($"item-{i:000}", Tetrahedron(), 1))
            .ToList();

        var result = await PlateWriter.WritePlatesAsync(
            many, "calibration", "Calibration", Grey, _directory);

        result.Plates.Count.Should().BeGreaterThan(1);
        result.Plates[0].FileName.Should().Be("calibration-1.3mf");
    }
}

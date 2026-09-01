using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.LDraw;
using Lego2STL.Core.Plates;

namespace Lego2STL.Tests.Plates;

/// <summary>
/// What goes on a calibration plate.
/// </summary>
/// <remarks>
/// Three mating pairs at every clearance, because a fit is a property of two parts and not of
/// one, and one wide plate printed once. The wide plate is not part of the matrix: it tests
/// warping, which no clearance value changes, and printing it six times would spend bed and
/// filament varying something along an axis that does not affect it.
/// </remarks>
public sealed class CalibrationSetTests
{
    private static PartMesh ABlockCalled(string number)
    {
        var t = new List<Triangle>
        {
            new(new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(0, 10, 0)),
            new(new Vector3(0, 0, 0), new Vector3(0, 10, 0), new Vector3(0, 0, 10)),
            new(new Vector3(0, 0, 0), new Vector3(0, 0, 10), new Vector3(10, 0, 0)),
            new(new Vector3(10, 0, 0), new Vector3(0, 0, 10), new Vector3(0, 10, 0)),
        };

        return new PartMesh(number, number, t, null, 1, []);
    }

    private static Dictionary<string, PartMesh> AllOfThem() =>
        CalibrationSet.PartNumbers.ToDictionary(n => n, ABlockCalled, StringComparer.OrdinalIgnoreCase);

    private static readonly double[] SixSteps = [0.00, 0.05, 0.10, 0.15, 0.20, 0.25];

    /// <summary>Six pieces at six clearances, and the witness once.</summary>
    [Fact]
    public void The_matrix_is_every_pair_at_every_step_and_the_witness_once()
    {
        var (items, missing) = CalibrationSet.Items(AllOfThem(), SixSteps, new MeshPipelineOptions());

        missing.Should().BeEmpty();

        // Two bricks 2x2 make the stud pair, so six pieces per step, not five.
        items.Where(i => i.Label != CalibrationSet.WitnessLabel).Should().HaveCount(6 * 6);
        items.Should().ContainSingle(i => i.Label == CalibrationSet.WitnessLabel);
    }

    /// <summary>Every label says its clearance, because that is what the sheet maps.</summary>
    [Fact]
    public void Every_label_carries_the_clearance_it_was_built_at()
    {
        var (items, _) = CalibrationSet.Items(AllOfThem(), SixSteps, new MeshPipelineOptions());

        items.Should().Contain(i => i.Label == "3705-0.15mm");
        items.Should().Contain(i => i.Label == "3673-0.25mm");
    }

    /// <summary>
    /// A part the library has not got is left off and named, rather than stopping the plate.
    /// </summary>
    /// <remarks>
    /// The old behaviour was to abort the whole command, which is right when the output is that
    /// one part and wrong for a plate whose value is mostly still there. Two of three fits are
    /// still worth printing.
    /// </remarks>
    [Fact]
    public void A_part_the_library_has_not_got_is_left_off_and_named()
    {
        var sources = AllOfThem();
        sources.Remove("3673");

        var (items, missing) = CalibrationSet.Items(sources, SixSteps, new MeshPipelineOptions());

        missing.Should().Contain("3673");
        items.Should().NotBeEmpty();
        items.Should().NotContain(i => i.Label.StartsWith("3673", StringComparison.Ordinal));
    }

    /// <summary>The witness is built once, at no clearance, whatever the steps are.</summary>
    [Fact]
    public void The_witness_is_built_at_no_clearance()
    {
        var (items, _) = CalibrationSet.Items(AllOfThem(), SixSteps, new MeshPipelineOptions());

        CalibrationSet.WitnessLabel.Should().EndWith("0.00mm");
        items.Should().ContainSingle(i => i.Label == CalibrationSet.WitnessLabel);
    }
}

using FluentAssertions;
using Lego2STL.Core.Plates;

namespace Lego2STL.Tests.Plates;

/// <summary>
/// Writing a bed size down, and reading it back.
/// </summary>
/// <remarks>
/// The window shows a printer's bed as the grey text in the box that takes one, so the way a bed
/// is written has to be a way <c>--plate-size</c> accepts. A suggestion the tool would refuse is
/// worse than no suggestion.
/// </remarks>
public sealed class PrintBedTests
{
    [Theory]
    [InlineData("A1", "256x256")]
    [InlineData("A1mini", "180x180")]
    [InlineData("X1C", "256x256")]
    [InlineData("H2D", "350x320x325")]
    public void A_bed_is_written_the_way_the_option_takes_it(string printer, string expected)
    {
        PrintBeds.TryGetByName(printer, out var bed).Should().BeTrue();

        bed.AsSize.Should().Be(expected);
    }

    /// <summary>The height is only said when it is not already implied by the width.</summary>
    [Fact]
    public void A_bed_as_tall_as_it_is_wide_is_written_with_two_numbers()
    {
        new PrintBed("square", 220f, 220f, 220f).AsSize.Should().Be("220x220");
        new PrintBed("tall", 220f, 220f, 400f).AsSize.Should().Be("220x220x400");
    }

    [Theory]
    [InlineData("A1")]
    [InlineData("A1mini")]
    [InlineData("P1P")]
    [InlineData("P1S")]
    [InlineData("X1C")]
    [InlineData("H2D")]
    public void Every_bed_written_down_is_read_back_as_the_same_bed(string printer)
    {
        PrintBeds.TryGetByName(printer, out var bed).Should().BeTrue();

        PrintBeds.TryParseSize(bed.AsSize, out var read).Should().BeTrue(
            "the tool has to accept the size it just suggested");

        read.Width.Should().Be(bed.Width);
        read.Depth.Should().Be(bed.Depth);
        read.Height.Should().Be(bed.Height);
    }
}

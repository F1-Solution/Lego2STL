using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Plates;
using Xunit;

namespace Lego2STL.Tests.Plates;

/// <summary>
/// The largest scale at which everything still fits the plate.
/// </summary>
/// <remarks>
/// The point of the number is that acting on it works, so the tests check that the parts fit
/// when it is applied rather than comparing against a figure written down here. A suggestion
/// the packer would then reject is worse than no suggestion.
/// </remarks>
public sealed class FittingScaleTests
{
    private static readonly PrintBed A1 = PrintBeds.A1;

    [Fact]
    public void Nothing_is_suggested_when_everything_already_fits()
    {
        var items = new[] { new PackableItem("small", new Vector2(40, 40), 20) };

        FittingScale.Largest(items, A1, margin: 5f, scaleUsed: 100).Should().BeNull();
    }

    /// <summary>The measured case: a part 304 mm across at 200%, on a 256 mm bed.</summary>
    [Fact]
    public void A_part_wider_than_the_bed_brings_the_whole_set_down()
    {
        var items = new[]
        {
            new PackableItem("46891", new Vector2(304f, 184.8f), 192.2f),
            new PackableItem("small", new Vector2(40, 40), 20),
        };

        var suggested = FittingScale.Largest(items, A1, margin: 5f, scaleUsed: 200);

        suggested.Should().NotBeNull();
        suggested.Should().BeLessThan(200);

        // What matters is that applying it works.
        var factor = (float)(suggested!.Value / 200);
        var shrunk = items.Select(i => i with
        {
            Footprint = i.Footprint * factor,
            Height = i.Height * factor,
        });

        ShelfPacker.Pack(shrunk.ToList(), new PackingOptions { Bed = A1, Margin = 5f })
            .Oversized.Should().BeEmpty("the suggestion has to be one the packer accepts");
    }

    [Fact]
    public void A_part_too_tall_counts_as_much_as_one_too_wide()
    {
        var items = new[] { new PackableItem("tower", new Vector2(20, 20), 500f) };

        var suggested = FittingScale.Largest(items, A1, margin: 5f, scaleUsed: 100);

        suggested.Should().NotBeNull().And.BeLessThan(100);
    }

    /// <summary>Rounded down, because a suggestion that overshoots is a suggestion that fails.</summary>
    [Fact]
    public void The_answer_is_a_whole_percent_and_never_rounds_up()
    {
        var items = new[] { new PackableItem("odd", new Vector2(333.3f, 20), 20) };

        var suggested = FittingScale.Largest(items, A1, margin: 5f, scaleUsed: 100);

        suggested.Should().Be(Math.Floor(suggested!.Value));
    }
}

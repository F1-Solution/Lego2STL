using FluentAssertions;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Rebrickable;

namespace Lego2STL.Tests.Catalogue;

/// <summary>
/// Which parts a printer is asked to make.
/// </summary>
/// <remarks>
/// Measured on run 6324712: three pneumatic hoses are rubber and fail loudly, while a Powered Up
/// hub and two motors are plastic, succeed, and are printed as hollow shells of things that have
/// to be bought. Material alone cannot tell the second group apart - every one of the dump's 615
/// electronic parts is plastic - which is why the kind is consulted as well.
/// </remarks>
public sealed class PrintabilityTests
{
    [Theory]
    [InlineData("Tubes and Hoses", "Rubber", Printable.NotItsMaterial)]
    [InlineData("Gear Parts", "Cardboard/Paper", Printable.NotItsMaterial)]
    [InlineData("Minifig Upper Body", "Cloth", Printable.NotItsMaterial)]
    [InlineData("Technic Special", "Foam", Printable.NotItsMaterial)]
    [InlineData("Tubes and Hoses", "Flexible Plastic", Printable.NotItsMaterial)]
    [InlineData("Technic Special", "Metal", Printable.NotItsMaterial)]
    [InlineData("Electronics", "Plastic", Printable.NotItsKind)]
    [InlineData("Stickers", "Plastic", Printable.NotItsKind)]
    [InlineData("Technic Beams", "Plastic", Printable.Yes)]
    [InlineData("Bricks", "Plastic", Printable.Yes)]
    public void The_kind_and_the_material_together_decide(string category, string material, Printable expected)
    {
        Printability.Of(new PartFact(category, material)).Should().Be(expected);
    }

    /// <summary>
    /// A rubber tyre is reported as rubber rather than as a wheel, because the material is the
    /// more specific answer and the one a person can act on.
    /// </summary>
    [Fact]
    public void The_material_is_answered_before_the_kind()
    {
        Printability.Of(new PartFact("Electronics", "Rubber")).Should().Be(Printable.NotItsMaterial);
    }

    [Fact]
    public void A_part_the_dump_does_not_know_is_unknown_and_still_printed()
    {
        var verdict = Printability.Of(null);

        verdict.Should().Be(Printable.Unknown);
        verdict.IsPrinted().Should().BeTrue("an absence is never a reason to leave a part out");
    }

    [Fact]
    public void Only_the_two_refusals_stop_a_part_being_printed()
    {
        Printable.Yes.IsPrinted().Should().BeTrue();
        Printable.NotItsMaterial.IsPrinted().Should().BeFalse();
        Printable.NotItsKind.IsPrinted().Should().BeFalse();
    }

    /// <summary>The word the run's record keeps, which has to survive being written and read.</summary>
    [Theory]
    [InlineData(Printable.Yes, "yes")]
    [InlineData(Printable.NotItsMaterial, "material")]
    [InlineData(Printable.NotItsKind, "kind")]
    [InlineData(Printable.Unknown, "unknown")]
    public void Every_verdict_survives_a_trip_through_its_word(Printable verdict, string token)
    {
        verdict.Token().Should().Be(token);
        Printability.FromToken(token).Should().Be(verdict);
    }

    /// <summary>A record written before any of this says nothing, and nothing is what it means.</summary>
    [Fact]
    public void A_record_with_no_word_reads_as_unknown()
    {
        Printability.FromToken(null).Should().Be(Printable.Unknown);
        Printability.FromToken("something else entirely").Should().Be(Printable.Unknown);
    }

    private static readonly Dictionary<string, PartFact> AFewFacts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["5102c13"] = new("Tubes and Hoses", "Rubber"),
        ["22127"] = new("Electronics", "Plastic"),
        ["32523"] = new("Technic Beams", "Plastic"),
    };

    [Fact]
    public void A_hose_and_a_hub_are_left_out_and_a_beam_is_not()
    {
        var (build, leave) = Printability.Choose(
            ["5102c13", "22127", "32523", "99999"], AFewFacts, printEverything: false);

        build.Should().Equal("32523", "99999");
        leave.Should().Equal("5102c13", "22127");
    }

    /// <summary>The order of the list is kept, so a run reads the way its list does.</summary>
    [Fact]
    public void The_two_sides_keep_the_order_the_list_had()
    {
        var (build, _) = Printability.Choose(
            ["32523", "99999", "3705"], AFewFacts, printEverything: false);

        build.Should().Equal("32523", "99999", "3705");
    }

    [Fact]
    public void With_no_facts_at_all_every_part_is_still_built()
    {
        var (build, leave) = Printability.Choose(
            ["5102c13", "22127", "32523"], new Dictionary<string, PartFact>(), printEverything: false);

        build.Should().Equal("5102c13", "22127", "32523");
        leave.Should().BeEmpty();
    }

    /// <summary>Asking for everything asks for everything, whatever the database says.</summary>
    [Fact]
    public void Print_everything_builds_the_hose_and_the_hub_too()
    {
        var (build, leave) = Printability.Choose(
            ["5102c13", "22127", "32523"], AFewFacts, printEverything: true);

        build.Should().Equal("5102c13", "22127", "32523");
        leave.Should().BeEmpty();
    }
}

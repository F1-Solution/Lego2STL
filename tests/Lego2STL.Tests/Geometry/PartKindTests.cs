using FluentAssertions;
using Lego2STL.Core.Geometry;

namespace Lego2STL.Tests.Geometry;

/// <summary>
/// Reading what a part is out of the description LDraw gives it.
/// </summary>
/// <remarks>
/// Every title here is a real one, taken from the record of run 6324712. The awkward case is
/// Technic: two thirds of that run's parts begin with the word, so the kind is the second word,
/// and a reader that only looks at the first finds nothing at all for most of a real set.
/// </remarks>
public sealed class PartKindTests
{
    [Theory]
    [InlineData("Brick  2 x  4", PartKind.Brick)]
    [InlineData("Technic Brick  1 x  2 with Hole", PartKind.Brick)]
    [InlineData("Plate  6 x  8", PartKind.Plate)]
    [InlineData("Plate  2 x  2 with Holes", PartKind.Plate)]
    [InlineData("Tile  1 x  2 Grille with Bottom Groove", PartKind.Tile)]
    [InlineData("Technic Beam  3 x  0.5 Liftarm", PartKind.Beam)]
    [InlineData("Technic Beam 15", PartKind.Beam)]
    [InlineData("Technic Axle  4", PartKind.Axle)]
    [InlineData("Technic Axle 12", PartKind.Axle)]
    [InlineData("Technic Pin Long with Friction Ridges", PartKind.Pin)]
    [InlineData("Technic Pin Joiner Perpendicular", PartKind.Pin)]
    public void A_description_says_what_the_part_is(string title, PartKind expected) =>
        PartKinds.FromTitle(title).Should().Be(expected);

    /// <summary>
    /// An axle pin is a pin, because it is the pin end that decides how it lies.
    /// </summary>
    /// <remarks>
    /// It reads as both, and the order the reader tries its words in is what settles it. Written
    /// down as a test because the answer is a choice, not a fact, and the next person should find
    /// the choice rather than re-make it.
    /// </remarks>
    [Fact]
    public void An_axle_pin_is_a_pin() =>
        PartKinds.FromTitle("Technic Axle Pin  3L with Friction").Should().Be(PartKind.Pin);

    [Theory]
    [InlineData("Technic Cross Block  1 x  3")]
    [InlineData("Technic Gear 20 Tooth")]
    [InlineData("Technic Panel  5 x 11")]
    [InlineData("Technic Turntable 60 Tooth Bottom")]
    [InlineData("Slope Brick 45  2 x  2")]
    [InlineData("Bar  3L")]
    [InlineData("Bracket  1 x  2 -  2 x  2 Down")]
    [InlineData("Wheel Rim 16 x 31 with 6 Pegholes")]
    [InlineData("Electric Control+ L Motor")]
    public void A_kind_with_no_rule_is_not_guessed_at(string title) =>
        PartKinds.FromTitle(title).Should().Be(PartKind.Unknown);

    /// <summary>Two fifths of a real set has no rule, so nothing may depend on there being one.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("~Moved to 3023b")]
    public void Nothing_to_read_is_not_a_kind(string? title) =>
        PartKinds.FromTitle(title).Should().Be(PartKind.Unknown);
}

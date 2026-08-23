using FluentAssertions;
using Lego2STL.Core.Colors;

namespace Lego2STL.Tests.Colors;

public sealed class Rgb24Tests
{
    [Theory]
    [InlineData("#05131D", 0x05, 0x13, 0x1D)]
    [InlineData("05131D", 0x05, 0x13, 0x1D)]
    [InlineData("  #ffffff  ", 0xFF, 0xFF, 0xFF)]
    [InlineData("c91a09", 0xC9, 0x1A, 0x09)]
    public void Parse_accepts_hex_with_or_without_hash(string input, byte r, byte g, byte b) =>
        Rgb24.Parse(input).Should().Be(new Rgb24(r, g, b));

    [Theory]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#GGGGGG")]
    [InlineData(null)]
    public void TryParse_rejects_anything_that_is_not_six_hex_digits(string? input) =>
        Rgb24.TryParse(input, out _).Should().BeFalse();

    [Fact]
    public void ToString_round_trips_through_Parse()
    {
        var original = new Rgb24(0xA0, 0xA5, 0xA9);
        Rgb24.Parse(original.ToString()).Should().Be(original);
    }

    [Fact]
    public void DeltaE_of_a_colour_against_itself_is_zero() =>
        new Rgb24(0x05, 0x13, 0x1D).DeltaE(new Rgb24(0x05, 0x13, 0x1D)).Should().Be(0);

    [Fact]
    public void DeltaE_is_symmetric()
    {
        var a = new Rgb24(0xC9, 0x1A, 0x09);
        var b = new Rgb24(0xD3, 0x0F, 0x01);
        a.DeltaE(b).Should().BeApproximately(b.DeltaE(a), 1e-9);
    }

    /// <summary>
    /// Guards the decision to use Rebrickable's RGB values, which was made by measuring the
    /// real PDF rather than by preference. Black in the instruction renders sampled as
    /// #010713; if a future change made LDraw's or BrickLink's palette look closer, the
    /// premise of that decision would have shifted and this test should fail loudly.
    /// </summary>
    [Fact]
    public void Rebrickable_black_is_closer_to_the_measured_PDF_pixels_than_LDraw_or_BrickLink()
    {
        var sampledFromPdf = Rgb24.Parse("#010713");

        var rebrickable = sampledFromPdf.DeltaE(Rgb24.Parse("#05131D"));
        var ldraw = sampledFromPdf.DeltaE(Rgb24.Parse("#1B2A34"));
        var brickLink = sampledFromPdf.DeltaE(Rgb24.Parse("#2E2E2E"));

        rebrickable.Should().BeLessThan(ldraw);
        rebrickable.Should().BeLessThan(brickLink);
    }

    /// <summary>Same measurement, for red: the PDF's red sampled as #D30F01.</summary>
    [Fact]
    public void Rebrickable_red_is_closer_to_the_measured_PDF_pixels_than_LDraw()
    {
        var sampledFromPdf = Rgb24.Parse("#D30F01");

        sampledFromPdf.DeltaE(Rgb24.Parse("#C91A09"))
            .Should().BeLessThan(sampledFromPdf.DeltaE(Rgb24.Parse("#B40000")));
    }

    /// <summary>
    /// Shading spread measured on page 4 of the reference PDF: pixels belonging to one part
    /// sit a median 3.9 (black) to 6.4 (red) from the reference colour, and the 90th
    /// percentile reaches 17.5 to 20.3. This is the noise floor any pixel cross-check has
    /// to clear.
    /// </summary>
    private const double MeasuredShadingSpreadP90 = 17.5;

    /// <summary>
    /// The honest limit of the pixel cross-check, stated as a relationship rather than a
    /// magic number: Light Gray and Light Bluish Gray are 4.8 apart, which is well inside
    /// the spread that shading alone produces, so a sampled pixel cannot tell them apart
    /// and the cross-check must abstain instead of choosing.
    /// </summary>
    [Fact]
    public void Near_neighbour_greys_are_closer_together_than_shading_noise_so_pixels_cannot_separate_them()
    {
        var separation = Rgb24.Parse("#9BA19D").DeltaE(Rgb24.Parse("#A0A5A9"));

        separation.Should().BeLessThan(MeasuredShadingSpreadP90,
            "Light Gray vs Light Bluish Gray is unresolvable from a shaded render");
    }

    /// <summary>
    /// The other half of the same story: the cross-check is genuinely useful where the
    /// colours are far apart. Black vs Dark Bluish Gray clears the shading floor by more
    /// than a factor of two, so that confusion really can be caught.
    /// </summary>
    [Fact]
    public void Black_and_dark_bluish_gray_are_far_enough_apart_for_pixels_to_separate_them()
    {
        var separation = Rgb24.Parse("#05131D").DeltaE(Rgb24.Parse("#6C6E68"));

        separation.Should().BeGreaterThan(2 * MeasuredShadingSpreadP90);
    }

    [Fact]
    public void Lab_of_pure_white_is_lightness_one_hundred()
    {
        var (l, a, b) = new Rgb24(255, 255, 255).ToLab();

        l.Should().BeApproximately(100.0, 0.01);
        a.Should().BeApproximately(0.0, 0.01);
        b.Should().BeApproximately(0.0, 0.01);
    }

    [Fact]
    public void Lab_of_pure_black_is_lightness_zero() =>
        new Rgb24(0, 0, 0).ToLab().L.Should().BeApproximately(0.0, 0.01);
}

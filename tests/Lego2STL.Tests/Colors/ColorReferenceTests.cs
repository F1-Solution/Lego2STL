using FluentAssertions;
using Lego2STL.Core.Colors;

namespace Lego2STL.Tests.Colors;

/// <summary>
/// Checks the table that actually ships in the assembly, so a bad regeneration is caught
/// rather than shipped.
/// </summary>
public sealed class ColorReferenceTests
{
    [Fact]
    public void Embedded_table_loads() =>
        ColorReference.Table.Count.Should().BeGreaterThan(200);

    /// <summary>
    /// The eight BrickLink codes that appear on pages 2-5 of the reference PDF, with the
    /// colours read off the page by eye. This is the assertion that the whole colour layer
    /// exists to satisfy.
    /// </summary>
    [Theory]
    [InlineData(11, "Black")]
    [InlineData(5, "Red")]
    [InlineData(7, "Blue")]
    [InlineData(2, "Tan")]
    [InlineData(8, "Brown")]
    [InlineData(9, "Light Gray")]
    [InlineData(85, "Dark Bluish Gray")]
    [InlineData(86, "Light Bluish Gray")]
    public void BrickLink_codes_from_the_reference_PDF_resolve_to_the_expected_colour(int code, string expectedName) =>
        ColorReference.Table.Get(ColorScheme.BrickLink, code).Name.Should().Be(expectedName);

    /// <summary>
    /// The distinction that proves the PDF uses BrickLink numbering rather than
    /// Rebrickable's: the same number means two entirely different colours.
    /// </summary>
    [Fact]
    public void Code_eleven_means_black_in_BrickLink_but_light_turquoise_in_Rebrickable()
    {
        var table = ColorReference.Table;

        table.Get(ColorScheme.BrickLink, 11).Name.Should().Be("Black");
        table.Get(ColorScheme.Rebrickable, 11).Name.Should().Be("Light Turquoise");
    }

    [Fact]
    public void Black_carries_the_expected_cross_references()
    {
        var black = ColorReference.Table.Get(ColorScheme.BrickLink, 11);

        black.RebrickableId.Should().Be(0);
        black.BrickLinkId.Should().Be(11);
        black.LegoId.Should().Be(26);
        black.LDrawId.Should().Be(0);
        black.Rgb.Should().Be(Rgb24.Parse("#05131D"));
        black.IsTranslucent.Should().BeFalse();
    }

    /// <summary>
    /// Alias codes have to resolve too. Black carries LEGO [26, 342] and LDraw [0, 256],
    /// where 342 is "Conduct. Black" and 256 is "Rubber_Black". Storing only the primary
    /// left 12 LEGO and 19 LDraw codes unmappable, which is why the reverse map is explicit.
    /// </summary>
    [Theory]
    [InlineData(ColorScheme.Lego, 26)]
    [InlineData(ColorScheme.Lego, 342)]
    [InlineData(ColorScheme.LDraw, 0)]
    [InlineData(ColorScheme.LDraw, 256)]
    public void Alias_codes_resolve_as_well_as_primary_codes(ColorScheme scheme, int code) =>
        ColorReference.Table.Get(scheme, code).Name.Should().Be("Black");

    /// <summary>
    /// The "[Unknown]" sentinel claims 17 LDraw codes including the meta-colours 16
    /// ("inherit from parent") and 24 ("edge colour"). Those are not real colours and must
    /// never come back from a lookup.
    /// </summary>
    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    public void LDraw_meta_colours_do_not_resolve(int metaCode) =>
        ColorReference.Table.TryGet(ColorScheme.LDraw, metaCode, out _).Should().BeFalse();

    [Fact]
    public void Unknown_sentinel_is_never_the_target_of_an_external_lookup()
    {
        var table = ColorReference.Table;

        foreach (var scheme in new[] { ColorScheme.BrickLink, ColorScheme.Lego, ColorScheme.LDraw })
        {
            foreach (var code in Enumerable.Range(-5, 1200))
            {
                if (table.TryGet(scheme, code, out var color))
                {
                    color.IsUnknown.Should().BeFalse(
                        $"{scheme} {code} must not resolve to the [Unknown] sentinel");
                }
            }
        }
    }

    /// <summary>The contested codes, resolved as recorded when the table was generated.</summary>
    [Theory]
    [InlineData(ColorScheme.BrickLink, 77, "Pearl Dark Gray")]
    [InlineData(ColorScheme.BrickLink, 72, "Maersk Blue")]
    [InlineData(ColorScheme.LDraw, 112, "Medium Bluish Violet")]
    [InlineData(ColorScheme.LDraw, 216, "Rust")]
    public void Contested_codes_resolve_the_way_the_generator_decided(
        ColorScheme scheme, int code, string expectedName) =>
        ColorReference.Table.Get(scheme, code).Name.Should().Be(expectedName);

    [Fact]
    public void An_unmappable_code_is_reported_rather_than_guessed()
    {
        var act = () => ColorReference.Table.Get(ColorScheme.BrickLink, 99999);

        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("*BrickLink code 99999*");
    }

    [Fact]
    public void Every_scheme_resolves_a_plausible_number_of_codes()
    {
        var table = ColorReference.Table;

        table.CountIn(ColorScheme.Rebrickable).Should().Be(table.Count);
        table.CountIn(ColorScheme.BrickLink).Should().BeGreaterThan(200);
        table.CountIn(ColorScheme.Lego).Should().BeGreaterThan(180);
        table.CountIn(ColorScheme.LDraw).Should().BeGreaterThan(160);
    }

    [Fact]
    public void Nearest_match_ranking_puts_black_first_for_a_pixel_sampled_from_the_PDF()
    {
        var ranked = ColorReference.Table.RankByDistance(Rgb24.Parse("#010713"));

        ranked.Should().NotBeEmpty();
        ranked[0].Color.Name.Should().Be("Black");
        ranked[0].DeltaE.Should().BeLessThan(5.0);
    }

    [Fact]
    public void Nearest_match_ranking_excludes_translucent_colours() =>
        ColorReference.Table.RankByDistance(Rgb24.Parse("#FFFFFF"))
            .Should().OnlyContain(m => !m.Color.IsTranslucent);
}

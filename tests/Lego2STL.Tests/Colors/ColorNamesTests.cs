using FluentAssertions;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Text;

namespace Lego2STL.Tests.Colors;

/// <summary>
/// Guards the colour names the way <see cref="Text.StringsTests"/> guards the phrases.
/// </summary>
/// <remarks>
/// A colour name reaches further than a label: it is written into the parts list, and a plate
/// file is named after it. A colour missing from a language would therefore show up as one
/// English row in an Italian list and one English file name among Italian ones, which is the
/// kind of thing nobody notices until the files are already on a printer.
/// </remarks>
public sealed class ColorNamesTests
{
    public static TheoryData<DisplayLanguage> Languages()
    {
        var data = new TheoryData<DisplayLanguage>();
        foreach (var language in DisplayLanguages.All)
        {
            data.Add(language);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_colour_in_the_reference_has_a_name_in_every_language(DisplayLanguage language)
    {
        var missing = ColorReference.Table.Colors
            .Select(c => c.Name)
            .Where(name => !ColorNames.Knows(language, name))
            .ToList();

        missing.Should().BeEmpty(
            "every colour needs a {0} name; missing: {1}",
            language,
            string.Join(", ", missing));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void No_colour_name_is_blank(DisplayLanguage language)
    {
        foreach (var colour in ColorReference.Table.Colors)
        {
            ColorNames.For(language, colour.Name).Should().NotBeNullOrWhiteSpace(
                "{0} needs a real {1} name", colour.Name, language);
        }
    }

    /// <summary>
    /// A parts list written in one language is read back in another, so a translated name has
    /// to lead back to the one the tool stores.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public void A_translated_name_reads_back_as_the_stored_one(DisplayLanguage language)
    {
        foreach (var colour in ColorReference.Table.Colors)
        {
            ColorNames.ToCanonical(ColorNames.For(language, colour.Name))
                .Should().Be(colour.Name);
        }
    }

    /// <summary>
    /// Two colours sharing a wording would make reading one back a coin toss, so the tables
    /// have to keep them apart.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public void No_two_colours_share_a_name(DisplayLanguage language)
    {
        var shared = ColorReference.Table.Colors
            .Select(c => ColorNames.For(language, c.Name))
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        shared.Should().BeEmpty(
            "no two colours may share a {0} name; shared: {1}",
            language,
            string.Join(", ", shared));
    }

    [Fact]
    public void A_name_the_tables_do_not_know_is_left_as_it_was_written()
    {
        ColorNames.For(DisplayLanguage.Italian, "Somebody's Own Colour")
            .Should().Be("Somebody's Own Colour");

        ColorNames.ToCanonical("Somebody's Own Colour").Should().Be("Somebody's Own Colour");
    }

    [Fact]
    public void A_name_is_recognised_however_it_is_cased_and_spaced()
    {
        ColorNames.ToCanonical("  grigio bluastro scuro  ").Should().Be("Dark Bluish Gray");
        ColorNames.ToCanonical("dark bluish gray").Should().Be("Dark Bluish Gray");
    }
}

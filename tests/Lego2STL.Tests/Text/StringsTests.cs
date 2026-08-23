using System.Globalization;
using System.Text.RegularExpressions;
using FluentAssertions;
using Lego2STL.Core.Text;

namespace Lego2STL.Tests.Text;

/// <summary>
/// Guards the translation tables.
/// </summary>
/// <remarks>
/// A phrase that exists in one language and not the other is the classic way a translated
/// application ends up half English, and it is invisible until someone switches language and
/// reads the output. These tests make it a build failure instead: every key in every language,
/// and the same placeholders in each, so a sentence cannot lose the number it was quoting.
/// </remarks>
public sealed class StringsTests
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
    public void Every_phrase_exists_in_every_language(DisplayLanguage language)
    {
        var words = Strings.For(language);

        var missing = Enum.GetValues<TextKey>()
            .Where(key => !words.Keys.Contains(key))
            .ToList();

        missing.Should().BeEmpty(
            "every phrase needs a {0} wording; missing: {1}",
            language,
            string.Join(", ", missing));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void No_phrase_is_blank(DisplayLanguage language)
    {
        var words = Strings.For(language);

        foreach (var key in Enum.GetValues<TextKey>())
        {
            words[key].Should().NotBeNullOrWhiteSpace("{0} needs a real {1} wording", key, language);
        }
    }

    [Fact]
    public void Translations_keep_the_same_placeholders()
    {
        foreach (var key in Enum.GetValues<TextKey>())
        {
            var expected = Placeholders(Strings.English[key]);

            foreach (var language in DisplayLanguages.All)
            {
                Placeholders(Strings.For(language)[key]).Should().Equal(
                    expected,
                    "the {0} wording of {1} has to fill in the same values as the English one",
                    language,
                    key);
            }
        }
    }

    [Fact]
    public void Filling_in_a_phrase_does_not_depend_on_the_machines_locale()
    {
        // A decimal comma in a measurement or a file name causes more trouble than it solves,
        // so the words follow the language while the numbers stay invariant.
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("it-IT");

            Strings.Italian.Format(TextKey.ReportPrintingNoteWithClearance, 0.15)
                .Should().Contain("0.15");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("it", DisplayLanguage.Italian)]
    [InlineData("IT", DisplayLanguage.Italian)]
    [InlineData("it-CH", DisplayLanguage.Italian)]
    [InlineData("en", DisplayLanguage.English)]
    [InlineData("en-GB", DisplayLanguage.English)]
    public void A_language_tag_is_read_however_it_is_written(string tag, DisplayLanguage expected)
    {
        DisplayLanguages.TryParse(tag, out var language).Should().BeTrue();
        language.Should().Be(expected);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("")]
    [InlineData(null)]
    public void A_language_we_do_not_speak_is_refused_rather_than_guessed(string? tag)
    {
        DisplayLanguages.TryParse(tag, out var language).Should().BeFalse();
        language.Should().Be(DisplayLanguages.Fallback);
    }

    [Fact]
    public void An_unspoken_machine_language_falls_back_to_english()
    {
        DisplayLanguages.FromCulture(new CultureInfo("de-DE")).Should().Be(DisplayLanguage.English);
        DisplayLanguages.FromCulture(new CultureInfo("it-IT")).Should().Be(DisplayLanguage.Italian);
    }

    /// <summary>The {0}, {1}... a phrase expects, in order and without repeats.</summary>
    private static IReadOnlyList<int> Placeholders(string text) =>
        [.. Regex.Matches(text, @"\{(\d+)")
            .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
            .Distinct()
            .Order()];
}

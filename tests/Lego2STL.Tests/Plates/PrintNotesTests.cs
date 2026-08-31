using FluentAssertions;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Text;

namespace Lego2STL.Tests.Plates;

/// <summary>
/// The sheet that goes beside the plates.
/// </summary>
/// <remarks>
/// The same reasoning as the note the calibration command already writes: by the time a folder of
/// files is printed, the command line that made them is long gone. This one carries the settings
/// the preset deliberately does not assert, so that a person has them even when the preset cannot
/// be written at all.
/// </remarks>
public sealed class PrintNotesTests
{
    [Theory]
    [InlineData(DisplayLanguage.English)]
    [InlineData(DisplayLanguage.Italian)]
    public void The_sheet_is_written_in_the_language_of_the_run(DisplayLanguage language)
    {
        var sheet = PrintNotes.Write("A1", Strings.For(language));

        sheet.Should().NotBeNullOrWhiteSpace();
        sheet.Should().Contain(Strings.For(language)[TextKey.PrintNotesTitle]);
    }

    /// <summary>Everything the preset will not assert has to be here, or it is nowhere.</summary>
    [Theory]
    [InlineData("215")]
    [InlineData("55")]
    [InlineData("0.16")]
    public void The_settings_the_preset_declines_to_assert_are_in_the_sheet(string value) =>
        PrintNotes.Write("A1", Strings.English).Should().Contain(value);

    /// <summary>
    /// A borrowed profile is named as borrowed.
    /// </summary>
    /// <remarks>
    /// The P1S has no profiles of its own and uses the P1P's. Someone reading "P1P" on a sheet
    /// they asked for about a P1S should find out here rather than wonder.
    /// </remarks>
    [Fact]
    public void A_printer_that_borrows_another_profile_says_so() =>
        PrintNotes.Write("P1S", Strings.English).Should().Contain("P1P");

    /// <summary>
    /// The sheet says which nozzle the preset is for.
    /// </summary>
    /// <remarks>
    /// The base preset names are per nozzle - "0.16mm Optimal @BBL A1 0.2 nozzle" exists beside
    /// the plain one - and this tool does not know which nozzle is fitted, so it targets the
    /// default 0.4 mm. Someone running a 0.2 mm nozzle has to be told the preset is not theirs.
    /// </remarks>
    [Fact]
    public void The_sheet_says_which_nozzle_the_preset_is_for() =>
        PrintNotes.Write("A1", Strings.English).Should().Contain("0.4");

    /// <summary>When there is no preset, the sheet is all there is, and it still works.</summary>
    [Fact]
    public void A_printer_with_no_preset_still_gets_a_sheet()
    {
        var sheet = PrintNotes.Write("some future machine", Strings.English);

        sheet.Should().NotBeNullOrWhiteSpace();
        sheet.Should().Contain("215", "the settings are the point, and they do not depend on a preset");
    }
}

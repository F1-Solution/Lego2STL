using System.Numerics;
using FluentAssertions;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Text;

namespace Lego2STL.Tests.Plates;

/// <summary>
/// The sheet beside a calibration plate.
/// </summary>
/// <remarks>
/// One sheet, not two. A build plate's folder gets how-to-print.txt; this folder keeps its own
/// single sheet and that sheet carries the print settings too, because two overlapping
/// instruction files in one folder is exactly the confusion the note was written to prevent.
/// </remarks>
public sealed class CalibrationNotesTests
{
    private static PackedPlate APlateWhereTheOrderChanged()
    {
        // Handed over as A, B, C; placed as C, B, A, and on two rows. A map built from the input
        // order would name them in the wrong places, which is the mistake this guards.
        PlacedItem At(string label, float x, float y) =>
            new(new PackableItem(label, new Vector2(10, 10), 5), x, y);

        return new PackedPlate(
            1,
            [At("3705-0.10mm", 5, 5), At("3705-0.05mm", 20, 5), At("3705-0.00mm", 5, 40)],
            new Vector2(30, 45));
    }

    [Theory]
    [InlineData(DisplayLanguage.English)]
    [InlineData(DisplayLanguage.Italian)]
    public void The_sheet_is_written_in_the_language_of_the_run(DisplayLanguage language)
    {
        var sheet = CalibrationNotes.Write(
            APlateWhereTheOrderChanged(), [], "A1", Strings.For(language));

        sheet.Should().Contain(Strings.For(language)[TextKey.CalibrationTitle]);
    }

    /// <summary>The print settings are in this sheet, because there is no second one.</summary>
    [Fact]
    public void The_print_settings_are_in_this_sheet() =>
        CalibrationNotes.Write(APlateWhereTheOrderChanged(), [], "A1", Strings.English)
            .Should().Contain(PrintNotes.Settings(Strings.English));

    /// <summary>
    /// The map follows the placement, not the order the pieces were handed over.
    /// </summary>
    /// <remarks>
    /// The packer sorts by depth, then width, then label. Clearance changes a footprint, so the
    /// depths differ by step and the sort is not the input order. A map that assumed the input
    /// order would send someone to the wrong piece, and they would measure it and believe it.
    /// </remarks>
    [Fact]
    public void The_map_follows_where_the_packer_actually_put_things()
    {
        var sheet = CalibrationNotes.Write(
            APlateWhereTheOrderChanged(), [], "A1", Strings.English);

        var first = sheet.IndexOf("3705-0.10mm", StringComparison.Ordinal);
        var second = sheet.IndexOf("3705-0.05mm", StringComparison.Ordinal);
        var third = sheet.IndexOf("3705-0.00mm", StringComparison.Ordinal);

        first.Should().BeGreaterThan(0);
        first.Should().BeLessThan(second, "they share a row and 0.10 is to the left");
        second.Should().BeLessThan(third, "0.00 is on the row behind");
    }

    /// <summary>A part that could not be built is named, with the fit it took away.</summary>
    [Fact]
    public void A_missing_part_is_named_on_the_sheet() =>
        CalibrationNotes.Write(APlateWhereTheOrderChanged(), ["3673"], "A1", Strings.English)
            .Should().Contain("3673");

    /// <summary>The line to run once a row has been chosen is on the sheet, ready to copy.</summary>
    [Fact]
    public void The_command_to_save_the_answer_is_on_the_sheet() =>
        CalibrationNotes.Write(APlateWhereTheOrderChanged(), [], "A1", Strings.English)
            .Should().Contain("--save").And.Contain("--name");
}

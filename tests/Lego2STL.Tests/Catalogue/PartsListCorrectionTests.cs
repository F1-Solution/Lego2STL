using FluentAssertions;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Run;

namespace Lego2STL.Tests.Catalogue;

/// <summary>
/// Folding a person's answers back into the parts list.
/// </summary>
/// <remarks>
/// The rule that matters is the one about a part already on the list: an answer that names a
/// part in a colour that is already there adds to that row rather than making a second one,
/// because the same part in the same colour is one thing to buy however many labels printed it.
/// </remarks>
public sealed class PartsListCorrectionTests
{
    private static PartsList AListOf(params (string Part, int Color, int Quantity)[] rows) =>
        new([.. rows.Select((r, i) => new PartEntry(
            i + 1, r.Part, r.Color, "Black", Rgb24.Parse("#05131D"), r.Quantity))], []);

    [Fact]
    public void An_answer_adds_the_part_it_names()
    {
        var list = AListOf(("32523", 11, 4));

        var corrected = PartsListCorrection.Apply(
            list, [new ReviewAnswer(372, "(0,0)-(1,1)", "3705", 5, 2, false)]);

        corrected.Entries.Should().HaveCount(2);
        corrected.Entries.Should().Contain(e => e.PartNumber == "3705" && e.Quantity == 2);
    }

    [Fact]
    public void An_answer_naming_a_part_already_there_adds_to_its_row()
    {
        var list = AListOf(("32523", 11, 4));

        var corrected = PartsListCorrection.Apply(
            list, [new ReviewAnswer(372, "(0,0)-(1,1)", "32523", 11, 3, false)]);

        corrected.Entries.Should().ContainSingle().Which.Quantity.Should().Be(7);
    }

    /// <summary>The same part in another colour is another row, as it always was.</summary>
    [Fact]
    public void The_same_part_in_another_colour_is_another_row()
    {
        var list = AListOf(("32523", 11, 4));

        var corrected = PartsListCorrection.Apply(
            list, [new ReviewAnswer(372, "(0,0)-(1,1)", "32523", 5, 3, false)]);

        corrected.Entries.Should().HaveCount(2);
    }

    [Fact]
    public void An_answer_that_says_it_was_never_an_entry_changes_nothing()
    {
        var list = AListOf(("32523", 11, 4));

        var corrected = PartsListCorrection.Apply(
            list, [new ReviewAnswer(372, "(0,0)-(1,1)", null, null, null, true)]);

        corrected.Entries.Should().ContainSingle().Which.Quantity.Should().Be(4);
    }

    [Fact]
    public void An_answer_missing_what_it_needs_changes_nothing()
    {
        var list = AListOf(("32523", 11, 4));

        var corrected = PartsListCorrection.Apply(
            list, [new ReviewAnswer(372, "(0,0)-(1,1)", "3705", null, 2, false)]);

        corrected.Entries.Should().ContainSingle();
    }

    /// <summary>The numbering stays 1..N and in order, because the file is read by people too.</summary>
    [Fact]
    public void The_rows_are_numbered_again_from_one()
    {
        var list = AListOf(("32523", 11, 4), ("2780", 7, 20));

        var corrected = PartsListCorrection.Apply(
            list, [new ReviewAnswer(372, "(0,0)-(1,1)", "3705", 5, 2, false)]);

        corrected.Entries.Select(e => e.Id).Should().Equal(1, 2, 3);
    }

    /// <summary>An added row names its colour, because the file is bought from as it stands.</summary>
    [Fact]
    public void An_added_row_carries_the_name_and_the_value_of_the_colour_it_names()
    {
        var corrected = PartsListCorrection.Apply(
            AListOf(("32523", 11, 4)), [new ReviewAnswer(372, "(0,0)-(1,1)", "3705", 5, 2, false)]);

        var added = corrected.Entries.Should().ContainSingle(e => e.PartNumber == "3705").Subject;

        added.ColorName.Should().Be("Red");
        added.Rgb.Should().Be(Rgb24.Parse("#C91A09"));
    }

    /// <summary>A colour no table knows is not an answer, however confidently it was typed.</summary>
    [Fact]
    public void An_answer_naming_a_colour_that_does_not_exist_changes_nothing()
    {
        var corrected = PartsListCorrection.Apply(
            AListOf(("32523", 11, 4)), [new ReviewAnswer(372, "(0,0)-(1,1)", "3705", 9999, 2, false)]);

        corrected.Entries.Should().ContainSingle();
    }

    /// <summary>The notes the list arrived with are still its notes.</summary>
    [Fact]
    public void Nothing_at_all_to_answer_leaves_the_list_as_it_was()
    {
        var list = AListOf(("32523", 11, 4));

        PartsListCorrection.Apply(list, []).Should().BeEquivalentTo(list);
    }
}

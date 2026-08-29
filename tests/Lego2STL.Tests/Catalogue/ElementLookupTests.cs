using FluentAssertions;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Rebrickable;
using Xunit;

namespace Lego2STL.Tests.Catalogue;

/// <summary>
/// Turning the element numbers official instructions print into parts and colours.
/// </summary>
public sealed class ElementLookupTests
{
    // ---- The digits alone ------------------------------------------------------------------

    /// <summary>
    /// The classic shape: four digits of design number and two of LEGO colour.
    /// </summary>
    /// <remarks>
    /// Each of these was checked against Rebrickable's own table, which is what makes them a
    /// yardstick rather than a restatement of the split.
    /// </remarks>
    [Theory]
    [InlineData("370726", "3707", 26)]   // black
    [InlineData("241201", "2412", 1)]    // white
    [InlineData("366621", "3666", 21)]   // red
    [InlineData("614124", "6141", 24)]   // yellow
    public void A_classic_element_number_comes_apart_into_a_design_and_a_colour(
        string element, string design, int color)
    {
        ElementNumber.TrySplitClassic(element, out var readDesign, out var readColor)
            .Should().BeTrue();

        readDesign.Should().Be(design);
        readColor.Should().Be(color);
    }

    /// <summary>
    /// The modern shape says nothing, and has to say nothing rather than guess: 6177114 split
    /// this way would claim design 6177 in colour 114, and both would be wrong.
    /// </summary>
    [Theory]
    [InlineData("6177114")]
    [InlineData("4514553")]
    [InlineData("12345")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_that_is_not_six_digits_is_left_alone(string? element)
    {
        ElementNumber.TrySplitClassic(element, out _, out _).Should().BeFalse();
    }

    // ---- The table -------------------------------------------------------------------------

    [Fact]
    public async Task An_element_in_the_table_is_looked_up_rather_than_inferred()
    {
        using var folder = new TemporaryFolder();
        folder.WriteElements(
            "element_id,part_num,color_id,design_id",
            "6177114,18654,15,",
            "306926,3069b,0,");

        using var lookup = ElementLookup.Open(folder.Path, [], apiKey: null, ColorReference.Table);

        var modern = await lookup.ResolveAsync("6177114");

        modern.Should().Be(new ResolvedElement("18654", 15, ColorScheme.Rebrickable, ElementSource.LocalTable));

        // A number the digits could also have answered still comes from the table, because the
        // table is right about the mould variant and the digits are not.
        var classic = await lookup.ResolveAsync("306926");

        classic!.PartNumber.Should().Be("3069b");
        classic.Source.Should().Be(ElementSource.LocalTable);
    }

    /// <summary>
    /// The columns are found by name, so a dump whose column order changes still reads.
    /// </summary>
    [Fact]
    public void The_table_is_read_by_column_name_and_not_by_position()
    {
        using var folder = new TemporaryFolder();
        folder.WriteElements(
            "design_id,color_id,element_id,part_num",
            ",15,6177114,18654");

        RebrickableDump.TryReadElements(folder.Path)
            .Should().ContainKey("6177114")
            .WhoseValue.Should().Be(("18654", 15));
    }

    /// <summary>
    /// A dump unpacked into a folder of its own beside the documents is found without anyone
    /// having to name it, which is what makes the option optional.
    /// </summary>
    [Fact]
    public void A_table_one_folder_down_is_found()
    {
        using var folder = new TemporaryFolder();
        folder.WriteElements("element_id,part_num,color_id", "370726,3707,0");

        var sub = Directory.CreateDirectory(Path.Combine(folder.Path, "DB Lego"));
        File.Move(Path.Combine(folder.Path, "elements.csv"), Path.Combine(sub.FullName, "elements.csv"));

        RebrickableDump.TryFindElementsFile(folder.Path).Should().Be(
            Path.Combine(sub.FullName, "elements.csv"));
    }

    [Fact]
    public void A_missing_or_unreadable_table_is_an_absence_and_not_a_failure()
    {
        RebrickableDump.TryReadElements(null).Should().BeEmpty();
        RebrickableDump.TryReadElements(Path.Combine(Path.GetTempPath(), "no-such-folder-" + Guid.NewGuid()))
            .Should().BeEmpty();

        using var folder = new TemporaryFolder();
        folder.WriteElements("something,else", "1,2");

        RebrickableDump.TryReadElements(folder.Path).Should().BeEmpty(
            "a file without the columns is not an element table");
    }

    // ---- Falling back ----------------------------------------------------------------------

    /// <summary>
    /// With no table and no key, the older numbers can still be read and the newer ones are
    /// reported as unresolved rather than invented.
    /// </summary>
    [Fact]
    public async Task With_nothing_to_look_in_the_older_numbers_still_resolve_and_the_newer_do_not()
    {
        using var lookup = ElementLookup.Open(null, [], apiKey: null, ColorReference.Table);

        var classic = await lookup.ResolveAsync("370726");

        classic.Should().Be(new ResolvedElement("3707", 26, ColorScheme.Lego, ElementSource.ClassicNumber));

        (await lookup.ResolveAsync("6177114")).Should().BeNull();
    }

    /// <summary>
    /// The colour a classic number gives has to be one the tool knows, or the number is no
    /// better than a guess and is refused.
    /// </summary>
    [Fact]
    public async Task A_classic_number_naming_a_colour_nobody_has_is_refused()
    {
        using var lookup = ElementLookup.Open(null, [], apiKey: null, ColorReference.Table);

        ColorReference.Table.TryGet(ColorScheme.Lego, 99, out _)
            .Should().BeFalse("this test is about a colour code that does not exist");

        (await lookup.ResolveAsync("370799")).Should().BeNull();
    }

    [Fact]
    public async Task What_answered_is_counted_so_a_run_can_say_which_numbers_it_inferred()
    {
        using var folder = new TemporaryFolder();
        folder.WriteElements("element_id,part_num,color_id", "6177114,18654,15");

        using var lookup = ElementLookup.Open(folder.Path, [], apiKey: null, ColorReference.Table);

        await lookup.ResolveAsync("6177114");
        await lookup.ResolveAsync("370726");
        await lookup.ResolveAsync("6177114");   // asked twice, counted once

        var notes = string.Join(" ", lookup.Notes());

        notes.Should().Contain("1 from the table").And.Contain("1 inferred from the number itself");
    }

    /// <summary>A folder that is deleted however the test ends.</summary>
    private sealed class TemporaryFolder : IDisposable
    {
        public TemporaryFolder() =>
            Path = Directory.CreateDirectory(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lego2stl-elements-" + Guid.NewGuid().ToString("N")))
                .FullName;

        public string Path { get; }

        public void WriteElements(params string[] lines) =>
            File.WriteAllLines(System.IO.Path.Combine(Path, RebrickableDump.ElementsFileName), lines);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temporary folder is not worth failing a test over.
            }
        }
    }
}

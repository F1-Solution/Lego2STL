using System;
using System.Linq;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Text;
using Lego2STL.Gui.ViewModels;

namespace Lego2STL.UiTests;

/// <summary>
/// The eighteen options a run is shaped by, as rows that can be found again.
/// </summary>
/// <remarks>
/// The last test here is the one the whole indirection exists for. Rows read and write the one
/// settings object through delegates rather than holding values of their own, so a row that
/// looks set and a run that was not given the setting cannot come apart. Everything above it -
/// searching, filtering, resetting - is only worth having if that holds.
/// </remarks>
public sealed class OptionRowTests
{
    private static readonly string[] TheNineteen =
    [
        "--csv-only", "--no-plates", "--ascii", "--keep-origin",
        "--no-repair", "--no-seam-repair", "--offline", "--no-unofficial",
        "--scale", "--clearance", "--weld-tolerance", "--plate-spacing",
        "--output-dir", "--element-map", "--ldraw-dir", "--ldraw-cache",
        "--plate-size",
        "--delimiter", "--printer",
    ];

    [AvaloniaFact]
    public void Every_option_a_run_is_shaped_by_has_a_row_and_no_more_than_one()
    {
        var rows = new OptionRowsViewModel(new RunOptionsViewModel());

        rows.Rows.Should().HaveCount(19);
        rows.Rows.Select(row => row.Flag).Should().BeEquivalentTo(TheNineteen);
        rows.Rows.Select(row => row.Flag).Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// Every row is named in the reader's own language, and still says which flag it is. A row
    /// showing a key rather than a phrase means a wording was never written.
    /// </summary>
    [AvaloniaFact]
    public void Every_option_is_named_in_words_as_well_as_by_its_flag()
    {
        var rows = new OptionRowsViewModel(new RunOptionsViewModel());

        rows.Rows.Should().OnlyContain(row => !string.IsNullOrWhiteSpace(row.Label));
        rows.Rows.Select(row => row.Label).Should().OnlyHaveUniqueItems();
        rows.Rows.Select(row => row.Label).Should().NotIntersectWith(Enum.GetNames<TextKey>());
        rows.Rows.Should().OnlyContain(row => !row.Label.StartsWith("--", StringComparison.Ordinal));
    }

    /// <summary>
    /// Naming the options did not cost the search the flags: anyone arriving from the terminal
    /// still finds a row by the flag they already know.
    /// </summary>
    [AvaloniaFact]
    public void An_option_is_found_by_its_name_as_well_as_by_its_flag()
    {
        var rows = new OptionRowsViewModel(new RunOptionsViewModel());
        var clearance = rows.Rows.Single(row => row.Flag == "--clearance");

        rows.Search = clearance.Label;

        clearance.IsVisible.Should().BeTrue();

        rows.Search = "--clearance";

        clearance.IsVisible.Should().BeTrue();
    }

    /// <summary>
    /// A help text with a placeholder left in it is a sentence with a hole: the printer row read
    /// "...to lay plates out for: {0}." on screen while the command line filled the same phrase
    /// in properly.
    /// </summary>
    [AvaloniaFact]
    public void No_option_describes_itself_with_a_placeholder_still_in_it()
    {
        var rows = new OptionRowsViewModel(new RunOptionsViewModel());

        rows.Rows.Should().OnlyContain(row => !row.Help.Contains('{', StringComparison.Ordinal));

        rows.Rows.Single(row => row.Flag == "--printer").Help
            .Should().Contain(PrintBeds.Default.Name);
    }

    /// <summary>
    /// An empty bed size means "whatever the printer's bed is", so that is what the empty box
    /// says. It read a flat "220x220" while the default printer's bed was 256x256, which is a
    /// grey number that was never anybody's default.
    /// </summary>
    [AvaloniaFact]
    public void The_empty_bed_size_shows_the_bed_the_chosen_printer_really_has()
    {
        var options = new RunOptionsViewModel();
        var rows = new OptionRowsViewModel(options);
        var bedSize = (TextOptionRow)rows.Rows.Single(row => row.Flag == "--plate-size");

        bedSize.Value.Should().BeNull("a bed size is only typed when it differs from the printer");
        bedSize.Placeholder.Should().Be(PrintBeds.Default.AsSize).And.Be("256x256");

        options.Printer = "H2D";

        bedSize.Placeholder.Should().Be("350x320x325",
            "the placeholder has to follow the printer, or it is a default nobody has");

        options.Printer = "A1mini";

        bedSize.Placeholder.Should().Be("180x180");
    }

    [AvaloniaFact]
    public void A_run_not_yet_touched_has_nothing_changed()
    {
        var rows = new OptionRowsViewModel(new RunOptionsViewModel());

        rows.Rows.Should().OnlyContain(row => row.IsChanged == false);
        rows.HiddenCount.Should().Be(0);
    }

    [AvaloniaFact]
    public void Changing_one_setting_marks_that_row_and_only_that_row()
    {
        var options = new RunOptionsViewModel { Clearance = 0.15 };
        var rows = new OptionRowsViewModel(options);

        rows.Rows.Where(row => row.IsChanged).Select(row => row.Flag)
            .Should().Equal("--clearance");
    }

    [AvaloniaFact]
    public void Showing_only_what_was_changed_hides_the_rest_and_says_how_many()
    {
        var options = new RunOptionsViewModel { Clearance = 0.15 };
        var rows = new OptionRowsViewModel(options) { ChangedOnly = true };

        rows.Rows.Where(row => row.IsVisible).Select(row => row.Flag).Should().Equal("--clearance");
        rows.HiddenCount.Should().Be(18, "an option set three runs ago must not sit invisible in silence");
    }

    /// <summary>
    /// A first run opens showing everything, because there is nothing yet to have changed and
    /// an empty list is a worse introduction than a long one. Every run after opens narrowed.
    /// </summary>
    [AvaloniaFact]
    public void Whether_it_opens_narrowed_is_decided_by_whoever_knows_about_earlier_runs()
    {
        new OptionRowsViewModel(new RunOptionsViewModel(), changedOnly: false)
            .Rows.Should().OnlyContain(row => row.IsVisible);

        new OptionRowsViewModel(new RunOptionsViewModel { Clearance = 0.15 }, changedOnly: true)
            .Rows.Where(row => row.IsVisible).Select(row => row.Flag)
            .Should().Equal("--clearance");
    }

    [AvaloniaFact]
    public void A_hidden_row_is_still_a_row()
    {
        var rows = new OptionRowsViewModel(new RunOptionsViewModel()) { ChangedOnly = true };

        rows.Rows.Should().HaveCount(19, "filtering hides rows; it does not throw them away");
        rows.Rows.Should().OnlyContain(row => row.IsVisible == false);
    }

    [AvaloniaFact]
    public void Searching_narrows_the_list_to_what_was_typed()
    {
        var rows = new OptionRowsViewModel(new RunOptionsViewModel()) { Search = "weld" };

        rows.Rows.Where(row => row.IsVisible).Select(row => row.Flag)
            .Should().Equal("--weld-tolerance");
    }

    [AvaloniaFact]
    public void Searching_looks_at_what_an_option_does_and_not_only_at_its_name()
    {
        var rows = new OptionRowsViewModel(new RunOptionsViewModel());
        var plateSpacing = rows.Rows.Single(row => row.Flag == "--plate-spacing");

        // A word from its own help text, whatever that text is, so re-wording help does not
        // quietly stop the search working.
        var word = plateSpacing.Help.Split(' ')
            .First(w => w.Length > 5 && w.All(char.IsLetter));

        rows.Search = word;

        plateSpacing.IsVisible.Should().BeTrue();
        rows.Rows.Where(row => row.IsVisible).Should().NotBeEmpty();
    }

    [AvaloniaFact]
    public void Searching_for_nothing_shows_everything_again()
    {
        var rows = new OptionRowsViewModel(new RunOptionsViewModel()) { Search = "weld" };

        rows.Search = null;

        rows.Rows.Should().OnlyContain(row => row.IsVisible);
        rows.HiddenCount.Should().Be(0);
    }

    [AvaloniaFact]
    public void Putting_one_option_back_leaves_the_others_alone()
    {
        var options = new RunOptionsViewModel { Clearance = 0.15, ScalePercent = 90 };
        var rows = new OptionRowsViewModel(options);
        var clearance = rows.Rows.Single(row => row.Flag == "--clearance");

        clearance.ResetOneCommand.Execute(null);

        clearance.IsChanged.Should().BeFalse();
        options.Clearance.Should().Be(0);
        options.ScalePercent.Should().Be(90, "one row was put back, not the lot");
    }

    [AvaloniaFact]
    public void Putting_them_all_back_leaves_a_run_as_it_started()
    {
        var options = new RunOptionsViewModel { Clearance = 0.15, ScalePercent = 90, Offline = true };
        var rows = new OptionRowsViewModel(options);

        rows.ResetAllCommand.Execute(null);

        rows.Rows.Should().OnlyContain(row => row.IsChanged == false);
        options.ToSettings().Should().BeEquivalentTo(new RunOptionsViewModel().ToSettings());
    }

    [AvaloniaFact]
    public void An_option_that_would_do_nothing_cannot_be_reached()
    {
        var options = new RunOptionsViewModel();
        var rows = new OptionRowsViewModel(options);

        var noPlates = rows.Rows.Single(row => row.Flag == "--no-plates");
        var plateRows = rows.Rows.Where(row =>
            row.Flag is "--printer" or "--plate-size" or "--plate-spacing").ToList();

        noPlates.IsEnabled.Should().BeTrue();
        plateRows.Should().OnlyContain(row => row.IsEnabled);

        options.CsvOnly = true;

        noPlates.IsEnabled.Should().BeFalse("there are no plates to refuse when there are no shapes");

        options.CsvOnly = false;
        options.NoPlates = true;

        plateRows.Should().OnlyContain(row => row.IsEnabled == false,
            "a bed nothing will be laid out on is not worth choosing");
    }

    /// <summary>
    /// The point of the whole indirection: a row is a view of the one settings object, not a
    /// copy of it. A row that looked set while the run was given something else would make
    /// every other test here worthless.
    /// </summary>
    [AvaloniaFact]
    public void What_a_row_is_set_to_is_what_the_run_is_given()
    {
        var options = new RunOptionsViewModel();
        var rows = new OptionRowsViewModel(options);

        ((NumberOptionRow)rows.Rows.Single(row => row.Flag == "--clearance")).Value = 0.2;
        ((NumberOptionRow)rows.Rows.Single(row => row.Flag == "--scale")).Value = 120;
        ((ToggleOptionRow)rows.Rows.Single(row => row.Flag == "--offline")).Value = true;
        ((ToggleOptionRow)rows.Rows.Single(row => row.Flag == "--csv-only")).Value = true;
        ((TextOptionRow)rows.Rows.Single(row => row.Flag == "--plate-size")).Value = "220x220";
        ((PathOptionRow)rows.Rows.Single(row => row.Flag == "--ldraw-dir")).Value = "C:/ldraw";
        ((ChoiceOptionRow)rows.Rows.Single(row => row.Flag == "--printer")).Value = "X1C";

        var settings = options.ToSettings();

        settings.Clearance.Should().Be(0.2);
        settings.ScalePercent.Should().Be(120);
        settings.Offline.Should().BeTrue();
        settings.Stages.Should().Be(RunStages.PartsListOnly);
        settings.PlateSize.Should().Be("220x220");
        settings.LDrawDirectory.Should().Be("C:/ldraw");
        settings.Printer.Should().Be("X1C");
    }

    [AvaloniaFact]
    public void A_row_set_the_other_way_round_reads_the_setting_the_other_way_round()
    {
        var options = new RunOptionsViewModel();
        var rows = new OptionRowsViewModel(options);

        var noUnofficial = (ToggleOptionRow)rows.Rows.Single(row => row.Flag == "--no-unofficial");

        noUnofficial.Value.Should().BeFalse("the unofficial collection is included by default");

        noUnofficial.Value = true;

        options.IncludeUnofficial.Should().BeFalse();
        options.ToSettings().IncludeUnofficial.Should().BeFalse();
        noUnofficial.IsChanged.Should().BeTrue();
    }
}

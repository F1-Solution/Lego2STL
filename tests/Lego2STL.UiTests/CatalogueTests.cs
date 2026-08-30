using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;
using Lego2STL.Gui.Localization;
using Lego2STL.Gui.Services;
using Lego2STL.Gui.ViewModels;
using Lego2STL.Gui.Views;

namespace Lego2STL.UiTests;

/// <summary>
/// The catalogue, shown a run that has already happened.
/// </summary>
/// <remarks>
/// The cards are the part of the window with the most going on - a picture or a colour swatch
/// in its place, a quantity, a warning that appears only sometimes - so they are the part most
/// worth drawing and looking at rather than reasoning about. The run reaches them the way every
/// run does now: through the record it kept and the one projection of it.
/// </remarks>
public sealed class CatalogueTests
{
    private static RunDocumentViewModel APretendRun()
    {
        var entries = new[]
        {
            new PartEntry(1, "32523", 11, "Black", Rgb24.Parse("#05131D"), 4),
            new PartEntry(2, "3705", 5, "Red", Rgb24.Parse("#C91A09"), 12),
            new PartEntry(3, "4265c", 9, "Light Gray", Rgb24.Parse("#9BA19D"), 8),
            new PartEntry(4, "32250", 85, "Dark Bluish Gray", Rgb24.Parse("#6C6E68"), 2),
            new PartEntry(5, "2780", 7, "Blue", Rgb24.Parse("#0055BF"), 20),
            new PartEntry(6, "32017", 2, "Tan", Rgb24.Parse("#E4CD9E"), 1),
        };

        var layout = RunLayout.For(
            Path.Combine(Path.GetTempPath(), "lego2stl-catalogue", "parts.csv"));

        var outcome = new RunOutcome
        {
            Result = RunResult.Complete,
            Settings = new RunSettings { Kind = InputKind.PartsList, InputPath = "parts.csv", Offline = true },
            Layout = layout,
            PartsList = new PartsList(entries, []),
        };

        return RunDocumentViewModel.Of(RunDocument.From(
            RunManifest.From(outcome, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null), layout));
    }

    private static Window Showing(RunDocumentViewModel run)
    {
        var window = new Window
        {
            Width = 1000,
            Height = 700,
            Content = new RunDocumentView { DataContext = run },
        };

        window.Show();
        return window;
    }

    [AvaloniaFact]
    public void Every_part_gets_a_card()
    {
        using var run = APretendRun();
        var window = Showing(run);

        window.CaptureRenderedFrame().Should().NotBeNull();

        run.Parts.Should().HaveCount(6);
        run.VisibleParts.Should().HaveCount(6);
    }

    [AvaloniaFact]
    public void The_colours_present_are_offered_as_a_filter_and_the_filter_narrows_the_list()
    {
        using var run = APretendRun();
        var window = Showing(run);

        run.Colours.Should().BeEquivalentTo(
            ["Black", "Blue", "Dark Bluish Gray", "Light Gray", "Red", "Tan"]);

        run.ColourFilter = "Red";
        window.CaptureRenderedFrame();

        run.VisibleParts.Should().ContainSingle().Which.PartNumber.Should().Be("3705");
    }

    [AvaloniaFact]
    public void Searching_matches_a_part_number()
    {
        using var run = APretendRun();
        var window = Showing(run);

        run.Search = "4265";
        window.CaptureRenderedFrame();

        run.VisibleParts.Should().ContainSingle().Which.PartNumber.Should().Be("4265c");
    }

    /// <summary>
    /// A plate is found from the record even though the two are worded in different languages.
    /// </summary>
    /// <remarks>
    /// The failure this ends: a run made in Italian names its plates in Italian - "bianco.3mf" -
    /// while the record keeps every colour in the one canonical English, "White". Matching the
    /// file name against the record's wording therefore found nothing, and every single "open
    /// plate" button on the page was disabled however well the run had gone. The colour code is
    /// the same number in both, so that is what they are matched on now.
    /// </remarks>
    [AvaloniaFact]
    public void A_plate_named_in_italian_is_found_for_a_part_recorded_in_english()
    {
        using var run = APretendRunWithPlates(DisplayLanguage.Italian);

        var black = run.Parts.Single(part => part.PartNumber == "32523");

        black.PlatePath.Should().NotBeNull();
        Path.GetFileName(black.PlatePath!).Should().Be("nero.3mf");
        black.HasPlateFile.Should().BeTrue("the run wrote this plate and it is still there");
    }

    /// <summary>The same run in English, so the match is not merely an Italian special case.</summary>
    [AvaloniaFact]
    public void A_plate_named_in_english_is_found_too()
    {
        using var run = APretendRunWithPlates(DisplayLanguage.English);

        var black = run.Parts.Single(part => part.PartNumber == "32523");

        Path.GetFileName(black.PlatePath!).Should().Be("black.3mf");
        black.HasPlateFile.Should().BeTrue();
    }

    /// <summary>
    /// A run recorded before plates were listed still finds them, by their names on the disk.
    /// </summary>
    /// <remarks>
    /// Without this every run already sitting in someone's folders would stay broken until it
    /// was made again, which for a run of several hours is not a fix.
    /// </remarks>
    [AvaloniaFact]
    public void A_run_that_recorded_no_plates_falls_back_to_the_names_on_the_disk()
    {
        using var run = APretendRunWithPlates(DisplayLanguage.Italian, recordPlates: false);

        var black = run.Parts.Single(part => part.PartNumber == "32523");

        black.PlatePath.Should().NotBeNull("the file is there to be found by name");
        Path.GetFileName(black.PlatePath!).Should().Be("nero.3mf");
    }

    /// <summary>A colour whose plate was never written offers nothing to open.</summary>
    [AvaloniaFact]
    public void A_colour_with_no_plate_offers_none()
    {
        using var run = APretendRunWithPlates(DisplayLanguage.Italian);

        var red = run.Parts.Single(part => part.PartNumber == "3705");

        red.PlatePath.Should().BeNull();
        red.HasPlateFile.Should().BeFalse();
    }

    /// <summary>
    /// A run that wrote one plate per colour, in the language it was run in.
    /// </summary>
    /// <remarks>
    /// Only black gets a plate, so the test can tell "found the right one" from "found one".
    /// </remarks>
    private static RunDocumentViewModel APretendRunWithPlates(
        DisplayLanguage language, bool recordPlates = true)
    {
        var entries = new[]
        {
            new PartEntry(1, "32523", 11, "Black", Rgb24.Parse("#05131D"), 4),
            new PartEntry(2, "3705", 5, "Red", Rgb24.Parse("#C91A09"), 12),
        };

        var folder = Path.Combine(
            Path.GetTempPath(), "lego2stl-plates-" + Guid.NewGuid().ToString("N"), "parts.csv");

        var layout = RunLayout.For(folder);
        Directory.CreateDirectory(layout.PlateDirectory);

        var plateName = PlateFileName.For(ColorNames.For(language, "Black"), 1, 1);
        File.WriteAllText(Path.Combine(layout.PlateDirectory, plateName), "not really a plate");

        var built = new BuiltPlate(plateName, ColorNames.For(language, "Black"), 11,
            Rgb24.Parse("#05131D"), 1, 4, "40 x 40");

        var outcome = new RunOutcome
        {
            Result = RunResult.Complete,
            Settings = new RunSettings
            {
                Kind = InputKind.PartsList,
                InputPath = "parts.csv",
                Offline = true,
                Language = language,
            },
            Layout = layout,
            PartsList = new PartsList(entries, []),
            Plates = recordPlates ? new PlateBuildResult([built], []) : null,
        };

        return RunDocumentViewModel.Of(RunDocument.From(
            RunManifest.From(outcome, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null), layout));
    }

    [AvaloniaFact]
    public void A_picture_of_a_filled_catalogue_is_written()
    {
        using var run = APretendRun();
        var window = Showing(run);

        var frame = window.CaptureRenderedFrame();
        frame.Should().NotBeNull();

        var directory = Environment.GetEnvironmentVariable("LEGO2STL_UI_SHOTS");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        using var file = File.Create(Path.Combine(directory, "Catalogue-filled.png"));
#pragma warning disable CS0618
        frame!.Save(file);
#pragma warning restore CS0618
    }

    /// <summary>Surfaces passing through each other is not the same fault as holes.</summary>
    [AvaloniaFact]
    public void A_shape_with_no_holes_is_not_told_it_has_open_edges()
    {
        var part = new RunDocumentPart(
            1, "32064a", 11, "Black", Rgb24.Parse("#05131D"), 2,
            Title: "a part", Size: "32 x 16 x 22.4 mm",
            IsClosed: false, OpenEdgeCount: 0, ThinnestSpanMm: 8,
            OverusedEdgeCount: 2, ClosedAtTolerance: null);

        var card = new CataloguePartViewModel(part, null, null);

        card.HasOpenEdges.Should().BeFalse();
        card.HasSelfIntersection.Should().BeTrue();
        card.HasWarning.Should().BeTrue();
        card.WarningText.Should().NotContain(
            Loc.Current.Text(TextKey.UiWarningNotClosed),
            "it has no open edges to warn about");
    }

    /// <summary>
    /// A run with a part too big says so, and offers the scale that would fit.
    /// </summary>
    /// <remarks>
    /// Pressing the offer starts again from this run's own parts list, so it lands in the same
    /// folder rather than scattering a second copy - the same path "continue from the parts
    /// list" already takes.
    /// </remarks>
    [AvaloniaFact]
    public void A_run_whose_parts_do_not_fit_offers_a_scale_that_would()
    {
        using var run = ARunWithAPartTooBig();

        run.HasPartsThatDoNotFit.Should().BeTrue();
        run.DoesNotFitText.Should().Contain("168");

        run.Parts.Single(p => p.PartNumber == "46891").DoesNotFitThePlate.Should().BeTrue();
        run.Parts.Single(p => p.PartNumber == "32523").DoesNotFitThePlate.Should().BeFalse();

        RunSettings? asked = null;
        run.ContinueRequested += (_, settings) => asked = settings;

        run.TryASmallerScaleCommand.Execute(null);

        asked.Should().NotBeNull();
        asked!.ScalePercent.Should().Be(168);
        asked.Kind.Should().Be(InputKind.PartsList);
    }

    /// <summary>A run where everything fits offers nothing.</summary>
    [AvaloniaFact]
    public void A_run_whose_parts_all_fit_offers_nothing()
    {
        using var run = APretendRun();

        run.HasPartsThatDoNotFit.Should().BeFalse();
    }

    private static RunDocumentViewModel ARunWithAPartTooBig()
    {
        var layout = RunLayout.For(Path.Combine(
            Path.GetTempPath(), "lego2stl-toobig-" + Guid.NewGuid().ToString("N"), "parts.csv"));

        layout.CreateDirectories();
        File.WriteAllText(layout.PartsListPath, "a parts list");

        var entries = new[]
        {
            new PartEntry(1, "32523", 11, "Black", Rgb24.Parse("#05131D"), 4),
            new PartEntry(2, "46891", 11, "Black", Rgb24.Parse("#05131D"), 1),
        };

        var outcome = new RunOutcome
        {
            Result = RunResult.Complete,
            Settings = new RunSettings
            {
                Kind = InputKind.PartsList,
                InputPath = layout.PartsListPath,
                Offline = true,
                ScalePercent = 200,
            },
            Layout = layout,
            PartsList = new PartsList(entries, []),
            Plates = new PlateBuildResult(
                [],
                [new SkippedPart("46891", 304f, 184.8f, 192.2f, TooTall: false)]),
        };

        var manifest = RunManifest.From(
            outcome, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null) with
        {
            LargestFittingScalePercent = 168,
        };

        return RunDocumentViewModel.Of(RunDocument.From(manifest, layout));
    }

    /// <summary>The catalogue can show either numbering, and says when it has none.</summary>
    [AvaloniaFact]
    public void The_catalogue_shows_either_numbering()
    {
        using var run = ARunWithElementNumbers();

        var withOne = run.Parts.Single(p => p.PartNumber == "32523");
        var without = run.Parts.Single(p => p.PartNumber == "3705");

        withOne.ShownNumber.Should().Be("32523");

        run.Numbering = PartNumbering.LegoElement;

        withOne.ShownNumber.Should().Be("6177114");
        without.ShownNumber.Should().Be(
            Loc.Current.Text(TextKey.UiNoElementNumber),
            "a list from a CSV has no element numbers and must not invent one");

        run.Numbering = PartNumbering.BrickLink;
        withOne.ShownNumber.Should().Be("32523");
    }

    private static RunDocumentViewModel ARunWithElementNumbers()
    {
        var layout = RunLayout.For(Path.Combine(
            Path.GetTempPath(), "lego2stl-numbering-" + Guid.NewGuid().ToString("N"), "parts.csv"));

        var entries = new[]
        {
            new PartEntry(1, "32523", 11, "Black", Rgb24.Parse("#05131D"), 4, ElementId: "6177114"),
            new PartEntry(2, "3705", 5, "Red", Rgb24.Parse("#C91A09"), 12),
        };

        var outcome = new RunOutcome
        {
            Result = RunResult.Complete,
            Settings = new RunSettings { Kind = InputKind.PartsList, InputPath = "parts.csv", Offline = true },
            Layout = layout,
            PartsList = new PartsList(entries, []),
        };

        return RunDocumentViewModel.Of(RunDocument.From(
            RunManifest.From(outcome, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null), layout));
    }

    /// <summary>A part that was not printed says so and offers to be bought.</summary>
    [AvaloniaFact]
    public void A_part_that_cannot_be_printed_offers_to_be_bought()
    {
        var part = new RunDocumentPart(
            1, "5102c13", 11, "Black", Rgb24.Parse("#05131D"), 3,
            Title: null, Size: null,
            IsClosed: null, OpenEdgeCount: null, ThinnestSpanMm: null,
            OverusedEdgeCount: null, ClosedAtTolerance: null,
            ElementId: "6177114", Printability: "material");

        var card = new CataloguePartViewModel(
            part, null, null, doesNotFitThePlate: false, shop: Shops.Defaults[0]);

        card.HasWarning.Should().BeTrue();
        card.CanBuy.Should().BeTrue();
        card.WarningText.Should().Contain(Loc.Current.Text(TextKey.UiNotPrintedMaterial));
    }

    /// <summary>An ordinary part is not offered for sale; it was printed.</summary>
    [AvaloniaFact]
    public void A_part_that_was_printed_is_not_offered_for_sale()
    {
        var part = new RunDocumentPart(
            1, "32523", 11, "Black", Rgb24.Parse("#05131D"), 4,
            Title: "a beam", Size: "32 x 16 x 8 mm",
            IsClosed: true, OpenEdgeCount: 0, ThinnestSpanMm: 8,
            OverusedEdgeCount: 0, ClosedAtTolerance: null,
            ElementId: null, Printability: "yes");

        var card = new CataloguePartViewModel(
            part, null, null, doesNotFitThePlate: false, shop: Shops.Defaults[0]);

        card.CanBuy.Should().BeFalse();
        card.HasWarning.Should().BeFalse();
    }

    /// <summary>With no shop there is nothing to press, and the card still stands.</summary>
    [AvaloniaFact]
    public void With_no_shop_chosen_nothing_is_offered()
    {
        var part = new RunDocumentPart(
            1, "5102c13", 11, "Black", Rgb24.Parse("#05131D"), 3,
            Title: null, Size: null,
            IsClosed: null, OpenEdgeCount: null, ThinnestSpanMm: null,
            OverusedEdgeCount: null, ClosedAtTolerance: null,
            ElementId: null, Printability: "material");

        var card = new CataloguePartViewModel(part, null, null, false, shop: null);

        card.CanBuy.Should().BeFalse();
        card.HasWarning.Should().BeTrue("it still has to say why there is no shape");
    }
}

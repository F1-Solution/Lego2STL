using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FluentAssertions;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;
using Lego2STL.Gui.Localization;
using Lego2STL.Gui.ViewModels;

namespace Lego2STL.UiTests;

/// <summary>
/// A run's own page, whether it is happening now or happened weeks ago.
/// </summary>
/// <remarks>
/// The first test is the one this design rests on at the window's end, as its Core counterpart
/// is at the other: a run being watched and the same run reopened have to put the same
/// catalogue on screen, because both are built from the same projection of the same record. The
/// rest is what the window owes the command line - that a run which could not be verified stops
/// being reported as finished.
/// </remarks>
public sealed class RunDocumentViewTests
{
    [AvaloniaFact]
    public async Task A_live_run_and_the_same_run_reopened_show_the_same_catalogue()
    {
        var layout = RunLayout.At(ARunFolder());
        var manifest = ARecordOf(layout);

        await RunManifest.WriteAsync(layout, manifest);

        using var live = RunDocumentViewModel.Of(RunDocument.From(manifest, layout));
        using var reopened = RunDocumentViewModel.Reopened(RunFolder.Read(layout.Root));

        reopened.Parts.Should().HaveCount(live.Parts.Count).And.NotBeEmpty();

        reopened.Parts.Select(Facts).Should().BeEquivalentTo(
            live.Parts.Select(Facts),
            "a run being watched and the same run reopened have to show the same parts");
    }

    [AvaloniaTheory]
    [InlineData(RunStatus.Running, TextKey.UiStatusRunning)]
    [InlineData(RunStatus.Complete, TextKey.UiStatusComplete)]
    [InlineData(RunStatus.NeedsDecision, TextKey.UiStatusNeedsDecision)]
    [InlineData(RunStatus.Failed, TextKey.UiStatusFailed)]
    [InlineData(RunStatus.Stopped, TextKey.UiStatusStopped)]
    public void How_a_run_stands_is_said_in_its_own_word(RunStatus status, TextKey expected)
    {
        using var page = RunDocumentViewModel.Of(ADocument() with { Status = status });

        page.StatusText.Should().Be(Loc.Current.Text(expected));
    }

    /// <summary>
    /// The defect this whole page exists to end: a run the command line calls exit code 2 was
    /// being shown as finished, at 100%, with a green tick.
    /// </summary>
    [AvaloniaFact]
    public void A_run_that_could_not_be_verified_is_never_reported_as_done()
    {
        using var page = RunDocumentViewModel.Of(ADocument() with
        {
            Status = RunStatus.NeedsDecision,
            LastStage = new ManifestStage(RunStage.BuildingShapes, 2, 3),
            Failed = [new ManifestFailure("3705", "no shape file for this part number")],
        });

        page.StatusText.Should().NotBe(Loc.Current.Text(TextKey.UiDone));
        page.StatusText.Should().Be(Loc.Current.Text(TextKey.UiStatusNeedsDecision));
        page.NeedsDecision.Should().BeTrue();
        page.Progress.Should().BeLessThan(1, "it stopped where it stopped");
        page.ProblemText.Should().Contain("3705").And.Contain("no shape file");
    }

    [AvaloniaFact]
    public void A_run_that_finished_has_nothing_to_answer_for()
    {
        using var page = RunDocumentViewModel.Of(ADocument() with { Status = RunStatus.Complete });

        page.NeedsDecision.Should().BeFalse();
        page.HasProblem.Should().BeFalse();
        page.Progress.Should().Be(1);
    }

    [AvaloniaFact]
    public void Entries_that_could_not_be_read_are_named_as_the_cause()
    {
        using var page = RunDocumentViewModel.Of(ADocument() with
        {
            Status = RunStatus.NeedsDecision,
            Unread =
            [
                new ManifestUnread(
                    4, "(10,20)-(60,40)", "4x", null, null, null, "the quantity could not be read"),
            ],
        });

        page.HasProblem.Should().BeTrue();
        page.ProblemText.Should().Contain("page 4");
    }

    [AvaloniaFact]
    public void A_folder_from_before_runs_recorded_themselves_says_so_and_offers_to_run_again()
    {
        var layout = RunLayout.At(ARunFolder());
        File.WriteAllText(layout.PartsListPath, "id;part;colour\n");

        using var page = RunDocumentViewModel.Reopened(RunFolder.Read(layout.Root));

        page.HasNotice.Should().BeTrue();
        page.NoticeText.Should().Be(Loc.Current.Text(TextKey.UiNoManifest));
        page.CanRunItAgain.Should().BeTrue();
    }

    [AvaloniaFact]
    public async Task A_run_recorded_by_a_newer_build_says_which_part_is_understood()
    {
        var layout = RunLayout.At(ARunFolder());

        await RunManifest.WriteAsync(
            layout, ARecordOf(layout) with { Version = RunManifest.CurrentVersion + 1 });

        using var page = RunDocumentViewModel.Reopened(RunFolder.Read(layout.Root));

        page.HasNotice.Should().BeTrue();
        page.NoticeText.Should().Be(Loc.Current.Text(TextKey.UiNewerManifest));
        page.Parts.Should().NotBeEmpty("what this build understands is still shown");
    }

    [AvaloniaFact]
    public void A_run_that_finished_cleanly_has_nothing_to_notice()
    {
        using var page = RunDocumentViewModel.Of(ADocument() with { Status = RunStatus.Complete });

        page.HasNotice.Should().BeFalse();
        page.CanRunItAgain.Should().BeFalse();
    }

    [AvaloniaFact]
    public void The_colour_filter_and_the_search_still_narrow_the_list()
    {
        using var page = RunDocumentViewModel.Of(ADocument());

        page.Parts.Should().HaveCount(3);
        page.Colours.Should().Contain("Red");

        page.ColourFilter = "Red";
        page.VisibleParts.Should().ContainSingle().Which.ColorName.Should().Be("Red");

        page.ColourFilter = null;
        page.Search = "32523";
        page.VisibleParts.Should().ContainSingle().Which.PartNumber.Should().Be("32523");

        page.Search = null;
        page.VisibleParts.Should().HaveCount(3);
    }

    /// <summary>
    /// A new run is a new document, so the previous run's log stays on its own page. Nothing
    /// clears a log; that is the whole of the fix for a log that used to be wiped mid-run.
    /// </summary>
    [AvaloniaFact]
    public void A_reopened_run_has_no_live_facet_to_speak_of()
    {
        var layout = RunLayout.At(ARunFolder());
        File.WriteAllText(layout.PartsListPath, "id;part;colour\n");

        using var page = RunDocumentViewModel.Reopened(RunFolder.Read(layout.Root));

        page.IsLive.Should().BeFalse();
        page.Busy.Should().BeFalse();
        page.Log.Should().BeEmpty();
    }

    /// <summary>
    /// The defect this ends: "the calling thread cannot access this object because a different
    /// thread owns it", as soon as a run got past its first fetch.
    /// </summary>
    /// <remarks>
    /// The pipeline awaits without capturing a context, so it hands its log lines and its
    /// progress back on a pool thread. The log is bound to a list on screen and the stage to a
    /// label, so both have to be touched on the window's thread. Watching where the collection
    /// changes is the whole of the claim; a real run is what makes the thread really move.
    /// </remarks>
    [AvaloniaFact]
    public async Task What_a_running_pipeline_says_reaches_the_page_on_the_windows_thread()
    {
        var settings = ARunThatLeavesTheMachineAlone();
        var layout = RunLayout.Plan(settings)!;

        using var page = RunDocumentViewModel.Live(settings, layout);

        var saidOffTheWindowsThread = 0;
        page.Log.CollectionChanged += (_, _) =>
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Interlocked.Increment(ref saidOffTheWindowsThread);
            }
        };

        await page.RunAsync();

        page.Log.Should().NotBeEmpty("a run says what it is doing as it does it");
        saidOffTheWindowsThread.Should().Be(0,
            "every line has to arrive on the thread the window is on");
        page.StageText.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// A parts list on a disk, the shape library pointed at nothing and the network refused, so
    /// a shapes run reaches its failure path without leaving the machine.
    /// </summary>
    private static RunSettings ARunThatLeavesTheMachineAlone()
    {
        var folder = Path.Combine(
            Path.GetTempPath(), "lego2stl-threading-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, "pistola.csv");
        File.WriteAllText(path, "Id;Part;BrickLink colour;Colour;RGB;Quantity\n1;32523;11;Black;#05131D;4\n");

        return new RunSettings
        {
            Kind = InputKind.PartsList,
            InputPath = path,
            Stages = RunStages.Shapes,
            Offline = true,
            LDrawCache = Path.Combine(folder, "no-library"),
            OutputDirectory = folder,
        };
    }

    private static object Facts(CataloguePartViewModel part) => new
    {
        part.PartNumber,
        part.ColorName,
        part.Quantity,
        part.Size,
        part.Title,
        part.HasOpenEdges,
        part.HasThinFeatures,
    };

    private static string ARunFolder()
    {
        var folder = Path.Combine(
            Path.GetTempPath(), "lego2stl-page-" + Guid.NewGuid().ToString("N"), "pistola");

        Directory.CreateDirectory(folder);
        return folder;
    }

    private static RunManifest ARecordOf(RunLayout layout) => new()
    {
        Status = RunStatus.Complete,
        StartedAt = new DateTimeOffset(2026, 8, 26, 9, 30, 0, TimeSpan.Zero),
        FinishedAt = new DateTimeOffset(2026, 8, 26, 9, 34, 0, TimeSpan.Zero),
        CommandLine = "lego2stl build pistola.csv",
        EntryCount = 3,
        TotalPieces = 24,
        ShapeCount = 3,
        Parts =
        [
            new ManifestPart(1, "32523", 11, "Black", "#05131D", 4, "a black one", "20 x 10 x 5 mm", true, 0, 8),
            new ManifestPart(2, "3705", 5, "Red", "#C91A09", 12, "a red one", "30 x 8 x 8 mm", false, 14, 0.3),
            new ManifestPart(3, "4265c", 9, "Light Gray", "#9BA19D", 8, "a grey one", "6 x 6 x 4 mm", true, 0, 2),
        ],
    };

    private static RunDocument ADocument()
    {
        var layout = RunLayout.At(ARunFolder());
        return RunDocument.From(ARecordOf(layout), layout);
    }
}

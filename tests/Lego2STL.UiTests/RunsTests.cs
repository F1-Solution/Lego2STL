using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;
using Lego2STL.Gui.Localization;
using Lego2STL.Gui.ViewModels;
using Xunit;

namespace Lego2STL.UiTests;

/// <summary>
/// The runs that have happened, as a screen.
/// </summary>
/// <remarks>
/// Every row is read from the folder it names rather than from anything cached beside the path,
/// which is what these tests are really defending: a run deleted by hand has to grey out, and a
/// run whose record says it stopped has to say so, without the list ever holding a second
/// opinion of its own. They share a collection because they all rewrite one history file.
/// </remarks>
[Collection("the history")]
public sealed class RunsTests
{
    [AvaloniaFact]
    public async Task With_nothing_recorded_there_is_nothing_to_show()
    {
        RunIndex.ForgetEverything();

        var runs = new RunsViewModel();
        await runs.RefreshAsync();

        runs.Rows.Should().BeEmpty();
        runs.Count.Should().Be(0);
        runs.Loading.Should().BeFalse();
    }

    [AvaloniaFact]
    public async Task The_newest_run_is_at_the_top_and_each_says_how_it_ended()
    {
        RunIndex.ForgetEverything();

        var older = await ARecordedRun("pistola", RunStatus.Complete);
        var newer = await ARecordedRun("castello", RunStatus.NeedsDecision);

        var runs = new RunsViewModel();
        await runs.RefreshAsync();

        runs.Rows.Should().HaveCount(2);
        runs.Rows[0].Name.Should().Be("castello");
        runs.Rows[1].Name.Should().Be("pistola");

        runs.Rows[0].StatusText.Should().Be(Loc.Current.Text(TextKey.UiStatusNeedsDecision));
        runs.Rows[1].StatusText.Should().Be(Loc.Current.Text(TextKey.UiStatusComplete));

        runs.Rows[0].Folder.Should().Be(newer.Root);
        runs.Rows[1].Folder.Should().Be(older.Root);
    }

    [AvaloniaFact]
    public async Task A_run_still_going_says_so()
    {
        RunIndex.ForgetEverything();
        await ARecordedRun("pistola", RunStatus.Running);

        var runs = new RunsViewModel();
        await runs.RefreshAsync();

        runs.Rows.Should().ContainSingle()
            .Which.StatusText.Should().Be(Loc.Current.Text(TextKey.UiStatusRunning));
    }

    [AvaloniaFact]
    public async Task A_run_that_stopped_part_way_says_where_it_got_to()
    {
        RunIndex.ForgetEverything();

        var layout = await ARecordedRun(
            "pistola",
            RunStatus.Stopped,
            stage: new ManifestStage(RunStage.BuildingShapes, 38, 91));

        var runs = new RunsViewModel();
        await runs.RefreshAsync();

        var row = runs.Rows.Should().ContainSingle().Subject;

        row.Summary.Should().Contain("38").And.Contain("91");
        row.Folder.Should().Be(layout.Root);
    }

    [AvaloniaFact]
    public async Task A_run_whose_folder_has_gone_is_greyed_and_can_only_be_forgotten()
    {
        RunIndex.ForgetEverything();

        var layout = await ARecordedRun("pistola", RunStatus.Complete);
        Directory.Delete(layout.Root, recursive: true);

        var runs = new RunsViewModel();
        await runs.RefreshAsync();

        var row = runs.Rows.Should().ContainSingle().Subject;

        row.Missing.Should().BeTrue();
        row.StatusText.Should().Be(Loc.Current.Text(TextKey.UiFolderMissing));
        row.OpenCommand.CanExecute(null).Should().BeFalse("there is nothing left to open");
        row.ForgetCommand.CanExecute(null).Should().BeTrue();
    }

    [AvaloniaFact]
    public async Task Forgetting_one_run_drops_it_from_the_list_and_from_the_history()
    {
        RunIndex.ForgetEverything();

        await ARecordedRun("pistola", RunStatus.Complete);
        await ARecordedRun("castello", RunStatus.Complete);

        var runs = new RunsViewModel();
        await runs.RefreshAsync();

        runs.Rows.First(row => row.Name == "castello").ForgetCommand.Execute(null);

        runs.Rows.Should().ContainSingle().Which.Name.Should().Be("pistola");
        RunIndex.Read().Should().ContainSingle();
    }

    [AvaloniaFact]
    public async Task Forgetting_everything_empties_both()
    {
        RunIndex.ForgetEverything();

        await ARecordedRun("pistola", RunStatus.Complete);
        await ARecordedRun("castello", RunStatus.Complete);

        var runs = new RunsViewModel();
        await runs.RefreshAsync();

        runs.ForgetEverythingCommand.Execute(null);

        runs.Rows.Should().BeEmpty();
        RunIndex.Read().Should().BeEmpty();
    }

    [AvaloniaFact]
    public async Task Opening_a_row_asks_for_that_folder()
    {
        RunIndex.ForgetEverything();

        var layout = await ARecordedRun("pistola", RunStatus.Complete);

        var runs = new RunsViewModel();
        await runs.RefreshAsync();

        RunFolder? asked = null;
        runs.OpenRequested += (_, folder) => asked = folder;

        runs.Rows[0].OpenCommand.Execute(null);

        asked.Should().NotBeNull();
        asked!.Layout.Root.Should().Be(layout.Root);
    }

    // ---- Adopting a folder that was never recorded here --------------------------------------

    [AvaloniaFact]
    public async Task A_folder_from_another_machine_can_be_taken_in()
    {
        RunIndex.ForgetEverything();

        var layout = RunLayout.At(AFolder("pistola"));
        await File.WriteAllTextAsync(layout.PartsListPath, "id;part;colour\n");

        var runs = new RunsViewModel();
        await runs.AdoptFolderCommand.ExecuteAsync(layout.Root);

        runs.AdoptProblem.Should().BeNull();
        runs.Rows.Should().ContainSingle().Which.Name.Should().Be("pistola");
        RunIndex.Read().Should().ContainSingle();
    }

    [AvaloniaFact]
    public async Task A_folder_that_holds_no_run_is_refused_with_a_reason()
    {
        RunIndex.ForgetEverything();

        var folder = AFolder("not-a-run");

        var runs = new RunsViewModel();
        await runs.AdoptFolderCommand.ExecuteAsync(folder);

        runs.AdoptProblem.Should().NotBeNullOrWhiteSpace();
        runs.Rows.Should().BeEmpty("an empty row would be worse than a refusal");
        RunIndex.Read().Should().BeEmpty();
    }

    [AvaloniaFact]
    public async Task Taking_in_a_folder_that_is_already_known_does_not_double_it()
    {
        RunIndex.ForgetEverything();

        var layout = await ARecordedRun("pistola", RunStatus.Complete);

        var runs = new RunsViewModel();
        await runs.RefreshAsync();
        await runs.AdoptFolderCommand.ExecuteAsync(layout.Root);

        runs.Rows.Should().ContainSingle();
        RunIndex.Read().Should().ContainSingle();
    }

    private static string AFolder(string name)
    {
        var folder = Path.Combine(
            Path.GetTempPath(), "lego2stl-runs-" + Guid.NewGuid().ToString("N"), name);

        Directory.CreateDirectory(folder);
        return folder;
    }

    private static async Task<RunLayout> ARecordedRun(
        string name, RunStatus status, ManifestStage? stage = null)
    {
        var layout = RunLayout.At(AFolder(name));

        await RunManifest.WriteAsync(layout, new RunManifest
        {
            Status = status,
            StartedAt = new DateTimeOffset(2026, 8, 26, 9, 30, 0, TimeSpan.Zero),
            FinishedAt = status == RunStatus.Running
                ? null
                : new DateTimeOffset(2026, 8, 26, 9, 34, 0, TimeSpan.Zero),
            CommandLine = "lego2stl build " + name + ".csv",
            LastStage = stage,
            EntryCount = 3,
            TotalPieces = 24,
            ShapeCount = 3,
        });

        await File.WriteAllTextAsync(layout.PartsListPath, "id;part;colour\n");

        RunIndex.Record(layout);
        return layout;
    }
}

/// <summary>Keeps these off each other's history, which is one file they all rewrite.</summary>
[CollectionDefinition("the history")]
public sealed class HistoryCollection;

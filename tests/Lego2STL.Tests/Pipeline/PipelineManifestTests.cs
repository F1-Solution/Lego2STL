using FluentAssertions;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;
using Lego2STL.Tests.Run;

namespace Lego2STL.Tests.Pipeline;

/// <summary>
/// What a run leaves behind about itself, at each of the ways a run can end.
/// </summary>
/// <remarks>
/// These run the real pipeline rather than a stand-in, because the claim being defended is
/// about where the writes sit in <c>RunAsync</c> - one write per ending, and never two - which a
/// stand-in could not get wrong. They stay offline and need no recogniser by starting from a
/// parts list and pointing the shape library at an empty folder with the network refused, so
/// every part fails for a reason the pipeline is meant to survive.
/// </remarks>
[Collection(AppDataFolder.Name)]
public sealed class PipelineManifestTests
{
    [Fact]
    public async Task A_run_that_was_only_asked_for_a_parts_list_is_recorded_as_complete()
    {
        using var settingsFolder = new AppDataFolder();
        var settings = AListToBuildFrom() with { Stages = RunStages.PartsListOnly };

        var outcome = await new PipelineRunner().RunAsync(settings);

        outcome.Result.Should().Be(RunResult.Complete);

        var manifest = TheRecordOf(outcome);

        manifest.Status.Should().Be(RunStatus.Complete);
        manifest.FinishedAt.Should().NotBeNull();
        manifest.EntryCount.Should().Be(3);
        manifest.CommandLine.Should().Contain("--csv-only");
    }

    [Fact]
    public async Task A_run_whose_parts_all_failed_is_recorded_as_needing_a_decision()
    {
        using var settingsFolder = new AppDataFolder();
        var settings = AListToBuildFrom() with { Stages = RunStages.Shapes };

        var outcome = await new PipelineRunner().RunAsync(settings);

        outcome.Result.Should().Be(RunResult.Unverified);

        var manifest = TheRecordOf(outcome);

        manifest.Status.Should().Be(RunStatus.NeedsDecision);
        manifest.ShapeCount.Should().Be(0);
        manifest.Failed.Should().HaveCount(3);
        manifest.Failed.Should().OnlyContain(failure => failure.Reason.Length > 0);
    }

    [Fact]
    public async Task A_run_that_never_started_names_no_folder_and_records_nothing()
    {
        using var settingsFolder = new AppDataFolder();
        var missing = Path.Combine(APretendRun.TempFolder(), "not-here.csv");
        var settings = AListToBuildFrom() with { InputPath = missing };

        var outcome = await new PipelineRunner().RunAsync(settings);

        outcome.Result.Should().Be(RunResult.Failed);
        outcome.Layout.Should().BeNull();

        var wouldHaveBeen = RunLayout.Plan(settings)!;

        Directory.Exists(wouldHaveBeen.Root).Should().BeFalse(
            "nothing has started, so there is nothing to record and no folder to record it in");
    }

    [Fact]
    public async Task A_run_that_was_cancelled_is_recorded_as_stopped_where_it_stopped()
    {
        using var settingsFolder = new AppDataFolder();
        var settings = AListToBuildFrom() with { Stages = RunStages.Shapes };
        var layout = RunLayout.Plan(settings)!;

        using var stop = new CancellationTokenSource();
        var watcher = new Watcher(progress =>
        {
            if (progress.Stage == RunStage.BuildingShapes && progress.Completed == 1)
            {
                stop.Cancel();
            }
        });

        var run = async () => await new PipelineRunner(null, watcher).RunAsync(settings, stop.Token);

        await run.Should().ThrowAsync<OperationCanceledException>();

        var (manifest, state) = RunManifest.Read(layout.ManifestPath);

        state.Should().Be(ManifestState.Present);
        manifest!.Status.Should().Be(RunStatus.Stopped, "a cancelled run must not read 'running' for ever");
        manifest.FinishedAt.Should().NotBeNull();
        manifest.LastStage.Should().NotBeNull();
        manifest.LastStage!.Stage.Should().Be(RunStage.BuildingShapes);
        manifest.LastStage.Total.Should().Be(3);
    }

    [Fact]
    public async Task While_a_run_is_still_going_its_record_says_it_is_running()
    {
        using var settingsFolder = new AppDataFolder();
        var settings = AListToBuildFrom() with { Stages = RunStages.Shapes };
        var layout = RunLayout.Plan(settings)!;

        RunStatus? seen = null;

        var watcher = new Watcher(progress =>
        {
            if (progress.Stage == RunStage.BuildingShapes && seen is null)
            {
                seen = RunManifest.Read(layout.ManifestPath).Manifest?.Status;
            }
        });

        await new PipelineRunner(null, watcher).RunAsync(settings);

        seen.Should().Be(RunStatus.Running, "the folder has to be real from the moment the run starts");
    }

    [Fact]
    public async Task The_pipeline_records_the_run_and_leaves_the_history_alone()
    {
        using var settingsFolder = new AppDataFolder();
        var settings = AListToBuildFrom() with { Stages = RunStages.PartsListOnly };

        await new PipelineRunner().RunAsync(settings);

        RunIndex.Read().Should().BeEmpty(
            "the pipeline writes inside the folder it was given and nowhere else");
        File.Exists(RunIndex.FilePath).Should().BeFalse();
    }

    /// <summary>
    /// A parts list on a disk, with the shape library pointed at nothing and the network
    /// refused, so a shapes run reaches its failure path without leaving the machine.
    /// </summary>
    private static RunSettings AListToBuildFrom()
    {
        var folder = APretendRun.TempFolder("pistola");
        var path = Path.Combine(folder, "pistola.csv");

        File.WriteAllText(path, PartsListCsv.Write(APretendRun.AList()));

        return new RunSettings
        {
            Kind = InputKind.PartsList,
            InputPath = path,
            Stages = RunStages.PartsListOnly,
            Offline = true,
            LDrawCache = Path.Combine(folder, "no-library"),
        };
    }

    private static RunManifest TheRecordOf(RunOutcome outcome)
    {
        var (manifest, state) = RunManifest.Read(outcome.Layout!.ManifestPath);

        state.Should().Be(ManifestState.Present);
        return manifest!;
    }

    private sealed class Watcher(Action<RunProgress> look) : IProgress<RunProgress>
    {
        public void Report(RunProgress value) => look(value);
    }
}

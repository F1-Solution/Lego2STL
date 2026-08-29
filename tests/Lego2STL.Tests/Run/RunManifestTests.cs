using FluentAssertions;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;

namespace Lego2STL.Tests.Run;

/// <summary>
/// What a run records about itself, and how carefully it is read back.
/// </summary>
/// <remarks>
/// This file is a file other programs may come to read, so its shape is pinned here rather
/// than left to whatever the serialiser happens to do. Reading is deliberately forgiving: a
/// manifest that will not parse must cost a history row and nothing else, because the folder
/// beside it still holds everything the run produced.
/// </remarks>
public sealed class RunManifestTests
{
    private static RunLayout ARunFolder() => RunLayout.At(APretendRun.TempFolder());

    [Fact]
    public async Task Everything_a_run_produced_survives_a_trip_through_the_file()
    {
        var layout = ARunFolder();
        var written = RunManifest.From(
            APretendRun.Complete(layout), APretendRun.Started, APretendRun.Finished, null);

        await RunManifest.WriteAsync(layout, written);
        var (read, state) = RunManifest.Read(layout.ManifestPath);

        state.Should().Be(ManifestState.Present);
        read.Should().BeEquivalentTo(written);
    }

    /// <summary>
    /// An entry that could not be read is recorded in the language the run was made in.
    /// </summary>
    /// <remarks>
    /// It used to be built by hand as "page {0} at {1}: {2}" and so arrived in English however
    /// the run had been asked to speak, which is what put a lone English sentence in the middle
    /// of an Italian window. The wording already existed in both languages; nothing was using it.
    /// </remarks>
    [Theory]
    [InlineData(DisplayLanguage.Italian, "pagina 372 in")]
    [InlineData(DisplayLanguage.English, "page 372 at")]
    public void An_unread_entry_is_worded_in_the_language_of_the_run(
        DisplayLanguage language, string expected)
    {
        var layout = ARunFolder();

        var manifest = RunManifest.From(
            APretendRun.WithAnUnreadEntry(layout, language),
            APretendRun.Started,
            APretendRun.Finished,
            null);

        manifest.Unread.Should().ContainSingle().Which.Should().StartWith(expected);
    }

    /// <summary>Every plate is recorded with its colour code, which is what finds it again.</summary>
    [Fact]
    public void The_plates_a_run_wrote_are_recorded_with_the_colour_that_went_on_them()
    {
        var layout = ARunFolder();

        var manifest = RunManifest.From(
            APretendRun.Complete(layout), APretendRun.Started, APretendRun.Finished, null);

        manifest.Plates.Should().HaveCount(manifest.PlateCount);
        manifest.Plates.Should().OnlyContain(plate => plate.FileName.EndsWith(".3mf"));
    }

    [Fact]
    public void A_run_that_has_only_just_started_records_that_and_no_more()
    {
        var manifest = RunManifest.Starting(APretendRun.ASetting(), APretendRun.Started);

        manifest.Version.Should().Be(RunManifest.CurrentVersion);
        manifest.Status.Should().Be(RunStatus.Running);
        manifest.StartedAt.Should().Be(APretendRun.Started);
        manifest.FinishedAt.Should().BeNull();
        manifest.Parts.Should().BeEmpty();
        manifest.EntryCount.Should().Be(0);
        manifest.CommandLine.Should().StartWith("lego2stl build");
    }

    [Theory]
    [InlineData(RunResult.Complete, RunStatus.Complete)]
    [InlineData(RunResult.Unverified, RunStatus.NeedsDecision)]
    [InlineData(RunResult.Failed, RunStatus.Failed)]
    public void How_a_run_ended_becomes_the_status_it_is_recorded_under(
        RunResult result, RunStatus expected)
    {
        var layout = ARunFolder();

        var outcome = APretendRun.Complete(layout) with
        {
            Result = result,
            Error = result == RunResult.Failed ? "something went wrong" : null,
        };

        RunManifest.From(outcome, APretendRun.Started, APretendRun.Finished, null)
            .Status.Should().Be(expected);
    }

    [Fact]
    public void A_run_that_was_stopped_records_where_it_had_got_to()
    {
        var manifest = RunManifest.Stopped(
            APretendRun.ASetting(),
            APretendRun.Started,
            APretendRun.Finished,
            new RunProgress(RunStage.BuildingShapes, 38, 91));

        manifest.Status.Should().Be(RunStatus.Stopped);
        manifest.LastStage.Should().Be(new ManifestStage(RunStage.BuildingShapes, 38, 91));
    }

    [Fact]
    public void What_each_shape_measured_is_written_down_while_it_is_still_known()
    {
        var layout = ARunFolder();

        var manifest = RunManifest.From(
            APretendRun.Complete(layout), APretendRun.Started, APretendRun.Finished, null);

        var closed = manifest.Parts.Single(p => p.Part == "32523");
        closed.IsClosed.Should().BeTrue();
        closed.OpenEdgeCount.Should().Be(0);
        closed.Size.Should().NotBeNullOrWhiteSpace();
        closed.Quantity.Should().Be(4);
        closed.Color.Should().Be("Black");

        var open = manifest.Parts.Single(p => p.Part == "3705");
        open.IsClosed.Should().BeFalse();
        open.OpenEdgeCount.Should().Be(14);
        open.ThinnestSpanMm.Should().BeLessThan(0.8);
    }

    /// <summary>A part with no shape is still a part, and says so by measuring nothing.</summary>
    [Fact]
    public void A_part_that_produced_no_shape_records_no_measurements()
    {
        var layout = ARunFolder();

        var manifest = RunManifest.From(
            APretendRun.NeedsADecision(layout), APretendRun.Started, APretendRun.Finished, null);

        var missing = manifest.Parts.Single(p => p.Part == "3705");
        missing.IsClosed.Should().BeNull();
        missing.OpenEdgeCount.Should().BeNull();
        missing.ThinnestSpanMm.Should().BeNull();

        manifest.Failed.Should().ContainSingle()
            .Which.Should().Be(new ManifestFailure("3705", "no shape file for this part number"));
    }

    /// <summary>
    /// The point of writing the command down is that it can be pasted somewhere, and a secret
    /// must not travel with it - into a folder that gets copied, archived or shared.
    /// </summary>
    [Fact]
    public async Task A_key_is_never_what_is_written_down()
    {
        var layout = ARunFolder();
        var outcome = APretendRun.Complete(layout, apiKey: "a-real-looking-secret");

        await RunManifest.WriteAsync(
            layout, RunManifest.From(outcome, APretendRun.Started, APretendRun.Finished, null));

        var onDisk = await File.ReadAllTextAsync(layout.ManifestPath);
        onDisk.Should().NotContain("a-real-looking-secret").And.Contain(RunSettings.MaskedApiKey);

        var (read, _) = RunManifest.Read(layout.ManifestPath);
        read!.Settings.ToSettings().ApiKey.Should()
            .BeNull("a placeholder is not a key and must never be used as one");
    }

    [Fact]
    public async Task The_status_is_written_as_a_word_rather_than_a_number()
    {
        var layout = ARunFolder();
        var outcome = APretendRun.NeedsADecision(layout);

        await RunManifest.WriteAsync(
            layout, RunManifest.From(outcome, APretendRun.Started, APretendRun.Finished, null));

        var onDisk = await File.ReadAllTextAsync(layout.ManifestPath);
        onDisk.Should().Contain("\"status\": \"needs-decision\"").And.Contain("\"version\": 1");
    }

    [Fact]
    public void A_folder_with_no_manifest_reads_as_having_none()
    {
        RunManifest.Read(Path.Combine(APretendRun.TempFolder(), "run.json"))
            .Should().Be(((RunManifest?)null, ManifestState.Missing));
    }

    /// <summary>Following the precedent that a file which will not read is treated as no file.</summary>
    [Theory]
    [InlineData("{")]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("null")]
    public async Task A_manifest_that_will_not_read_is_treated_as_missing(string content)
    {
        var layout = ARunFolder();
        await File.WriteAllTextAsync(layout.ManifestPath, content);

        var (read, state) = RunManifest.Read(layout.ManifestPath);

        state.Should().Be(ManifestState.Missing);
        read.Should().BeNull();
    }

    [Fact]
    public async Task A_manifest_from_a_newer_build_says_so_and_still_gives_up_what_it_can()
    {
        var layout = ARunFolder();
        await File.WriteAllTextAsync(
            layout.ManifestPath,
            """{ "version": 99, "status": "complete", "entryCount": 7, "commandLine": "lego2stl build x.csv" }""");

        var (read, state) = RunManifest.Read(layout.ManifestPath);

        state.Should().Be(ManifestState.Newer);
        read!.Status.Should().Be(RunStatus.Complete);
        read.EntryCount.Should().Be(7);
    }

    /// <summary>Not being able to record a run is never worth interrupting it over.</summary>
    [Fact]
    public async Task A_manifest_that_cannot_be_written_does_not_stop_anything()
    {
        // A file where the run folder should be, so the folder cannot be made at all.
        var inTheWay = Path.Combine(APretendRun.TempFolder(), "not-a-folder");
        await File.WriteAllTextAsync(inTheWay, "in the way");

        var write = async () => await RunManifest.WriteAsync(
            RunLayout.At(inTheWay),
            RunManifest.Starting(APretendRun.ASetting(), APretendRun.Started));

        await write.Should().NotThrowAsync();
    }
}

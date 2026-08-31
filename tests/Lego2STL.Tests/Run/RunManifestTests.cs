using System.Globalization;
using FluentAssertions;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Rebrickable;
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
    /// An entry that could not be read is worded when it is shown, not when it is recorded.
    /// </summary>
    /// <remarks>
    /// It used to be a finished sentence, built in whatever language the run spoke, which put a
    /// lone English sentence in the middle of an Italian window - and left nothing for anything
    /// to ask a question about. A run recorded in one language now shows in the other, which is
    /// the same bug made impossible rather than fixed.
    /// </remarks>
    [Theory]
    [InlineData(DisplayLanguage.Italian, "pagina 372 in")]
    [InlineData(DisplayLanguage.English, "page 372 at")]
    public void An_unread_entry_is_worded_when_it_is_shown(
        DisplayLanguage language, string expected)
    {
        var layout = ARunFolder();

        var manifest = RunManifest.From(
            APretendRun.WithAnUnreadEntry(layout, DisplayLanguage.English),
            APretendRun.Started,
            APretendRun.Finished,
            null);

        RunDocument.From(manifest, layout)
            .UnreadTextIn(Strings.For(language))
            .Should().ContainSingle().Which.Should().StartWith(expected);
    }

    /// <summary>
    /// An entry that could not be read is kept with its page and its region, not as a sentence.
    /// </summary>
    /// <remarks>
    /// Kept as a sentence, nothing could show the piece of page in question or ask about it -
    /// and the sentence was formatted in whatever language the run spoke, so a window switched
    /// to the other one showed the old words for ever.
    /// </remarks>
    [Fact]
    public async Task An_entry_that_could_not_be_read_keeps_its_page_and_its_region()
    {
        var layout = ARunFolder();
        var language = DisplayLanguage.Italian;

        var manifest = RunManifest.From(
            APretendRun.WithAnUnreadEntry(layout, language),
            APretendRun.Started,
            APretendRun.Finished,
            null);

        var unread = manifest.Unread.Should().ContainSingle().Subject;

        unread.Page.Should().BePositive();
        unread.Bounds.Should().NotBeNullOrWhiteSpace();
        unread.RawText.Should().NotBeNull();

        // Through the file as well, because that is where the window reads it from.
        await RunManifest.WriteAsync(layout, manifest);
        var (readBack, state) = RunManifest.Read(layout.ManifestPath);

        state.Should().Be(ManifestState.Present);
        readBack!.Unread.Should().ContainSingle().Which.Should().Be(unread);

        var document = RunDocument.From(manifest, layout);

        document.Unread.Should().ContainSingle();
        document.UnreadText.Should().ContainSingle().Which.Should()
            .Contain(unread.Page.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// A record written before unread entries were kept as data still opens, whole.
    /// </summary>
    /// <remarks>
    /// The cost of refusing one is not the sentences: a record that will not parse is read as no
    /// record at all, so the run loses its status, its parts and its plates too and falls back to
    /// "a folder from before runs recorded themselves". An old sentence is therefore kept as its
    /// own text, shown as it was written, and never asked about - it has no region to show.
    /// </remarks>
    [Fact]
    public async Task A_record_that_kept_its_unread_entries_as_sentences_still_opens()
    {
        var layout = ARunFolder();

        await RunManifest.WriteAsync(layout, RunManifest.From(
            APretendRun.Complete(layout), APretendRun.Started, APretendRun.Finished, null));

        const string Sentence = "page 372 at (444,530)-(512,566): could not be read";

        File.WriteAllText(
            layout.ManifestPath,
            File.ReadAllText(layout.ManifestPath)
                .Replace(
                    "\"unread\": []",
                    $"\"unread\": [ \"{Sentence}\" ]",
                    StringComparison.Ordinal));

        var (read, state) = RunManifest.Read(layout.ManifestPath);

        state.Should().Be(ManifestState.Present, "one unreadable field must not cost the whole run");
        read!.Parts.Should().NotBeEmpty();
        read.Plates.Should().NotBeEmpty();

        var unread = read.Unread.Should().ContainSingle().Subject;

        unread.CanBeAskedAbout.Should().BeFalse("there is no region to show anyone");

        RunDocument.From(read, layout).UnreadText
            .Should().ContainSingle().Which.Should().Be(Sentence);
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

    /// <summary>
    /// Both ways a shape can fail to be closed are recorded, because they are different faults.
    /// </summary>
    /// <remarks>
    /// Only the open-edge count used to be kept, so a part with no holes whose surfaces pass
    /// through each other was recorded as indistinguishable from a part full of holes - and the
    /// catalogue told 19 parts of run 6324712 they had open edges when they had none.
    /// </remarks>
    [Fact]
    public void A_shape_records_both_kinds_of_fault()
    {
        var layout = ARunFolder();

        var manifest = RunManifest.From(
            APretendRun.Complete(layout), APretendRun.Started, APretendRun.Finished, null);

        var measured = manifest.Parts.Where(part => part.IsClosed is not null).ToList();

        measured.Should().NotBeEmpty();
        measured.Should().OnlyContain(part => part.OverusedEdgeCount != null,
            "a shape that was measured was measured for both");
    }

    /// <summary>
    /// Why a part was not built is kept on the record, so a run reopened later says the same
    /// thing without the parts database being present.
    /// </summary>
    [Fact]
    public void A_part_that_was_not_built_says_why_on_the_record()
    {
        var layout = ARunFolder();

        var outcome = APretendRun.Complete(layout) with
        {
            PartFacts = new Dictionary<string, PartFact>(StringComparer.OrdinalIgnoreCase)
            {
                ["32523"] = new("Tubes and Hoses", "Rubber"),
            },
            NotPrinted = ["32523"],
        };

        var manifest = RunManifest.From(outcome, APretendRun.Started, APretendRun.Finished, null);
        var document = RunDocument.From(manifest, layout);

        var hose = document.Parts.Single(p => p.PartNumber == "32523");

        hose.Printability.Should().Be("material");
        hose.NotPrinted.Should().BeTrue();
        hose.NotPrintedForMaterial.Should().BeTrue();

        document.Parts.Where(p => p.PartNumber != "32523")
            .Should().OnlyContain(p => p.NotPrinted == false);
    }
}

using FluentAssertions;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;

namespace Lego2STL.Tests.Run;

/// <summary>
/// What a run amounts to on screen, whether it is happening now or happened weeks ago.
/// </summary>
/// <remarks>
/// The first test here is the one this whole design rests on. Everything else in these files -
/// the manifest's own lifecycle, the index holding nothing but paths - is in service of a
/// single claim: that a run being watched and the same run reopened are the same thing, because
/// they are computed by the same function over the same record. If that claim ever stops
/// holding, the two have drifted and nothing else about a run's page can be trusted.
/// </remarks>
public sealed class RunDocumentTests
{
    private static RunLayout ARunFolder() => RunLayout.At(APretendRun.TempFolder());

    [Fact]
    public async Task A_live_run_and_the_same_run_reopened_are_the_same_document()
    {
        var layout = ARunFolder();

        var live = RunManifest.From(
            APretendRun.Complete(layout), APretendRun.Started, APretendRun.Finished, null);

        await RunManifest.WriteAsync(layout, live);
        var (reread, state) = RunManifest.Read(layout.ManifestPath);

        state.Should().Be(ManifestState.Present);

        RunDocument.From(reread!, layout).Should().BeEquivalentTo(
            RunDocument.From(live, layout),
            "a run being watched and the same run reopened have to be the same document");
    }

    [Fact]
    public void What_a_run_produced_reaches_the_document()
    {
        var layout = ARunFolder();
        var manifest = RunManifest.From(
            APretendRun.Complete(layout), APretendRun.Started, APretendRun.Finished, null);

        var document = RunDocument.From(manifest, layout);

        document.Folder.Should().Be(layout.Root);
        document.Name.Should().Be(layout.Name);
        document.Status.Should().Be(RunStatus.Complete);
        document.ManifestKnown.Should().BeTrue();
        document.EntryCount.Should().Be(3);
        document.TotalPieces.Should().Be(24);
        document.ShapeCount.Should().Be(3);
        document.PlateCount.Should().Be(2);
        document.Parts.Should().HaveCount(3);
        document.CommandLine.Should().StartWith("lego2stl build");
        document.Settings!.Clearance.Should().Be(0.15);
        document.Notes.Should().ContainSingle();
    }

    /// <summary>Every path a run's page offers has to be inside that run's folder.</summary>
    [Fact]
    public void Everything_the_page_can_open_is_inside_the_folder()
    {
        var layout = ARunFolder();
        var document = RunDocument.From(
            RunManifest.Starting(APretendRun.ASetting(), APretendRun.Started), layout);

        foreach (var path in new[]
        {
            document.PartsListPath, document.StlDirectory, document.PlateDirectory,
            document.ReportPath, document.LogPath,
        })
        {
            path.Should().StartWith(layout.Root);
        }
    }

    /// <summary>
    /// A run at second one, and a run that was killed mid-flight, are the same state on disk,
    /// so it is a first-class case rather than an edge one.
    /// </summary>
    [Fact]
    public void A_run_that_has_only_just_started_is_a_document_too()
    {
        var layout = ARunFolder();
        var document = RunDocument.From(
            RunManifest.Starting(APretendRun.ASetting(), APretendRun.Started), layout);

        document.Status.Should().Be(RunStatus.Running);
        document.IsRunning.Should().BeTrue();
        document.Parts.Should().BeEmpty();
        document.EntryCount.Should().Be(0);
        document.Progress.Should().Be(0);
        document.FinishedAt.Should().BeNull();
    }

    /// <summary>
    /// The bar shows where a run actually stopped. Reporting a run that could not be verified
    /// at 100% is the largest gap there has been between the window and the command line.
    /// </summary>
    [Fact]
    public void A_run_that_stopped_part_way_says_where_rather_than_showing_a_full_bar()
    {
        var layout = ARunFolder();
        var manifest = RunManifest.Stopped(
            APretendRun.ASetting(),
            APretendRun.Started,
            APretendRun.Finished,
            new RunProgress(RunStage.BuildingShapes, 38, 91));

        var document = RunDocument.From(manifest, layout);

        document.Status.Should().Be(RunStatus.Stopped);
        document.Progress.Should().BeGreaterThan(0).And.BeLessThan(1);
        document.LastStage.Should().Be(new ManifestStage(RunStage.BuildingShapes, 38, 91));
    }

    [Fact]
    public void A_completed_run_is_a_full_bar()
    {
        var layout = ARunFolder();

        RunDocument.From(
                RunManifest.From(
                    APretendRun.Complete(layout), APretendRun.Started, APretendRun.Finished, null),
                layout)
            .Progress.Should().Be(1);
    }

    [Fact]
    public void A_run_needing_a_decision_carries_what_the_decision_is_about()
    {
        var layout = ARunFolder();
        var manifest = RunManifest.From(
            APretendRun.NeedsADecision(layout), APretendRun.Started, APretendRun.Finished, null);

        var document = RunDocument.From(manifest, layout);

        document.Status.Should().Be(RunStatus.NeedsDecision);
        document.NeedsDecision.Should().BeTrue();
        document.Failed.Should().ContainSingle().Which.Part.Should().Be("3705");
    }

    [Fact]
    public void A_run_recorded_by_a_newer_build_says_so()
    {
        var layout = ARunFolder();
        var document = RunDocument.From(
            RunManifest.Starting(APretendRun.ASetting(), APretendRun.Started) with { Version = 99 },
            layout);

        document.FromNewerBuild.Should().BeTrue();
    }

    // ---- Folders written before manifests existed ----------------------------------------

    [Fact]
    public void A_folder_from_before_the_record_existed_still_lists_its_parts()
    {
        var layout = ARunFolder();

        var document = RunDocument.WithoutManifest(layout, APretendRun.AList());

        document.ManifestKnown.Should().BeFalse();
        document.Parts.Should().HaveCount(3);
        document.EntryCount.Should().Be(3);
        document.TotalPieces.Should().Be(24);
    }

    /// <summary>
    /// The two shape warnings are unrecoverable for such a folder, and it says so by measuring
    /// nothing rather than by guessing.
    /// </summary>
    [Fact]
    public void A_folder_from_before_the_record_existed_knows_nothing_about_its_shapes()
    {
        var document = RunDocument.WithoutManifest(ARunFolder(), APretendRun.AList());

        document.Parts.Should().AllSatisfy(part =>
        {
            part.ShapeWasMeasured.Should().BeFalse();
            part.IsClosed.Should().BeNull();
            part.OpenEdgeCount.Should().BeNull();
            part.ThinnestSpanMm.Should().BeNull();
            part.HasOpenEdges.Should().BeFalse();
            part.HasThinFeatures.Should().BeFalse();
        });

        document.CommandLine.Should().BeEmpty();
        document.Settings.Should().BeNull();
    }

    [Fact]
    public void An_empty_folder_is_a_document_with_nothing_in_it()
    {
        var document = RunDocument.WithoutManifest(ARunFolder(), null);

        document.ManifestKnown.Should().BeFalse();
        document.Parts.Should().BeEmpty();
        document.EntryCount.Should().Be(0);
    }

    // ---- What a part says about its shape -------------------------------------------------

    [Fact]
    public void A_shape_with_holes_or_thin_walls_says_so()
    {
        var layout = ARunFolder();
        var document = RunDocument.From(
            RunManifest.From(
                APretendRun.Complete(layout), APretendRun.Started, APretendRun.Finished, null),
            layout);

        var closed = document.Parts.Single(p => p.PartNumber == "32523");
        closed.HasOpenEdges.Should().BeFalse();
        closed.HasThinFeatures.Should().BeFalse();
        closed.ShapeWasMeasured.Should().BeTrue();

        var rough = document.Parts.Single(p => p.PartNumber == "3705");
        rough.HasOpenEdges.Should().BeTrue();
        rough.HasThinFeatures.Should().BeTrue();
    }

    /// <summary>The threshold is one line of a common nozzle, which is what a wall needs.</summary>
    [Theory]
    [InlineData(0.3, true)]
    [InlineData(0.79, true)]
    [InlineData(0.8, false)]
    [InlineData(4.0, false)]
    public void A_wall_thinner_than_a_nozzle_can_lay_down_is_a_warning(double span, bool expected)
    {
        var part = new RunDocumentPart(
            1, "32523", 11, "Black", Rgb24.Parse("#05131D"), 1,
            Title: null, Size: null, IsClosed: true, OpenEdgeCount: 0, ThinnestSpanMm: span);

        part.HasThinFeatures.Should().Be(expected);
    }
}

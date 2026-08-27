using FluentAssertions;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Run;

namespace Lego2STL.Tests.Run;

/// <summary>
/// What a folder on a disk turns back into.
/// </summary>
/// <remarks>
/// These tests build folders by hand rather than by running anything, because the folders that
/// matter most are the awkward ones: the one written by a version that did not record itself,
/// the one whose record will not parse, the one that was deleted between being listed and being
/// opened. A reader that throws on any of those is a history list that cannot be scrolled.
/// </remarks>
public sealed class RunFolderTests
{
    [Fact]
    public async Task A_folder_with_everything_in_it_reports_everything_present()
    {
        var layout = RunLayout.At(APretendRun.TempFolder("pistola"));
        var manifest = RunManifest.From(
            APretendRun.Complete(layout), APretendRun.Started, APretendRun.Finished, null);

        await RunManifest.WriteAsync(layout, manifest);
        await File.WriteAllTextAsync(layout.PartsListPath, PartsListCsv.Write(APretendRun.AList()));
        await File.WriteAllTextAsync(layout.ReportPath, "what happened");
        await File.WriteAllTextAsync(layout.LogPath, "line one\nline two");
        Directory.CreateDirectory(layout.StlDirectory);
        await File.WriteAllTextAsync(Path.Combine(layout.StlDirectory, "32523.stl"), "solid");
        Directory.CreateDirectory(layout.PlateDirectory);
        await File.WriteAllTextAsync(Path.Combine(layout.PlateDirectory, "black-1.3mf"), "zip");

        var folder = RunFolder.Read(layout.Root);

        folder.Exists.Should().BeTrue();
        folder.State.Should().Be(ManifestState.Present);
        folder.Manifest.Should().NotBeNull();
        folder.HasPartsList.Should().BeTrue();
        folder.PartsList!.Entries.Should().HaveCount(3);
        folder.HasShapes.Should().BeTrue();
        folder.HasPlates.Should().BeTrue();
        folder.HasReport.Should().BeTrue();
        folder.HasLog.Should().BeTrue();
    }

    [Fact]
    public async Task A_folder_read_back_is_the_document_the_run_itself_would_have_shown()
    {
        var layout = RunLayout.At(APretendRun.TempFolder("pistola"));
        var manifest = RunManifest.From(
            APretendRun.Complete(layout), APretendRun.Started, APretendRun.Finished, null);

        await RunManifest.WriteAsync(layout, manifest);

        RunFolder.Read(layout.Root).ToDocument().Should().BeEquivalentTo(
            RunDocument.From(manifest, layout),
            "reopening a folder has to arrive at the same document the run did");
    }

    [Fact]
    public async Task A_folder_with_only_a_parts_list_still_lists_its_parts()
    {
        var layout = RunLayout.At(APretendRun.TempFolder("pistola"));
        await File.WriteAllTextAsync(layout.PartsListPath, PartsListCsv.Write(APretendRun.AList()));

        var folder = RunFolder.Read(layout.Root);

        folder.State.Should().Be(ManifestState.Missing);
        folder.Manifest.Should().BeNull();
        folder.HasPartsList.Should().BeTrue();
        folder.HasShapes.Should().BeFalse();

        var document = folder.ToDocument();

        document.ManifestKnown.Should().BeFalse();
        document.EntryCount.Should().Be(3);
        document.TotalPieces.Should().Be(24);
        document.Parts.Should().HaveCount(3);
        document.Parts.Should().OnlyContain(
            part => part.ShapeWasMeasured == false,
            "a folder written before runs recorded themselves knows nothing about its shapes");
    }

    [Fact]
    public async Task A_record_that_will_not_parse_is_treated_as_no_record()
    {
        var layout = RunLayout.At(APretendRun.TempFolder("pistola"));
        await File.WriteAllTextAsync(layout.ManifestPath, "{ this is not json");
        await File.WriteAllTextAsync(layout.PartsListPath, PartsListCsv.Write(APretendRun.AList()));

        var folder = RunFolder.Read(layout.Root);

        folder.State.Should().Be(ManifestState.Missing);
        folder.ToDocument().ManifestKnown.Should().BeFalse();
        folder.ToDocument().EntryCount.Should().Be(3);
    }

    [Fact]
    public async Task A_record_from_a_newer_build_is_read_and_said_to_be_newer()
    {
        var layout = RunLayout.At(APretendRun.TempFolder("pistola"));
        var manifest = RunManifest.From(
            APretendRun.Complete(layout), APretendRun.Started, APretendRun.Finished, null);

        await RunManifest.WriteAsync(layout, manifest with { Version = RunManifest.CurrentVersion + 1 });

        var folder = RunFolder.Read(layout.Root);

        folder.State.Should().Be(ManifestState.Newer);
        folder.Manifest.Should().NotBeNull();

        var document = folder.ToDocument();

        document.ManifestKnown.Should().BeTrue();
        document.FromNewerBuild.Should().BeTrue();
        document.EntryCount.Should().Be(3);
    }

    [Fact]
    public async Task A_parts_list_that_will_not_read_leaves_the_list_null_and_says_it_is_there()
    {
        var layout = RunLayout.At(APretendRun.TempFolder("pistola"));
        await File.WriteAllTextAsync(layout.PartsListPath, string.Empty);

        var folder = RunFolder.Read(layout.Root);

        folder.HasPartsList.Should().BeTrue("the file is there, whatever is in it");
        folder.PartsList.Should().BeNull();
        folder.ToDocument().EntryCount.Should().Be(0);
    }

    [Fact]
    public void A_folder_that_is_not_there_says_so_rather_than_throwing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "lego2stl-gone-" + Guid.NewGuid().ToString("N"));

        var folder = RunFolder.Read(missing);

        folder.Exists.Should().BeFalse();
        folder.State.Should().Be(ManifestState.Missing);
        folder.PartsList.Should().BeNull();
        folder.HasPartsList.Should().BeFalse();
        folder.ToDocument().Name.Should().Be(Path.GetFileName(missing));
    }

    [Fact]
    public void A_log_that_is_not_there_reads_as_nothing_said()
    {
        var missing = Path.Combine(APretendRun.TempFolder(), "run.log");

        RunLogFile.Read(missing).Should().BeEmpty();
    }

    [Fact]
    public async Task A_log_reads_back_as_the_lines_that_were_written()
    {
        var path = Path.Combine(APretendRun.TempFolder(), "run.log");
        await File.WriteAllLinesAsync(path, ["reading the parts list", "building shapes"]);

        RunLogFile.Read(path).Should().Equal("reading the parts list", "building shapes");
    }

    [Fact]
    public void No_log_was_asked_for_so_none_is_opened()
    {
        RunLogFile.Open(null).Should().BeNull();
        RunLogFile.Open("   ").Should().BeNull();
    }

    [Fact]
    public void A_log_is_opened_in_a_folder_that_does_not_exist_yet()
    {
        var path = Path.Combine(APretendRun.TempFolder(), "not-yet", "run.log");

        using (var log = RunLogFile.Open(path))
        {
            log.Should().NotBeNull();
            log!.WriteLine("something happened");
        }

        RunLogFile.Read(path).Should().Equal("something happened");
    }
}

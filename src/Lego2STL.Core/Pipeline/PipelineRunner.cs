using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.LDraw;
using Lego2STL.Core.Ocr;
using Lego2STL.Core.Pdf;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;

namespace Lego2STL.Core.Pipeline;

/// <summary>
/// Runs the whole thing, from whichever start to whichever finish.
/// </summary>
/// <remarks>
/// <para>
/// One implementation for both front ends. The terminal and the window differ in how they ask
/// and how they show, and in nothing else: they build the same <see cref="RunSettings"/> and
/// call this. That is what makes "everything the command line can do" a fact about the code
/// rather than a promise to keep in step by hand.
/// </para>
/// <para>
/// Nothing here writes to a console or knows what a window is. It says what is happening
/// through a callback and how far along it is through another, and the caller decides whether
/// that becomes a line of text or a moving bar.
/// </para>
/// </remarks>
public sealed class PipelineRunner
{
    private readonly Action<string> _log;
    private readonly IProgress<RunProgress>? _progress;

    public PipelineRunner(Action<string>? log = null, IProgress<RunProgress>? progress = null)
    {
        _log = log ?? (_ => { });
        _progress = progress;
    }

    public async Task<RunOutcome> RunAsync(RunSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var problems = settings.Problems();
        if (problems.Count > 0)
        {
            return RunOutcome.Failure(settings, string.Join(" ", problems));
        }

        Report(RunStage.Starting);

        try
        {
            return await RunCoreAsync(settings, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                       or IOException
                                       or FormatException
                                       or OcrUnavailableException
                                       or InvalidDataException
                                       or PlatformNotSupportedException)
        {
            // These all carry a message written for whoever is reading it; anything else is a
            // fault in the tool and is left to travel with its stack trace.
            return RunOutcome.Failure(settings, ex.Message);
        }
    }

    private async Task<RunOutcome> RunCoreAsync(RunSettings settings, CancellationToken cancellationToken)
    {
        var words = Strings.For(settings.Language);
        var notes = new List<string>();

        var (list, layout, unread) = settings.Kind switch
        {
            InputKind.Document => await FromDocumentAsync(settings, notes, cancellationToken)
                .ConfigureAwait(false),
            InputKind.SetNumber => await FromSetAsync(settings, notes, cancellationToken)
                .ConfigureAwait(false),
            _ => await FromPartsListAsync(settings, notes, cancellationToken).ConfigureAwait(false),
        };

        // Writing the parts list is worth doing even when part of it could not be read: it is
        // the thing that can be corrected by hand and fed straight back in.
        Report(RunStage.WritingPartsList);
        await PartsListCsv.WriteFileAsync(
                layout.PartsListPath, list, settings.Delimiter, settings.Language, cancellationToken)
            .ConfigureAwait(false);

        _log(words.Format(TextKey.MsgPartsListWritten, layout.PartsListPath));

        if (unread.Count > 0)
        {
            return new RunOutcome
            {
                Result = RunResult.Unverified,
                Settings = settings,
                Layout = layout,
                PartsList = list,
                Unread = unread,
                Notes = notes,
            };
        }

        if (!settings.WantsShapes)
        {
            Report(RunStage.Finished);
            return new RunOutcome
            {
                Result = RunResult.Complete,
                Settings = settings,
                Layout = layout,
                PartsList = list,
                Notes = notes,
            };
        }

        return await BuildShapesAsync(settings, list, layout, notes, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(PartsList List, RunLayout Layout, IReadOnlyList<UnresolvedReading> Unread)>
        FromDocumentAsync(RunSettings settings, List<string> notes, CancellationToken cancellationToken)
    {
        var words = Strings.For(settings.Language);

        // Asked before the document is opened: a missing recogniser is the whole answer, and
        // saying it after a page count reads as though the reading had begun.
        if (!OcrEngines.IsAvailable)
        {
            throw new OcrUnavailableException(OcrEngines.DescribeUnavailable(words));
        }

        using var document = PdfPageImageSource.Open(settings.InputPath!);
        _log(words.Format(TextKey.MsgPagesInDocument, Path.GetFileName(settings.InputPath!), document.PageCount));

        var pages = string.IsNullOrWhiteSpace(settings.Pages)
            ? DetectCataloguePages(document, cancellationToken)
            : PageRange.Parse(settings.Pages, document.PageCount);

        if (pages.Count == 0)
        {
            throw new InvalidOperationException(words[TextKey.MsgNoCataloguePages]);
        }

        _log(words.Format(TextKey.MsgReadingPages, PageRange.Format(pages), settings.ColorScheme));

        var reader = new CatalogueReader(OcrEngines.Create(null, words), null, words);
        var read = await ReadPagesAsync(reader, document, pages, cancellationToken).ConfigureAwait(false);

        notes.AddRange(read.Notes);
        foreach (var note in read.Notes)
        {
            _log("  " + note);
        }

        var list = PartsListBuilder.Build(
            read.Entries, ColorReference.Table, settings.ColorScheme, words);
        notes.AddRange(list.Notes);

        var layout = RunLayout.For(settings.InputPath!, settings.OutputDirectory);
        layout.CreateDirectories();

        return (list, layout, read.Unresolved);
    }

    private async Task<CatalogueReadResult> ReadPagesAsync(
        CatalogueReader reader,
        PdfPageImageSource document,
        IReadOnlyList<int> pages,
        CancellationToken cancellationToken)
    {
        Report(RunStage.ReadingDocument, 0, pages.Count);

        // The reader works a page at a time internally; running it per page as well is what
        // lets the bar move rather than sitting still for the whole document.
        var entries = new List<CatalogueReading>();
        var unresolved = new List<UnresolvedReading>();
        var notes = new List<string>();

        for (var i = 0; i < pages.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Report(RunStage.ReadingDocument, i, pages.Count, $"page {pages[i]}");

            var one = await reader.ReadAsync(document, [pages[i]], cancellationToken).ConfigureAwait(false);

            entries.AddRange(one.Entries);
            unresolved.AddRange(one.Unresolved);
            notes.AddRange(one.Notes);
        }

        Report(RunStage.ReadingDocument, pages.Count, pages.Count);

        return new CatalogueReadResult(entries, unresolved, notes);
    }

    private async Task<(PartsList, RunLayout, IReadOnlyList<UnresolvedReading>)>
        FromSetAsync(RunSettings settings, List<string> notes, CancellationToken cancellationToken)
    {
        Report(RunStage.LookingUpSet);

        var list = await SetInventory.FetchAsync(
                settings.SetNumber!,
                ColorReference.Table,
                settings.IncludeSpares,
                settings.ApiKey,
                _log,
                cancellationToken)
            .ConfigureAwait(false);

        notes.AddRange(list.Notes);

        var name = RebrickableSetFolderName(settings.SetNumber!);
        var root = settings.OutputDirectory ?? Environment.CurrentDirectory;
        var layout = RunLayout.For(Path.Combine(root, name + ".csv"), null);
        layout.CreateDirectories();

        return (list, layout, []);
    }

    private async Task<(PartsList, RunLayout, IReadOnlyList<UnresolvedReading>)>
        FromPartsListAsync(RunSettings settings, List<string> notes, CancellationToken cancellationToken)
    {
        Report(RunStage.ReadingPartsList);

        var list = await PartsListCsv
            .ReadFileAsync(settings.InputPath!, cancellationToken, settings.Language)
            .ConfigureAwait(false);

        notes.AddRange(list.Notes);

        var layout = RunLayout.For(settings.InputPath!, settings.OutputDirectory);
        layout.CreateDirectories();

        return (list, layout, []);
    }

    private async Task<RunOutcome> BuildShapesAsync(
        RunSettings settings,
        PartsList list,
        RunLayout layout,
        List<string> notes,
        CancellationToken cancellationToken)
    {
        var words = Strings.For(settings.Language);

        Report(RunStage.GatheringShapes);

        using var library = new EscalatingLDrawLibrary(settings.LDrawOptions, message => _log("  " + message));
        var builder = new LDrawMeshBuilder(library);

        var options = settings.MeshOptions;
        var shapes = new Dictionary<string, IndexedMesh>(StringComparer.OrdinalIgnoreCase);
        var prepared = new List<PreparedMesh>();
        var failed = new List<FailedPart>();

        var parts = list.DistinctPartNumbers;

        for (var i = 0; i < parts.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var partNumber = parts[i];
            Report(RunStage.BuildingShapes, i, parts.Count, partNumber);

            try
            {
                var source = await builder.BuildAsync(partNumber, cancellationToken).ConfigureAwait(false);

                if (source.IsEmpty)
                {
                    failed.Add(new FailedPart(partNumber, "the shape file contains no surfaces"));
                    continue;
                }

                var ready = MeshPipeline.Prepare(source, options);
                prepared.Add(ready);
                shapes[partNumber] = ready.Mesh;

                await StlWriter.WriteFileAsync(
                        Path.Combine(layout.StlDirectory, partNumber + ".stl"),
                        ready.Mesh,
                        settings.AsciiStl,
                        partNumber,
                        cancellationToken)
                    .ConfigureAwait(false);

                _log($"  {partNumber,-9}{ready.Mesh.TriangleCount,7} {ready.DescribeSize(),-26} " +
                     (ready.Quality.IsClosed
                         ? "closed"
                         : $"{ready.Quality.OpenEdgeCount} open edge(s)"));
            }
            catch (LDrawPartNotFoundException)
            {
                failed.Add(new FailedPart(partNumber, "no shape file for this part number"));
            }
        }

        Report(RunStage.BuildingShapes, parts.Count, parts.Count);
        _log(words.Format(TextKey.MsgWroteShapes, prepared.Count, layout.StlDirectory));

        var plates = await ArrangePlatesAsync(settings, list, shapes, layout, failed, cancellationToken)
            .ConfigureAwait(false);

        Report(RunStage.WritingReport);

        var outcome = new RunOutcome
        {
            Result = failed.Count > 0 ? RunResult.Unverified : RunResult.Complete,
            Settings = settings,
            Layout = layout,
            PartsList = list,
            Shapes = prepared,
            ShapesByPart = shapes,
            Failed = failed,
            Plates = plates,
            GeometrySource = library.Description,
            Notes = notes,
        };

        await RunReport.WriteAsync(layout, outcome, cancellationToken).ConfigureAwait(false);
        _log(words.Format(TextKey.MsgReportWritten, layout.ReportPath));

        Report(RunStage.Finished);
        return outcome;
    }

    private async Task<PlateBuildResult?> ArrangePlatesAsync(
        RunSettings settings,
        PartsList list,
        IReadOnlyDictionary<string, IndexedMesh> shapes,
        RunLayout layout,
        IReadOnlyList<FailedPart> failed,
        CancellationToken cancellationToken)
    {
        var words = Strings.For(settings.Language);

        if (!settings.WantsPlates)
        {
            _log(words[TextKey.MsgNoPlatesRequested]);
            return null;
        }

        // A plate missing the parts that failed is still worth having - the pieces that did
        // come out are printable now - so it gets built. What must not happen is its looking
        // finished: the count is said here, the report names them, and the run stays
        // unverified.
        if (failed.Count > 0)
        {
            _log(words.Format(TextKey.MsgPlatesMissingParts, failed.Count));
        }

        Report(RunStage.ArrangingPlates);

        var plates = await PlateBuilder.WriteAsync(
                list, shapes, layout.PlateDirectory, settings.PackingOptions, cancellationToken)
            .ConfigureAwait(false);

        foreach (var note in plates.Skipped)
        {
            _log("  " + note);
        }

        _log(words.Format(TextKey.MsgWrotePlates, plates.Plates.Count, layout.PlateDirectory));
        return plates;
    }

    /// <summary>
    /// Which pages hold a catalogue, when no range was given: the ones with entries on them.
    /// </summary>
    private List<int> DetectCataloguePages(PdfPageImageSource document, CancellationToken cancellationToken)
    {
        var locator = new Extraction.LabelLocator();
        var found = new List<int>();

        Report(RunStage.ReadingDocument, 0, document.PageCount, "looking for the catalogue");

        for (var pageNumber = 1; pageNumber <= document.PageCount; pageNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var page = document.GetPage(pageNumber);
            if (locator.Locate(page).Count > 0)
            {
                found.Add(pageNumber);
            }
        }

        if (found.Count > 0)
        {
            _log($"Catalogue pages: {PageRange.Format(found)}");
        }

        return found;
    }

    /// <summary>Where a set's run folder goes, given its number.</summary>
    public static string RebrickableSetFolderName(string setNumber) =>
        "set-" + Rebrickable.RebrickableClient.NormaliseSetNumber(setNumber);

    private void Report(RunStage stage, int completed = 0, int total = 0, string? detail = null) =>
        _progress?.Report(new RunProgress(stage, completed, total, detail));
}

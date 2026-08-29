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

    /// <summary>What "stopped while building shapes, 38 of 91" is read from.</summary>
    private RunProgress? _lastProgress;

    public PipelineRunner(Action<string>? log = null, IProgress<RunProgress>? progress = null)
    {
        _log = log ?? (_ => { });
        _progress = progress;
    }

    /// <remarks>
    /// The one place a manifest is written, so a run's four possible endings are four states of
    /// one write rather than four scattered calls that could each be forgotten - or, worse, two
    /// of which could fire for the same run. The <c>finally</c> is what covers cancellation:
    /// without it a run that was stopped leaves a record reading "running" for ever.
    /// </remarks>
    public async Task<RunOutcome> RunAsync(RunSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var problems = settings.Problems();
        if (problems.Count > 0)
        {
            // Nothing has started and no folder has been named, so there is nothing to record.
            return RunOutcome.Failure(settings, string.Join(" ", problems));
        }

        var layout = RunLayout.Plan(settings);
        var started = DateTimeOffset.UtcNow;
        var recorded = false;

        Report(RunStage.Starting);

        if (layout is not null)
        {
            layout.CreateDirectories();
            await Record(layout, RunManifest.Starting(settings, started), cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            var outcome = await RunCoreAsync(settings, cancellationToken).ConfigureAwait(false);

            await Record(
                    outcome.Layout ?? layout,
                    RunManifest.From(outcome, started, DateTimeOffset.UtcNow, _lastProgress),
                    cancellationToken)
                .ConfigureAwait(false);

            recorded = true;
            return outcome;
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
            var failure = RunOutcome.Failure(settings, ex.Message) with { Layout = layout };

            await Record(
                    layout,
                    RunManifest.From(failure, started, DateTimeOffset.UtcNow, _lastProgress),
                    CancellationToken.None)
                .ConfigureAwait(false);

            recorded = true;
            return failure;
        }
        finally
        {
            if (!recorded)
            {
                await Record(
                        layout,
                        RunManifest.Stopped(settings, started, DateTimeOffset.UtcNow, _lastProgress),
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
    }

    private static async Task Record(RunLayout? layout, RunManifest manifest, CancellationToken cancellationToken)
    {
        if (layout is null)
        {
            return;
        }

        await RunManifest.WriteAsync(layout, manifest, cancellationToken).ConfigureAwait(false);
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

    /// <remarks>
    /// <para>
    /// Two ways to read a page, and the document decides which. Official building instructions
    /// carry a text layer, and their catalogue pages can simply be quoted from it: exact, free
    /// of any recogniser, and free of rasterising the pages at all. Documents without one -
    /// scans, and the image-only exports the pixel path was written for - go the other way,
    /// and a document can be part one and part the other without anyone having to say so.
    /// </para>
    /// <para>
    /// What a printed page yields is element numbers rather than parts and colours, so the
    /// third step is a lookup. That is the only cost of the fast path, and it is paid once per
    /// distinct number.
    /// </para>
    /// </remarks>
    private async Task<(PartsList List, RunLayout Layout, IReadOnlyList<UnresolvedReading> Unread)>
        FromDocumentAsync(RunSettings settings, List<string> notes, CancellationToken cancellationToken)
    {
        var words = Strings.For(settings.Language);

        using var document = PdfPageImageSource.Open(settings.InputPath!);
        _log(words.Format(TextKey.MsgPagesInDocument, Path.GetFileName(settings.InputPath!), document.PageCount));

        var typeset = false;
        IReadOnlyList<int> pages;

        if (string.IsNullOrWhiteSpace(settings.Pages))
        {
            (pages, typeset) = DetectCataloguePages(document, words, cancellationToken);
        }
        else
        {
            pages = PageRange.Parse(settings.Pages, document.PageCount);
        }

        if (pages.Count == 0)
        {
            // A typeset book that prints no catalogue is usually one volume of a set, and the
            // parts list is in another; a document with no text at all was simply not read.
            throw new InvalidOperationException(words[
                typeset ? TextKey.MsgNoCatalogueInThisBook : TextKey.MsgNoCataloguePages]);
        }

        var printed = new List<int>();
        var toRecognise = new List<int>();

        foreach (var page in pages)
        {
            (document.ReadPrintedCatalogue(page).Count > 0 ? printed : toRecognise).Add(page);
        }

        // Said here rather than before the document is opened, because a book that prints its
        // catalogue needs no recogniser at all and used to be refused by a build without one.
        // It is still said before a single page has been read.
        if (toRecognise.Count > 0 && !OcrEngines.IsAvailable)
        {
            throw new OcrUnavailableException(OcrEngines.DescribeUnavailable(words));
        }

        var entries = new List<CatalogueReading>();
        var unresolved = new List<UnresolvedReading>();
        var read = new List<string>();

        Report(RunStage.ReadingDocument, 0, pages.Count);

        if (printed.Count > 0)
        {
            _log(words.Format(TextKey.MsgReadingPrintedPages, PageRange.Format(printed)));

            var (found, missed) = await ReadPrintedPagesAsync(
                settings, document, printed, read, cancellationToken).ConfigureAwait(false);

            entries.AddRange(found);
            unresolved.AddRange(missed);
        }

        if (toRecognise.Count > 0)
        {
            _log(words.Format(TextKey.MsgReadingPages, PageRange.Format(toRecognise), settings.ColorScheme));

            var reader = new CatalogueReader(OcrEngines.Create(null, words), null, words);
            var recognised = await ReadPagesAsync(
                    reader, document, toRecognise, printed.Count, pages.Count, cancellationToken)
                .ConfigureAwait(false);

            entries.AddRange(recognised.Entries);
            unresolved.AddRange(recognised.Unresolved);
            read.AddRange(recognised.Notes);
        }

        Report(RunStage.ReadingDocument, pages.Count, pages.Count);

        notes.AddRange(read);
        foreach (var note in read)
        {
            _log("  " + note);
        }

        var list = PartsListBuilder.Build(entries, ColorReference.Table, settings.ColorScheme, words);
        notes.AddRange(list.Notes);

        var layout = RunLayout.Plan(settings)!;
        layout.CreateDirectories();

        return (list, layout, unresolved);
    }

    /// <summary>
    /// Reads the pages that print their catalogue, and turns their element numbers into parts
    /// and colours.
    /// </summary>
    private async Task<(List<CatalogueReading> Entries, List<UnresolvedReading> Unresolved)>
        ReadPrintedPagesAsync(
            RunSettings settings,
            PdfPageImageSource document,
            IReadOnlyList<int> pages,
            List<string> notes,
            CancellationToken cancellationToken)
    {
        var words = Strings.For(settings.Language);

        using var elements = ElementLookup.Open(
            settings.ElementMap,
            [Path.GetDirectoryName(Path.GetFullPath(settings.InputPath!)), Directory.GetCurrentDirectory()],
            settings.ApiKey,
            ColorReference.Table,
            words);

        // Read first, resolve after. Quoting the pages costs nothing, and knowing how many
        // distinct numbers there are before looking any of them up is what lets a run that has
        // to ask Rebrickable say so, and say how long it will take, rather than going quiet.
        var printed = new List<(int Page, PrintedEntry Entry)>();

        foreach (var pageNumber in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var onPage = document.ReadPrintedCatalogue(pageNumber);
            notes.Add(words.Format(TextKey.NoteEntriesFound, pageNumber, onPage.Count));
            printed.AddRange(onPage.Select(e => (pageNumber, e)));
        }

        var distinct = printed.Select(p => p.Entry.ElementId).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        if (elements.TablePath is null && elements.CanAskRebrickable)
        {
            _log(words.Format(TextKey.MsgLookingUpElements, distinct));
        }

        var entries = new List<CatalogueReading>();
        var unresolved = new List<UnresolvedReading>();

        for (var i = 0; i < printed.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (pageNumber, entry) = printed[i];
            Report(RunStage.ReadingDocument, i, printed.Count, entry.ElementId);

            var resolved = await elements.ResolveAsync(entry.ElementId, cancellationToken)
                .ConfigureAwait(false);

            if (resolved is null)
            {
                unresolved.Add(new UnresolvedReading(
                    pageNumber,
                    entry.Bounds,
                    entry.ToString(),
                    entry.Quantity,
                    null,
                    null,
                    words.Format(TextKey.ReasonUnknownElement, entry.ElementId)));
                continue;
            }

            entries.Add(new CatalogueReading(
                pageNumber,
                entry.Bounds,
                entry.Quantity,
                resolved.PartNumber,
                resolved.ColorCode,
                ReadingSource.PrintedText,
                ReadingSource.PrintedText,
                resolved.Scheme));
        }

        notes.AddRange(elements.Notes());
        return (entries, unresolved);
    }

    /// <param name="alreadyDone">
    /// Pages read by the other path, so that a document read partly each way still shows one
    /// bar filling once rather than two filling in turn.
    /// </param>
    private async Task<CatalogueReadResult> ReadPagesAsync(
        CatalogueReader reader,
        PdfPageImageSource document,
        IReadOnlyList<int> pages,
        int alreadyDone,
        int total,
        CancellationToken cancellationToken)
    {
        // The reader works a page at a time internally; running it per page as well is what
        // lets the bar move rather than sitting still for the whole document.
        var entries = new List<CatalogueReading>();
        var unresolved = new List<UnresolvedReading>();
        var notes = new List<string>();

        for (var i = 0; i < pages.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Report(RunStage.ReadingDocument, alreadyDone + i, total, $"page {pages[i]}");

            var one = await reader.ReadAsync(document, [pages[i]], cancellationToken).ConfigureAwait(false);

            entries.AddRange(one.Entries);
            unresolved.AddRange(one.Unresolved);
            notes.AddRange(one.Notes);
        }

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
                Strings.For(settings.Language),
                cancellationToken)
            .ConfigureAwait(false);

        notes.AddRange(list.Notes);

        var layout = RunLayout.Plan(settings)!;
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

        var layout = RunLayout.Plan(settings)!;
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

        using var library = new EscalatingLDrawLibrary(
            settings.LDrawOptions, message => _log("  " + message), words);
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
                    failed.Add(new FailedPart(partNumber, words[TextKey.ReasonShapeHasNoSurfaces]));
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
                         ? words[TextKey.MsgShapeClosed]
                         : words.Format(TextKey.MsgShapeOpenEdges, ready.Quality.OpenEdgeCount)));
            }
            catch (LDrawPartNotFoundException)
            {
                failed.Add(new FailedPart(partNumber, words[TextKey.ReasonNoShapeFile]));
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
                list,
                shapes,
                layout.PlateDirectory,
                settings.PackingOptions,
                settings.Language,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var part in plates.Skipped)
        {
            _log("  " + PlateBuilder.Describe(part, words, settings.Bed));
        }

        _log(words.Format(TextKey.MsgWrotePlates, plates.Plates.Count, layout.PlateDirectory));
        return plates;
    }

    /// <summary>
    /// Which pages hold a catalogue, when no range was given.
    /// </summary>
    /// <remarks>
    /// The search itself lives in <see cref="CataloguePages"/>, because the window and the
    /// listing command ask the same question and an answer worked out twice is an answer that
    /// will eventually differ.
    /// </remarks>
    /// <returns>The pages, and whether the document carries a text layer at all.</returns>
    private (List<int> Pages, bool Typeset) DetectCataloguePages(
        PdfPageImageSource document,
        Strings words,
        CancellationToken cancellationToken)
    {
        Report(RunStage.ReadingDocument, 0, document.PageCount, "looking for the catalogue");

        var search = CataloguePages.Find(
            document,
            (page, total) => Report(RunStage.ReadingDocument, page, total, "looking for the catalogue"),
            cancellationToken);

        var found = search.Numbers.ToList();

        if (found.Count > 0)
        {
            _log(words.Format(TextKey.MsgCataloguePagesFound, PageRange.Format(found)));
        }

        return (found, search.Typeset);
    }

    /// <summary>Where a set's run folder goes, given its number.</summary>
    public static string RebrickableSetFolderName(string setNumber) =>
        RunLayout.SetFolderName(setNumber);

    private void Report(RunStage stage, int completed = 0, int total = 0, string? detail = null)
    {
        var progress = new RunProgress(stage, completed, total, detail);

        _lastProgress = progress;
        _progress?.Report(progress);
    }
}

using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Text;

namespace Lego2STL.Core.Run;

/// <summary>
/// One part of a run, as a screen needs it.
/// </summary>
/// <remarks>
/// The three measurements are nullable because a part may have produced no shape, and because a
/// folder written before runs recorded themselves has no way of knowing them. Null means not
/// measured, which is a different thing from measured as zero, and the difference is what lets
/// the window say "not known" instead of "closed".
/// </remarks>
public sealed record RunDocumentPart(
    int Id,
    string PartNumber,
    int BrickLinkColorCode,
    string ColorName,
    Rgb24 Rgb,
    int Quantity,
    string? Title,
    string? Size,
    bool? IsClosed,
    int? OpenEdgeCount,
    double? ThinnestSpanMm,
    int? OverusedEdgeCount = null,
    float? ClosedAtTolerance = null,
    string? ElementId = null,
    string? Printability = null,
    string? LaidDown = null)
{
    /// <summary>
    /// Below this, in millimetres, a wall is thinner than a common nozzle can lay down and the
    /// slicer will either thicken it or leave it out. 0.4 mm is the usual nozzle; a wall needs
    /// at least one line of it.
    /// </summary>
    public const double ThinnestPrintableMillimetres = 0.8;

    public bool ShapeWasMeasured => IsClosed is not null;

    /// <summary>Holes in the surface. Not merely "not closed" - that is two faults, not one.</summary>
    public bool HasOpenEdges => OpenEdgeCount > 0;

    /// <summary>
    /// Surfaces that pass through each other, which is not the same as a hole.
    /// </summary>
    /// <remarks>
    /// A run recorded before both counts were kept has no overused figure, but it does say the
    /// shape was not closed and had no open edges, and only this fault can produce that pair -
    /// so runs already on disk name the right fault without being made again.
    /// </remarks>
    public bool HasSelfIntersection =>
        OverusedEdgeCount > 0
        || (OverusedEdgeCount is null && IsClosed == false && OpenEdgeCount == 0);

    /// <summary>True when the run deliberately did not build this part.</summary>
    public bool NotPrinted =>
        !Core.Catalogue.Printability.FromToken(Printability).IsPrinted();

    /// <summary>True when what it is made of is what ruled it out, rather than what it is.</summary>
    public bool NotPrintedForMaterial =>
        Core.Catalogue.Printability.FromToken(Printability) is Printable.NotItsMaterial;

    /// <summary>
    /// True when the parts database has never heard of this code.
    /// </summary>
    /// <remarks>
    /// Also true of every part of a run recorded before the database was consulted, which is why
    /// nothing is refused on this alone - it only chooses which sentence a card that has nothing
    /// else to show puts on itself.
    /// </remarks>
    public bool IsUnknownPart =>
        Core.Catalogue.Printability.FromToken(Printability) is Printable.Unknown;

    public bool HasThinFeatures =>
        ThinnestSpanMm is { } span && span < ThinnestPrintableMillimetres;

    public bool HasWarning => HasOpenEdges || HasThinFeatures || HasSelfIntersection;
}

/// <summary>
/// A run, as everything that shows one needs it.
/// </summary>
/// <remarks>
/// <para>
/// Computed by exactly one function from exactly one record, which is the whole of this design.
/// A run being watched reaches here as outcome, then manifest, then document; a run reopened
/// weeks later reaches here as file, then manifest, then document. The two are identical by
/// construction rather than by anyone remembering to keep a second builder in step.
/// </para>
/// <para>
/// Immutable, and a record, so two of them can simply be compared - which is what the test
/// defending that claim does.
/// </para>
/// </remarks>
public sealed record RunDocument
{
    public required string Folder { get; init; }

    public required string Name { get; init; }

    public required RunStatus Status { get; init; }

    /// <summary>False for a folder written before runs recorded themselves.</summary>
    public bool ManifestKnown { get; init; }

    public bool FromNewerBuild { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>What was asked for, ready to ask for again. Null when nothing recorded it.</summary>
    public RunSettings? Settings { get; init; }

    /// <summary>
    /// The command that would do the same thing, as this run stored it.
    /// </summary>
    /// <remarks>
    /// Stored rather than recomputed, so a run from three weeks ago can be repeated in a
    /// terminal without anyone reconstructing what was ticked at the time.
    /// </remarks>
    public string CommandLine { get; init; } = string.Empty;

    public ManifestStage? LastStage { get; init; }

    public int EntryCount { get; init; }

    public int TotalPieces { get; init; }

    public int DistinctPartCount { get; init; }

    public int ShapeCount { get; init; }

    public int ClosedShapeCount { get; init; }

    public int PlateCount { get; init; }

    /// <summary>The largest scale at which every part would fit, when some did not.</summary>
    public double? LargestFittingScalePercent { get; init; }

    /// <summary>Parts no plate could take, with what ruled each one out.</summary>
    public IReadOnlyList<SkippedPart> DidNotFit { get; init; } = [];

    /// <summary>Every plate the run wrote, so a colour can be matched to its file by code.</summary>
    public IReadOnlyList<ManifestPlate> Plates { get; init; } = [];

    /// <summary>Entries the reader could not make out, each with where it was.</summary>
    public IReadOnlyList<ManifestUnread> Unread { get; init; } = [];

    /// <summary>
    /// The same entries as sentences, for the places that only show them.
    /// </summary>
    /// <remarks>
    /// Worded on demand rather than when the run ended, so a window switched to the other
    /// language re-words them instead of leaving the old ones in place for ever.
    /// </remarks>
    public IReadOnlyList<string> UnreadText => UnreadTextIn(Strings.For(DisplayLanguages.Fallback));

    /// <summary>The same entries as sentences, in the language the caller is speaking.</summary>
    public IReadOnlyList<string> UnreadTextIn(Strings words)
    {
        ArgumentNullException.ThrowIfNull(words);

        // An entry from before a run kept where it was is already a finished sentence, and
        // the only honest thing to do with it is show it as it was written.
        return
        [
            .. Unread.Select(u => u.CanBeAskedAbout
                ? words.Format(TextKey.MsgUnreadEntryAt, u.Page, u.Bounds, u.Reason)
                : u.RawText),
        ];
    }

    public IReadOnlyList<ManifestFailure> Failed { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];

    public string? Error { get; init; }

    public IReadOnlyList<RunDocumentPart> Parts { get; init; } = [];

    public required string PartsListPath { get; init; }

    public required string StlDirectory { get; init; }

    /// <summary>Where the pictures cut from the document live.</summary>
    public string ImageDirectory { get; init; } = string.Empty;

    /// <summary>Where the crops of what could not be read live.</summary>
    public string ReviewDirectory { get; init; } = string.Empty;

    /// <summary>Where the answers given about those crops are kept.</summary>
    public string OverridesPath { get; init; } = string.Empty;

    public required string PlateDirectory { get; init; }

    public required string ReportPath { get; init; }

    public required string LogPath { get; init; }

    /// <summary>
    /// How far the run actually got, from 0 to 1.
    /// </summary>
    /// <remarks>
    /// A completed run is full and nothing else is. A run that could not be verified stopped
    /// somewhere, and showing it at 100% while the command line returns a different exit code
    /// for the same run is the disagreement this exists to end.
    /// </remarks>
    public double Progress => Status == RunStatus.Complete
        ? 1
        : LastStage is { } stage
            ? new RunProgress(stage.Stage, stage.Completed, stage.Total).Fraction
            : 0;

    public bool IsRunning => Status == RunStatus.Running;

    public bool NeedsDecision => Status == RunStatus.NeedsDecision;

    public bool Failing => Status == RunStatus.Failed;

    /// <summary>The one projection: a record and a folder become a run on screen.</summary>
    public static RunDocument From(RunManifest manifest, RunLayout layout)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(layout);

        return Shell(layout) with
        {
            Status = manifest.Status,
            ManifestKnown = true,
            FromNewerBuild = manifest.Version > RunManifest.CurrentVersion,
            StartedAt = manifest.StartedAt,
            FinishedAt = manifest.FinishedAt,
            Settings = manifest.Settings.ToSettings(),
            CommandLine = manifest.CommandLine,
            LastStage = manifest.LastStage,

            EntryCount = manifest.EntryCount,
            TotalPieces = manifest.TotalPieces,
            DistinctPartCount = manifest.DistinctPartCount,
            ShapeCount = manifest.ShapeCount,
            ClosedShapeCount = manifest.ClosedShapeCount,
            PlateCount = manifest.PlateCount,
            LargestFittingScalePercent = manifest.LargestFittingScalePercent,
            DidNotFit = manifest.DidNotFit,
            Plates = manifest.Plates,

            Unread = manifest.Unread,
            Failed = manifest.Failed,
            Notes = manifest.Notes,
            Error = manifest.Error,

            Parts =
            [
                .. manifest.Parts.Select(part => new RunDocumentPart(
                    part.Id,
                    part.Part,
                    part.ColorCode,
                    part.Color,
                    Rgb24.Parse(part.Rgb),
                    part.Quantity,
                    part.Title,
                    part.Size,
                    part.IsClosed,
                    part.OpenEdgeCount,
                    part.ThinnestSpanMm,
                    part.OverusedEdgeCount,
                    part.ClosedAtTolerance,
                    part.ElementId,
                    part.Printability,
                    part.LaidDown)),
            ],
        };
    }

    /// <summary>
    /// A folder written before runs recorded themselves.
    /// </summary>
    /// <remarks>
    /// Its parts list still says which parts in which colours and how many, so that much is
    /// shown. What its shapes measured is gone: nothing reads a mesh back, and a re-welded count
    /// of open edges would depend on the tolerance it was welded at, so it could honestly
    /// disagree with what the run itself reported. The window says so, and offers to run it
    /// again rather than guessing.
    /// </remarks>
    public static RunDocument WithoutManifest(RunLayout layout, PartsList? partsList)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var entries = partsList?.Entries ?? [];

        return Shell(layout) with
        {
            Status = RunStatus.Stopped,
            ManifestKnown = false,
            EntryCount = entries.Count,
            TotalPieces = partsList?.TotalPieces ?? 0,
            DistinctPartCount = partsList?.DistinctPartNumbers.Count ?? 0,
            Notes = partsList?.Notes ?? [],
            Parts =
            [
                .. entries.Select(entry => new RunDocumentPart(
                    entry.Id,
                    entry.PartNumber,
                    entry.BrickLinkColorCode,
                    entry.ColorName,
                    entry.Rgb,
                    entry.Quantity,
                    Title: null,
                    Size: null,
                    IsClosed: null,
                    OpenEdgeCount: null,
                    ThinnestSpanMm: null)),
            ],
        };
    }

    /// <summary>Everything a document knows from its folder alone.</summary>
    private static RunDocument Shell(RunLayout layout) => new()
    {
        Folder = layout.Root,
        Name = layout.Name,
        Status = RunStatus.Running,
        PartsListPath = layout.PartsListPath,
        StlDirectory = layout.StlDirectory,
        ImageDirectory = layout.ImageDirectory,
        ReviewDirectory = layout.ReviewDirectory,
        OverridesPath = layout.OverridesPath,
        PlateDirectory = layout.PlateDirectory,
        ReportPath = layout.ReportPath,
        LogPath = layout.LogPath,
    };
}

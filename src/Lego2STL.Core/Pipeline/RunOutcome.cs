using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.Ocr;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Rebrickable;
using Lego2STL.Core.Run;

namespace Lego2STL.Core.Pipeline;

/// <summary>How a run ended.</summary>
public enum RunResult
{
    /// <summary>Everything asked for was produced.</summary>
    Complete,

    /// <summary>
    /// Output was written, but something in it could not be verified, so the later stages
    /// were refused. Worth a different answer from plain failure: there is a usable parts
    /// list, it just needs a decision before it goes any further.
    /// </summary>
    Unverified,

    /// <summary>Nothing usable was produced.</summary>
    Failed,
}

/// <summary>A part that could not be turned into a shape, and why.</summary>
public sealed record FailedPart(string PartNumber, string Reason);

/// <summary>
/// Everything a run produced, so that a window can show it and a terminal can print it
/// without either having to work anything out for itself.
/// </summary>
public sealed record RunOutcome
{
    public required RunResult Result { get; init; }

    public required RunSettings Settings { get; init; }

    public RunLayout? Layout { get; init; }

    public PartsList? PartsList { get; init; }

    /// <summary>Entries on the pages that could not be read. Empty unless a document was read.</summary>
    public IReadOnlyList<UnresolvedReading> Unread { get; init; } = [];

    public IReadOnlyList<PreparedMesh> Shapes { get; init; } = [];

    public IReadOnlyList<FailedPart> Failed { get; init; } = [];

    /// <summary>What the parts database says about each part, when there is a database.</summary>
    public IReadOnlyDictionary<string, PartFact> PartFacts { get; init; } =
        new Dictionary<string, PartFact>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Parts deliberately not built, because they cannot be printed.</summary>
    public IReadOnlyList<string> NotPrinted { get; init; } = [];

    public PlateBuildResult? Plates { get; init; }

    /// <summary>Where the geometry came from, for the report.</summary>
    public string GeometrySource { get; init; } = string.Empty;

    /// <summary>Anything worth saying that is not an error.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>The message, when the run failed outright.</summary>
    public string? Error { get; init; }

    public int ClosedShapeCount => Shapes.Count(s => s.Quality.IsClosed);

    public int ClearedShapeCount => Shapes.Count(s => s.ClearanceApplied);

    /// <summary>The shapes by part number, for whatever wants to look one up.</summary>
    public IReadOnlyDictionary<string, IndexedMesh> ShapesByPart { get; init; } =
        new Dictionary<string, IndexedMesh>(StringComparer.OrdinalIgnoreCase);

    public static RunOutcome Failure(RunSettings settings, string error) =>
        new() { Result = RunResult.Failed, Settings = settings, Error = error };
}

using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Rebrickable;

namespace Lego2STL.Core.Run;

/// <summary>How a run ended, as its own folder records it.</summary>
/// <remarks>
/// Four endings, not two. A run that wrote a usable parts list and then refused to go on is
/// not a failure and is not a success, and calling it either is the single largest way the
/// window and the command line have disagreed.
/// </remarks>
public enum RunStatus
{
    Running,
    Complete,
    NeedsDecision,
    Failed,
    Stopped,
}

/// <summary>Whether a folder's record could be used.</summary>
public enum ManifestState
{
    Present,
    Missing,
    Newer,
}

/// <summary>The step a run had reached, and how far into it.</summary>
public sealed record ManifestStage(RunStage Stage, int Completed, int Total);

/// <summary>A part that produced no shape, and why.</summary>
public sealed record ManifestFailure(string Part, string Reason);

/// <summary>
/// An entry the reader could not make out, with enough to ask a person about it.
/// </summary>
/// <param name="Bounds">
/// The region on the page, as the same text the extraction prints, so the file stays readable
/// by eye and the shape of <c>PixelBounds</c> is not frozen into it.
/// </param>
[JsonConverter(typeof(ManifestUnreadConverter))]
public sealed record ManifestUnread(
    int Page,
    string Bounds,
    string RawText,
    int? Quantity,
    string? PartNumber,
    int? ColorCode,
    string Reason,
    string? Picture = null)
{
    /// <summary>False for an entry recorded before a run kept where it was.</summary>
    [JsonIgnore]
    public bool CanBeAskedAbout => Bounds.Length > 0;
}

/// <summary>
/// Reads both shapes this has had: a finished sentence, and the entry it was made from.
/// </summary>
/// <remarks>
/// Runs already on people's disks recorded a sentence per unread entry. Refusing those would
/// not cost the sentences alone - a record that will not parse costs the whole run, back to
/// "a folder from before runs recorded themselves" - so an old sentence is kept as its own
/// text, with no page and no region, and is shown rather than asked about.
/// </remarks>
internal sealed class ManifestUnreadConverter : JsonConverter<ManifestUnread>
{
    public override ManifestUnread? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var sentence = reader.GetString() ?? string.Empty;
            return new ManifestUnread(0, string.Empty, sentence, null, null, null, sentence);
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        return new ManifestUnread(
            Number(root, "page") ?? 0,
            Text(root, "bounds") ?? string.Empty,
            Text(root, "rawText") ?? string.Empty,
            Number(root, "quantity"),
            Text(root, "partNumber"),
            Number(root, "colorCode"),
            Text(root, "reason") ?? string.Empty,
            Text(root, "picture"));
    }

    public override void Write(
        Utf8JsonWriter writer, ManifestUnread value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        writer.WriteNumber("page", value.Page);
        writer.WriteString("bounds", value.Bounds);
        writer.WriteString("rawText", value.RawText);
        Number(writer, "quantity", value.Quantity);
        Text(writer, "partNumber", value.PartNumber);
        Number(writer, "colorCode", value.ColorCode);
        writer.WriteString("reason", value.Reason);
        Text(writer, "picture", value.Picture);
        writer.WriteEndObject();
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? Number(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.TryGetInt32(out var number)
            ? number
            : null;

    private static void Text(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    private static void Number(Utf8JsonWriter writer, string name, int? value)
    {
        if (value is { } number)
        {
            writer.WriteNumber(name, number);
        }
    }
}

/// <summary>
/// A plate the run wrote, and the colour that went on it.
/// </summary>
/// <remarks>
/// The colour code rather than the name, because the name is written in whichever language the
/// run spoke and the code is the same number in all of them. That is what lets a run made in
/// Italian be reopened in English and still offer its plates.
/// </remarks>
public sealed record ManifestPlate(string FileName, int ColorCode);

/// <summary>
/// One part as the run left it.
/// </summary>
/// <remarks>
/// The three measurements are recorded here because they cannot be recovered later. Nothing
/// reads a mesh back off the disk, and even if something did, an STL is a triangle soup with
/// no shared corners: a re-welded count of open edges depends on the tolerance it was welded
/// at, and could honestly disagree with what the run itself reported. Null means not measured -
/// the part produced no shape - rather than measured as zero.
/// </remarks>
public sealed record ManifestPart(
    int Id,
    string Part,
    int ColorCode,
    string Color,
    string Rgb,
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
    string? LaidDown = null);

/// <summary>
/// What a run records about itself, in its own folder.
/// </summary>
/// <remarks>
/// <para>
/// Written from the moment a run starts rather than at its end, which is what makes the folder
/// - and everything the window says about it - real from second one, through failure and
/// cancellation alike. It has its own lifecycle for that reason: the report is written at one
/// exit only, and two of the pipeline's three endings never reach it.
/// </para>
/// <para>
/// Versioned from the first release, because this is a file other programs may come to read.
/// </para>
/// </remarks>
public sealed record RunManifest
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public RunStatus Status { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public ManifestSettings Settings { get; init; } = new();

    /// <summary>The command that would do the same thing, kept so a run can be repeated.</summary>
    public string CommandLine { get; init; } = string.Empty;

    /// <summary>Where it had got to, which is what lets a row say where a run stopped.</summary>
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

    /// <summary>Every plate written, in the order they were written.</summary>
    public IReadOnlyList<ManifestPlate> Plates { get; init; } = [];

    /// <summary>Entries the reader could not make out, each with where it was.</summary>
    public IReadOnlyList<ManifestUnread> Unread { get; init; } = [];

    public IReadOnlyList<ManifestFailure> Failed { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];

    public string? Error { get; init; }

    public IReadOnlyList<ManifestPart> Parts { get; init; } = [];

    /// <summary>A run that has only just begun: the settings, and nothing it has produced yet.</summary>
    public static RunManifest Starting(RunSettings settings, DateTimeOffset startedAt)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new RunManifest
        {
            Status = RunStatus.Running,
            StartedAt = startedAt,
            Settings = ManifestSettings.From(settings),
            CommandLine = settings.ToCommandLine(),
        };
    }

    /// <summary>A run that was stopped part-way, with the step it had reached.</summary>
    public static RunManifest Stopped(
        RunSettings settings,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        RunProgress? lastStage)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return Starting(settings, startedAt) with
        {
            Status = RunStatus.Stopped,
            FinishedAt = finishedAt,
            LastStage = Stage(lastStage),
        };
    }

    public static RunManifest From(
        RunOutcome outcome,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        RunProgress? lastStage)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var shapes = outcome.Shapes.ToDictionary(s => s.PartNumber, StringComparer.OrdinalIgnoreCase);
        var entries = outcome.PartsList?.Entries ?? [];

        return new RunManifest
        {
            Status = outcome.Result switch
            {
                RunResult.Complete => RunStatus.Complete,
                RunResult.Unverified => RunStatus.NeedsDecision,
                _ => RunStatus.Failed,
            },
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            Settings = ManifestSettings.From(outcome.Settings),
            CommandLine = outcome.Settings.ToCommandLine(),
            LastStage = Stage(lastStage),

            EntryCount = entries.Count,
            TotalPieces = outcome.PartsList?.TotalPieces ?? 0,
            DistinctPartCount = outcome.PartsList?.DistinctPartNumbers.Count ?? 0,
            ShapeCount = outcome.Shapes.Count,
            ClosedShapeCount = outcome.ClosedShapeCount,
            PlateCount = outcome.Plates?.Plates.Count ?? 0,
            LargestFittingScalePercent = FittingScale.Largest(
                outcome.ShapesByPart.Select(pair =>
                {
                    var (min, max) = pair.Value.Bounds();
                    var size = max - min;
                    return new PackableItem(pair.Key, new Vector2(size.X, size.Y), size.Z);
                }),
                outcome.Settings.Bed,
                margin: new PackingOptions().Margin,
                scaleUsed: outcome.Settings.ScalePercent),
            DidNotFit = outcome.Plates?.Skipped ?? [],
            Plates =
            [
                .. (outcome.Plates?.Plates ?? [])
                    .Select(plate => new ManifestPlate(plate.FileName, plate.BrickLinkColorCode)),
            ],

            Unread =
            [
                .. outcome.Unread.Select(u => new ManifestUnread(
                    u.Page,
                    u.Bounds.ToString(),
                    u.RawText,
                    u.Quantity,
                    u.PartNumber,
                    u.ColorCode,
                    u.Reason,
                    u.Picture)),
            ],
            Failed = [.. outcome.Failed.Select(f => new ManifestFailure(f.PartNumber, f.Reason))],
            Notes = [.. outcome.Notes],
            Error = outcome.Error,

            Parts = [.. entries.Select(entry => Part(entry, shapes, outcome.PartFacts))],
        };
    }

    private static ManifestPart Part(
        PartEntry entry,
        Dictionary<string, PreparedMesh> shapes,
        IReadOnlyDictionary<string, PartFact> facts)
    {
        shapes.TryGetValue(entry.PartNumber, out var shape);

        return new ManifestPart(
            entry.Id,
            entry.PartNumber,
            entry.BrickLinkColorCode,
            entry.ColorName,
            entry.Rgb.ToString(),
            entry.Quantity,
            shape?.Title,
            shape?.DescribeSize(),
            shape?.Quality.IsClosed,
            shape?.Quality.OpenEdgeCount,
            shape is null ? null : ClearanceOffset.ThinnestSpan(shape.Mesh),
            shape?.Quality.OverusedEdgeCount,
            shape?.ClosedAtTolerance,
            entry.ElementId,
            Core.Catalogue.Printability.Of(facts.GetValueOrDefault(entry.PartNumber)).Token(),
            shape?.LaidDown);
    }

    private static ManifestStage? Stage(RunProgress? progress) =>
        progress is null ? null : new ManifestStage(progress.Stage, progress.Completed, progress.Total);

    // ---- The file -------------------------------------------------------------------------

    /// <remarks>
    /// <para>
    /// Kebab-case tokens for every enum, so "needs-decision" and "building-shapes" read as
    /// words in a file someone may open in a text editor, and stay meaningful if the order of
    /// an enum's members ever changes.
    /// </para>
    /// <para>
    /// The relaxed encoder because this is a file on a disk, not a fragment of a web page: the
    /// escaping meant for HTML would write the key's placeholder as <your key>, and
    /// an Italian note as a row of escapes.
    /// </para>
    /// </remarks>
    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    /// <summary>
    /// Writes the manifest into a run's folder.
    /// </summary>
    /// <remarks>
    /// Through a temporary file and a replace, because the terminal and the window can be
    /// running at once. Failing is swallowed: a run that produced its shapes has done its job,
    /// and not being able to record that is not worth interrupting anyone over.
    /// </remarks>
    public static async Task WriteAsync(
        RunLayout layout,
        RunManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(manifest);

        var temporary = layout.ManifestPath + ".writing";

        try
        {
            Directory.CreateDirectory(layout.Root);

            await File.WriteAllTextAsync(
                    temporary, JsonSerializer.Serialize(manifest, Format), cancellationToken)
                .ConfigureAwait(false);

            File.Move(temporary, layout.ManifestPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or JsonException
                                       or NotSupportedException)
        {
            TryDelete(temporary);
        }
    }

    /// <summary>
    /// Reads a manifest back.
    /// </summary>
    /// <remarks>
    /// Following the precedent already set for the window's preferences: a file that will not
    /// read is treated as no file. The folder beside it still holds everything the run
    /// produced, so the cost of a manifest nobody can parse is one history row saying less,
    /// not a screen refusing to open.
    /// </remarks>
    public static (RunManifest? Manifest, ManifestState State) Read(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        try
        {
            if (!File.Exists(manifestPath))
            {
                return (null, ManifestState.Missing);
            }

            var manifest = JsonSerializer.Deserialize<RunManifest>(
                File.ReadAllText(manifestPath), Format);

            return manifest is null
                ? (null, ManifestState.Missing)
                : (manifest, manifest.Version > CurrentVersion
                    ? ManifestState.Newer
                    : ManifestState.Present);
        }
        catch (Exception ex) when (ex is IOException
                                       or JsonException
                                       or UnauthorizedAccessException
                                       or NotSupportedException)
        {
            return (null, ManifestState.Missing);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover half-written file is untidy and harmless.
        }
    }
}

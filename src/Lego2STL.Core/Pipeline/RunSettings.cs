using System.Globalization;
using System.Text;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.LDraw;
using Lego2STL.Core.Ocr;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Text;

namespace Lego2STL.Core.Pipeline;

/// <summary>Where a run starts from.</summary>
public enum InputKind
{
    /// <summary>A document whose pages hold a parts catalogue.</summary>
    Document,

    /// <summary>A parts list from an earlier run, or written by hand.</summary>
    PartsList,

    /// <summary>A set number, whose contents are looked up online.</summary>
    SetNumber,
}

/// <summary>How far to take the run.</summary>
public enum RunStages
{
    /// <summary>Read the input and write the parts list, and stop there.</summary>
    PartsListOnly,

    /// <summary>Also produce the shape files.</summary>
    Shapes,

    /// <summary>Also arrange the shapes onto coloured plates.</summary>
    ShapesAndPlates,
}

/// <summary>
/// Everything a run can be asked for.
/// </summary>
/// <remarks>
/// <para>
/// One record for the whole tool, shared by the command line and the window, so that the two
/// cannot drift apart. Every option is here exactly once; a new one appears in both places by
/// being added here, and neither can quietly grow a setting the other does not have.
/// </para>
/// <para>
/// <see cref="ToCommandLine"/> is what closes the loop the other way. The window shows the
/// command that would do what has been set up, so that anyone using it can see how to do the
/// same thing again from a terminal or a script, without reading a manual.
/// </para>
/// </remarks>
public sealed record RunSettings
{
    // ---- Input -------------------------------------------------------------------------

    public InputKind Kind { get; init; } = InputKind.Document;

    /// <summary>The document or parts list to start from.</summary>
    public string? InputPath { get; init; }

    /// <summary>The set to look up, e.g. "42100-1".</summary>
    public string? SetNumber { get; init; }

    /// <summary>Which pages hold the catalogue, e.g. "2-5" or "2-5,8,11-13".</summary>
    public string? Pages { get; init; }

    /// <summary>
    /// Whose colour numbering the document prints.
    /// </summary>
    /// <remarks>
    /// Applies to pages read off the pixels, where a colour code is printed beside the part
    /// number. Pages that print element numbers instead carry their colour inside the number,
    /// so this has nothing to say about them.
    /// </remarks>
    public ColorScheme ColorScheme { get; init; } = ColorScheme.BrickLink;

    /// <summary>
    /// A Rebrickable <c>elements.csv</c>, or a folder holding one, for turning the element
    /// numbers official instructions print into parts and colours.
    /// </summary>
    /// <remarks>
    /// Optional in every direction. Left empty, the document's folder and the working folder
    /// are searched for one - a level down as well, so an unpacked dump sitting beside the
    /// documents is found by itself. Failing that a Rebrickable key answers the same question
    /// online, and failing that the older six-digit numbers can still be taken apart on their
    /// own. See <see cref="Catalogue.ElementLookup"/>.
    /// </remarks>
    public string? ElementMap { get; init; }

    /// <summary>Include a set's spare pieces as well as the ones the model uses.</summary>
    public bool IncludeSpares { get; init; }

    // ---- Stages ------------------------------------------------------------------------

    public RunStages Stages { get; init; } = RunStages.ShapesAndPlates;

    // ---- Output ------------------------------------------------------------------------

    public string? OutputDirectory { get; init; }

    public char Delimiter { get; init; } = PartsListCsv.DefaultDelimiter;

    /// <summary>Write the readable form of STL rather than the compact one.</summary>
    public bool AsciiStl { get; init; }

    // ---- Geometry ----------------------------------------------------------------------

    public bool KeepOrigin { get; init; }

    public double ScalePercent { get; init; } = 100.0;

    public double Clearance { get; init; }

    /// <summary>
    /// Cover over the gaps in a shape's surface, turning it into a solid.
    /// </summary>
    /// <remarks>
    /// On unless asked otherwise, which is the same way round the command line and the window
    /// have it: a shape with holes in its surface is the common case, and covering them is what
    /// makes a clearance applicable to it at all.
    /// </remarks>
    public bool FillGaps { get; init; } = true;

    public bool NoSeamRepair { get; init; }

    public double WeldTolerance { get; init; } = VertexWelder.DefaultToleranceUnits;

    // ---- Shape library -----------------------------------------------------------------

    public string? LDrawDirectory { get; init; }

    public string? LDrawCache { get; init; }

    public bool Offline { get; init; }

    public bool IncludeUnofficial { get; init; } = true;

    // ---- Plates ------------------------------------------------------------------------

    public string Printer { get; init; } = PrintBeds.Default.Name;

    /// <summary>A bed size in millimetres, used in place of <see cref="Printer"/> when set.</summary>
    public string? PlateSize { get; init; }

    public double PlateSpacing { get; init; } = 3.0;

    // ---- Behaviour ---------------------------------------------------------------------

    public DisplayLanguage Language { get; init; } = DisplayLanguages.Fallback;

    public string? ApiKey { get; init; }

    /// <summary>Say only what matters: warnings, failures and where things were written.</summary>
    public bool Quiet { get; init; }

    /// <summary>Also write everything said during the run to this file.</summary>
    public string? LogFile { get; init; }

    // ---- The key, never written out ----------------------------------------------------

    /// <summary>
    /// What stands in for the key wherever the settings are written down.
    /// </summary>
    /// <remarks>
    /// One rule with one spelling, applied by the shown command line and by the record a run
    /// keeps of itself. Both are made to be copied, pasted and kept, and a secret must not
    /// travel with either.
    /// </remarks>
    public const string MaskedApiKey = "<your key>";

    public static string? MaskApiKey(string? apiKey) =>
        string.IsNullOrWhiteSpace(apiKey) ? null : MaskedApiKey;

    // ---- Derived -----------------------------------------------------------------------

    public bool WantsShapes => Stages is RunStages.Shapes or RunStages.ShapesAndPlates;

    public bool WantsPlates => Stages is RunStages.ShapesAndPlates;

    public PrintBed Bed => PlateSize is { Length: > 0 }
        ? PrintBeds.Parse(PlateSize)
        : PrintBeds.Parse(Printer);

    public MeshPipelineOptions MeshOptions => new()
    {
        RepairSeams = !NoSeamRepair,
        FillGaps = FillGaps,
        PlaceOnBed = !KeepOrigin,
        ScalePercent = (float)ScalePercent,
        ClearanceMillimetres = (float)Clearance,
        WeldTolerance = (float)WeldTolerance,
    };

    public LDrawSourceOptions LDrawOptions => new()
    {
        LocalDirectory = LDrawDirectory,
        CacheDirectory = LDrawCache,
        Offline = Offline,
        IncludeUnofficial = IncludeUnofficial,
    };

    public PackingOptions PackingOptions => new()
    {
        Bed = Bed,
        Spacing = (float)PlateSpacing,
    };

    /// <summary>
    /// Anything obviously wrong, said before the run starts rather than part-way through.
    /// </summary>
    public IReadOnlyList<string> Problems()
    {
        var words = Strings.For(Language);
        var problems = new List<string>();

        switch (Kind)
        {
            case InputKind.Document when string.IsNullOrWhiteSpace(InputPath):
                problems.Add(words[TextKey.ErrChooseDocument]);
                break;

            case InputKind.Document when !File.Exists(InputPath):
                problems.Add(words.Format(TextKey.ErrNoFileAt, InputPath));
                break;

            case InputKind.PartsList when string.IsNullOrWhiteSpace(InputPath):
                problems.Add(words[TextKey.ErrChoosePartsList]);
                break;

            case InputKind.PartsList when !File.Exists(InputPath):
                problems.Add(words.Format(TextKey.ErrNoFileAt, InputPath));
                break;

            case InputKind.SetNumber when string.IsNullOrWhiteSpace(SetNumber):
                problems.Add(words[TextKey.ErrTypeSetNumber]);
                break;
        }

        // A missing recogniser is no longer refused here. Whether a document needs one is a
        // fact about the document: a book that prints its catalogue in a text layer is read
        // without one, and refusing it unseen turned the commonest case - official building
        // instructions - into an error on a build that could have read it. The refusal now
        // happens in the run, once the pages are known and still before any is read.

        if (Clearance < 0)
        {
            problems.Add(words.Format(TextKey.ErrClearanceNegative, Clearance));
        }

        if (ScalePercent <= 0)
        {
            problems.Add(words[TextKey.ErrScaleNotPositive]);
        }

        if (PlateSpacing < 0)
        {
            problems.Add(words[TextKey.ErrSpacingNegative]);
        }

        // Only a run that will lay parts out on a bed cares what size it is.
        if (WantsPlates && PlateSize is { Length: > 0 } && !PrintBeds.TryParseSize(PlateSize, out _))
        {
            problems.Add(words.Format(TextKey.ErrNotABedSize, PlateSize));
        }

        return problems;
    }

    /// <summary>
    /// The command that would do the same thing, so the window can show its own workings.
    /// </summary>
    public string ToCommandLine()
    {
        var parts = new List<string> { "lego2stl" };
        var defaults = new RunSettings();

        switch (Kind)
        {
            case InputKind.SetNumber:
                parts.Add("build");
                parts.Add("--set");
                parts.Add(Quote(SetNumber ?? "SET"));
                break;

            case InputKind.PartsList:
                parts.Add("build");
                parts.Add(Quote(InputPath ?? "parts.csv"));
                break;

            default:
                parts.Add("extract");
                parts.Add(Quote(InputPath ?? "document.pdf"));
                if (!string.IsNullOrWhiteSpace(Pages))
                {
                    parts.Add(Quote(Pages));
                }

                break;
        }

        AddInputOptions(parts, defaults);
        AddOutputOptions(parts, defaults);
        AddGeometryOptions(parts, defaults);
        AddLibraryOptions(parts, defaults);
        AddPlateOptions(parts, defaults);
        AddBehaviourOptions(parts, defaults);

        return string.Join(' ', parts);
    }

    private void AddInputOptions(List<string> parts, RunSettings defaults)
    {
        if (Kind == InputKind.Document && ColorScheme != defaults.ColorScheme)
        {
            parts.Add("--color-scheme");
            parts.Add(ColorScheme.ToString());
        }

        if (Kind == InputKind.Document && !string.IsNullOrWhiteSpace(ElementMap))
        {
            parts.Add("--element-map");
            parts.Add(Quote(ElementMap));
        }

        if (Kind == InputKind.SetNumber && IncludeSpares)
        {
            parts.Add("--include-spares");
        }

        if (Stages == RunStages.PartsListOnly)
        {
            parts.Add("--csv-only");
        }
    }

    private void AddOutputOptions(List<string> parts, RunSettings defaults)
    {
        if (!string.IsNullOrWhiteSpace(OutputDirectory))
        {
            parts.Add("--output-dir");
            parts.Add(Quote(OutputDirectory));
        }

        if (Delimiter != defaults.Delimiter)
        {
            parts.Add("--delimiter");
            parts.Add(Delimiter == '\t' ? "tab" : Quote(Delimiter.ToString()));
        }

        if (AsciiStl)
        {
            parts.Add("--ascii");
        }
    }

    private void AddGeometryOptions(List<string> parts, RunSettings defaults)
    {
        if (KeepOrigin)
        {
            parts.Add("--keep-origin");
        }

        if (Math.Abs(ScalePercent - defaults.ScalePercent) > 1e-9)
        {
            parts.Add("--scale");
            parts.Add(Format(ScalePercent));
        }

        if (Clearance > 0)
        {
            parts.Add("--clearance");
            parts.Add(Format(Clearance));
        }

        if (!FillGaps)
        {
            parts.Add("--no-repair");
        }

        if (NoSeamRepair)
        {
            parts.Add("--no-seam-repair");
        }

        if (Math.Abs(WeldTolerance - defaults.WeldTolerance) > 1e-9)
        {
            parts.Add("--weld-tolerance");
            parts.Add(Format(WeldTolerance));
        }
    }

    private void AddLibraryOptions(List<string> parts, RunSettings defaults)
    {
        if (!string.IsNullOrWhiteSpace(LDrawDirectory))
        {
            parts.Add("--ldraw-dir");
            parts.Add(Quote(LDrawDirectory));
        }

        if (!string.IsNullOrWhiteSpace(LDrawCache))
        {
            parts.Add("--ldraw-cache");
            parts.Add(Quote(LDrawCache));
        }

        if (Offline)
        {
            parts.Add("--offline");
        }

        if (IncludeUnofficial != defaults.IncludeUnofficial)
        {
            parts.Add("--no-unofficial");
        }
    }

    private void AddPlateOptions(List<string> parts, RunSettings defaults)
    {
        if (!WantsPlates)
        {
            if (Stages != RunStages.PartsListOnly)
            {
                parts.Add("--no-plates");
            }

            return;
        }

        if (PlateSize is { Length: > 0 })
        {
            parts.Add("--plate-size");
            parts.Add(Quote(PlateSize));
        }
        else if (!string.Equals(Printer, defaults.Printer, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("--printer");
            parts.Add(Quote(Printer));
        }

        if (Math.Abs(PlateSpacing - defaults.PlateSpacing) > 1e-9)
        {
            parts.Add("--plate-spacing");
            parts.Add(Format(PlateSpacing));
        }
    }

    private void AddBehaviourOptions(List<string> parts, RunSettings defaults)
    {
        if (Language != defaults.Language)
        {
            parts.Add("--lang");
            parts.Add(Language.Tag());
        }

        if (Quiet)
        {
            parts.Add("--quiet");
        }

        if (!string.IsNullOrWhiteSpace(LogFile))
        {
            parts.Add("--log");
            parts.Add(Quote(LogFile));
        }

        // The key is deliberately never written out: the point of showing the command is that
        // it can be pasted somewhere, and a secret should not travel with it.
        if (MaskApiKey(ApiKey) is { } masked)
        {
            parts.Add("--api-key");
            parts.Add(masked);
        }
    }

    private static string Format(double value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>Wraps in quotes only when it needs them, so the usual case stays readable.</summary>
    private static string Quote(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }

        if (!value.Any(c => char.IsWhiteSpace(c) || c is '"'))
        {
            return value;
        }

        var sb = new StringBuilder("\"");
        foreach (var c in value)
        {
            if (c == '"')
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        return sb.Append('"').ToString();
    }
}

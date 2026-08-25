using System.CommandLine;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Text;

namespace Lego2STL.Cli.Commands;

/// <summary>
/// The options that shape a run, declared once and shared by the commands that take them.
/// </summary>
/// <remarks>
/// Both <c>extract</c> and <c>build</c> end up in the same pipeline, so they accept the same
/// settings; declaring each option here rather than in both commands is what keeps the two
/// spellings, descriptions and defaults from drifting. It is also the list the window mirrors,
/// so anything added here has exactly one place to be added.
/// </remarks>
internal sealed class PipelineOptions
{
    public PipelineOptions(Strings words)
    {
        ArgumentNullException.ThrowIfNull(words);

        // ---- Input ---------------------------------------------------------------------

        ColorScheme = new Option<ColorScheme>("--color-scheme")
        {
            Description = words[TextKey.HelpOptColorScheme],
            DefaultValueFactory = _ => Core.Colors.ColorScheme.BrickLink,
        };

        IncludeSpares = new Option<bool>("--include-spares")
        {
            Description = words[TextKey.HelpOptIncludeSpares],
        };

        // ---- Stages --------------------------------------------------------------------

        CsvOnly = new Option<bool>("--csv-only") { Description = words[TextKey.HelpOptCsvOnly] };

        NoPlates = new Option<bool>("--no-plates") { Description = words[TextKey.HelpOptNoPlates] };

        // ---- Output --------------------------------------------------------------------

        OutputDirectory = new Option<DirectoryInfo?>("--output-dir")
        {
            Description = words[TextKey.HelpOptOutputDir],
        };

        Delimiter = new Option<string?>("--delimiter")
        {
            Description = words[TextKey.HelpOptDelimiter],
        };

        Ascii = new Option<bool>("--ascii") { Description = words[TextKey.HelpOptAscii] };

        // ---- Geometry ------------------------------------------------------------------

        KeepOrigin = new Option<bool>("--keep-origin")
        {
            Description = words[TextKey.HelpOptKeepOrigin],
        };

        Scale = new Option<double>("--scale")
        {
            Description = words[TextKey.HelpOptScale],
            DefaultValueFactory = _ => 100.0,
        };

        Clearance = new Option<double>("--clearance")
        {
            Description = words[TextKey.HelpOptClearance],
            DefaultValueFactory = _ => 0.0,
        };

        NoRepair = new Option<bool>("--no-repair") { Description = words[TextKey.HelpOptNoRepair] };

        NoSeamRepair = new Option<bool>("--no-seam-repair")
        {
            Description = words[TextKey.HelpOptNoSeamRepair],
        };

        WeldTolerance = new Option<double>("--weld-tolerance")
        {
            Description = words[TextKey.HelpOptWeldTolerance],
            DefaultValueFactory = _ => VertexWelder.DefaultToleranceUnits,
        };

        // ---- Shape library -------------------------------------------------------------

        LDrawDirectory = new Option<DirectoryInfo?>("--ldraw-dir")
        {
            Description = words[TextKey.HelpOptLDrawDir],
        };

        LDrawCache = new Option<DirectoryInfo?>("--ldraw-cache")
        {
            Description = words[TextKey.HelpOptLDrawCache],
        };

        Offline = new Option<bool>("--offline") { Description = words[TextKey.HelpOptOffline] };

        NoUnofficial = new Option<bool>("--no-unofficial")
        {
            Description = words[TextKey.HelpOptNoUnofficial],
        };

        // ---- Plates --------------------------------------------------------------------

        Printer = new Option<string>("--printer")
        {
            Description = words.Format(TextKey.HelpOptPrinter, string.Join(", ", PrintBeds.Names)),
            DefaultValueFactory = _ => PrintBeds.Default.Name,
        };

        PlateSize = new Option<string?>("--plate-size")
        {
            Description = words[TextKey.HelpOptPlateSize],
        };

        PlateSpacing = new Option<double>("--plate-spacing")
        {
            Description = words[TextKey.HelpOptPlateSpacing],
            DefaultValueFactory = _ => 3.0,
        };

        // ---- Behaviour -----------------------------------------------------------------

        ApiKey = new Option<string?>("--api-key") { Description = words[TextKey.HelpOptApiKey] };

        Quiet = new Option<bool>("--quiet") { Description = words[TextKey.HelpOptQuiet] };

        LogFile = new Option<FileInfo?>("--log") { Description = words[TextKey.HelpOptLog] };
    }

    public Option<ColorScheme> ColorScheme { get; }

    public Option<bool> IncludeSpares { get; }

    public Option<bool> CsvOnly { get; }

    public Option<bool> NoPlates { get; }

    public Option<DirectoryInfo?> OutputDirectory { get; }

    public Option<string?> Delimiter { get; }

    public Option<bool> Ascii { get; }

    public Option<bool> KeepOrigin { get; }

    public Option<double> Scale { get; }

    public Option<double> Clearance { get; }

    public Option<bool> NoRepair { get; }

    public Option<bool> NoSeamRepair { get; }

    public Option<double> WeldTolerance { get; }

    public Option<DirectoryInfo?> LDrawDirectory { get; }

    public Option<DirectoryInfo?> LDrawCache { get; }

    public Option<bool> Offline { get; }

    public Option<bool> NoUnofficial { get; }

    public Option<string> Printer { get; }

    public Option<string?> PlateSize { get; }

    public Option<double> PlateSpacing { get; }

    public Option<string?> ApiKey { get; }

    public Option<bool> Quiet { get; }

    public Option<FileInfo?> LogFile { get; }

    /// <summary>Adds every option to a command, in the order they should appear in help.</summary>
    public void AddTo(Command command, bool includeDocumentOptions)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (includeDocumentOptions)
        {
            command.Options.Add(ColorScheme);
        }

        foreach (var option in new Option[]
        {
            IncludeSpares, CsvOnly, NoPlates, OutputDirectory, Delimiter, Ascii,
            KeepOrigin, Scale, Clearance, NoRepair, NoSeamRepair, WeldTolerance,
            LDrawDirectory, LDrawCache, Offline, NoUnofficial,
            Printer, PlateSize, PlateSpacing,
            ApiKey, Quiet, LogFile,
        })
        {
            command.Options.Add(option);
        }
    }

    /// <summary>Gathers what was typed into the settings the pipeline takes.</summary>
    public RunSettings Read(ParseResult parseResult, InputKind kind, string? inputPath, string? pages)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        var csvOnly = parseResult.GetValue(CsvOnly);
        var noPlates = parseResult.GetValue(NoPlates);

        return new RunSettings
        {
            Kind = kind,
            InputPath = inputPath,
            SetNumber = kind == InputKind.SetNumber ? inputPath : null,
            Pages = pages,
            ColorScheme = parseResult.GetValue(ColorScheme),
            IncludeSpares = parseResult.GetValue(IncludeSpares),

            Stages = csvOnly
                ? RunStages.PartsListOnly
                : noPlates
                    ? RunStages.Shapes
                    : RunStages.ShapesAndPlates,

            OutputDirectory = parseResult.GetValue(OutputDirectory)?.FullName,
            Delimiter = ParseDelimiter(parseResult.GetValue(Delimiter)),
            AsciiStl = parseResult.GetValue(Ascii),

            KeepOrigin = parseResult.GetValue(KeepOrigin),
            ScalePercent = parseResult.GetValue(Scale),
            Clearance = parseResult.GetValue(Clearance),
            FillGaps = !parseResult.GetValue(NoRepair),
            NoSeamRepair = parseResult.GetValue(NoSeamRepair),
            WeldTolerance = parseResult.GetValue(WeldTolerance),

            LDrawDirectory = parseResult.GetValue(LDrawDirectory)?.FullName,
            LDrawCache = parseResult.GetValue(LDrawCache)?.FullName,
            Offline = parseResult.GetValue(Offline),
            IncludeUnofficial = !parseResult.GetValue(NoUnofficial),

            Printer = parseResult.GetValue(Printer) ?? PrintBeds.Default.Name,
            PlateSize = parseResult.GetValue(PlateSize),
            PlateSpacing = parseResult.GetValue(PlateSpacing),

            Language = parseResult.GetValue(CommonOptions.Language),
            ApiKey = parseResult.GetValue(ApiKey),
            Quiet = parseResult.GetValue(Quiet),
            LogFile = parseResult.GetValue(LogFile)?.FullName,
        };
    }

    private static char ParseDelimiter(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return PartsListCsv.DefaultDelimiter;
        }

        if (text is "\\t" or "tab")
        {
            return '\t';
        }

        return text.Length == 1
            ? text[0]
            : throw new ArgumentException($"--delimiter takes a single character, or \"tab\"; got '{text}'.");
    }
}

using System.CommandLine;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Plates;

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
    // ---- Input -------------------------------------------------------------------------

    public Option<ColorScheme> ColorScheme { get; } = new("--color-scheme")
    {
        Description = "Whose colour numbering the document prints.",
        DefaultValueFactory = _ => Core.Colors.ColorScheme.BrickLink,
    };

    public Option<bool> IncludeSpares { get; } = new("--include-spares")
    {
        Description = "Keep a set's spare pieces as well as the ones the model uses.",
    };

    // ---- Stages ------------------------------------------------------------------------

    public Option<bool> CsvOnly { get; } = new("--csv-only")
    {
        Description = "Write the parts list and stop, without making any shapes.",
    };

    public Option<bool> NoPlates { get; } = new("--no-plates")
    {
        Description = "Write the shape files but no coloured plates.",
    };

    // ---- Output ------------------------------------------------------------------------

    public Option<DirectoryInfo?> OutputDirectory { get; } = new("--output-dir")
    {
        Description = "Where to put the run folder. Defaults to beside the input.",
    };

    public Option<string?> Delimiter { get; } = new("--delimiter")
    {
        Description = "Separator for the parts list, or \"tab\". Defaults to a semicolon.",
    };

    public Option<bool> Ascii { get; } = new("--ascii")
    {
        Description = "Write the readable form of STL instead of the compact one.",
    };

    // ---- Geometry ----------------------------------------------------------------------

    public Option<bool> KeepOrigin { get; } = new("--keep-origin")
    {
        Description = "Keep each part's own origin instead of standing it on the bed.",
    };

    public Option<double> Scale { get; } = new("--scale")
    {
        Description = "Scale as a percentage. 100 is true size.",
        DefaultValueFactory = _ => 100.0,
    };

    public Option<double> Clearance { get; } = new("--clearance")
    {
        Description =
            "Take every face in by this many millimetres, so printed parts clip together. " +
            "Use the calibration command to find the right figure for your printer.",
        DefaultValueFactory = _ => 0.0,
    };

    public Option<bool> Repair { get; } = new("--repair")
    {
        Description =
            "Cover over the gaps a shape's surface arrives with, making it a solid. Needed " +
            "before a clearance can be applied to a part that has any.",
    };

    public Option<bool> NoSeamRepair { get; } = new("--no-seam-repair")
    {
        Description = "Skip closing seams where a corner lies part-way along another edge.",
    };

    public Option<double> WeldTolerance { get; } = new("--weld-tolerance")
    {
        Description = "How close two corners have to be to count as the same point.",
        DefaultValueFactory = _ => VertexWelder.DefaultToleranceMillimetres,
    };

    // ---- Shape library -----------------------------------------------------------------

    public Option<DirectoryInfo?> LDrawDirectory { get; } = new("--ldraw-dir")
    {
        Description = "An existing LDraw library to use instead of downloading anything.",
    };

    public Option<DirectoryInfo?> LDrawCache { get; } = new("--ldraw-cache")
    {
        Description = "Where to keep fetched shape files between runs.",
    };

    public Option<bool> Offline { get; } = new("--offline")
    {
        Description = "Never use the network; a part that is not already available is reported.",
    };

    public Option<bool> NoUnofficial { get; } = new("--no-unofficial")
    {
        Description = "Use only the official shape library, not the unofficial collection.",
    };

    // ---- Plates ------------------------------------------------------------------------

    public Option<string> Printer { get; } = new("--printer")
    {
        Description = $"Which printer's bed to lay plates out for: {string.Join(", ", PrintBeds.Names)}.",
        DefaultValueFactory = _ => PrintBeds.Default.Name,
    };

    public Option<string?> PlateSize { get; } = new("--plate-size")
    {
        Description = "A bed size in millimetres instead of a printer name, e.g. 220x220 or 300x300x400.",
    };

    public Option<double> PlateSpacing { get; } = new("--plate-spacing")
    {
        Description = "Millimetres to leave between parts on a plate.",
        DefaultValueFactory = _ => 3.0,
    };

    // ---- Behaviour ---------------------------------------------------------------------

    public Option<string?> ApiKey { get; } = new("--api-key")
    {
        Description = "A Rebrickable key, for looking up a set. Also read from REBRICKABLE_API_KEY.",
    };

    public Option<bool> Quiet { get; } = new("--quiet")
    {
        Description = "Say only what matters: warnings, failures and where things were written.",
    };

    public Option<FileInfo?> LogFile { get; } = new("--log")
    {
        Description = "Also write everything said during the run to this file.",
    };

    /// <summary>Adds every option to a command, in the order they should appear in help.</summary>
    public void AddTo(Command command, bool includeDocumentOptions)
    {
        if (includeDocumentOptions)
        {
            command.Options.Add(ColorScheme);
        }

        command.Options.Add(IncludeSpares);
        command.Options.Add(CsvOnly);
        command.Options.Add(NoPlates);
        command.Options.Add(OutputDirectory);
        command.Options.Add(Delimiter);
        command.Options.Add(Ascii);
        command.Options.Add(KeepOrigin);
        command.Options.Add(Scale);
        command.Options.Add(Clearance);
        command.Options.Add(Repair);
        command.Options.Add(NoSeamRepair);
        command.Options.Add(WeldTolerance);
        command.Options.Add(LDrawDirectory);
        command.Options.Add(LDrawCache);
        command.Options.Add(Offline);
        command.Options.Add(NoUnofficial);
        command.Options.Add(Printer);
        command.Options.Add(PlateSize);
        command.Options.Add(PlateSpacing);
        command.Options.Add(ApiKey);
        command.Options.Add(Quiet);
        command.Options.Add(LogFile);
    }

    /// <summary>Gathers what was typed into the settings the pipeline takes.</summary>
    public RunSettings Read(ParseResult parseResult, InputKind kind, string? inputPath, string? pages)
    {
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
            FillGaps = parseResult.GetValue(Repair),
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

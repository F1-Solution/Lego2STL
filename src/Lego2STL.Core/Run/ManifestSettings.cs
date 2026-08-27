using Lego2STL.Core.Colors;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Text;

namespace Lego2STL.Core.Run;

/// <summary>
/// Everything a run was asked for, as the manifest stores it.
/// </summary>
/// <remarks>
/// A shape of its own rather than <see cref="RunSettings"/> itself, because that record
/// computes several properties on the way out - a bed size among them, which throws on a size
/// that does not parse - and a serialiser would call every one of them. This holds only what
/// was chosen.
/// </remarks>
public sealed record ManifestSettings
{
    public InputKind Kind { get; init; }

    public string? InputPath { get; init; }

    public string? SetNumber { get; init; }

    public string? Pages { get; init; }

    public ColorScheme ColorScheme { get; init; }

    public bool IncludeSpares { get; init; }

    public RunStages Stages { get; init; }

    public string? OutputDirectory { get; init; }

    public string Delimiter { get; init; } = ";";

    public bool AsciiStl { get; init; }

    public bool KeepOrigin { get; init; }

    public double ScalePercent { get; init; }

    public double Clearance { get; init; }

    public bool FillGaps { get; init; }

    public bool NoSeamRepair { get; init; }

    public double WeldTolerance { get; init; }

    public string? LDrawDirectory { get; init; }

    public string? LDrawCache { get; init; }

    public bool Offline { get; init; }

    public bool IncludeUnofficial { get; init; }

    public string? Printer { get; init; }

    public string? PlateSize { get; init; }

    public double PlateSpacing { get; init; }

    public DisplayLanguage Language { get; init; }

    /// <summary>Masked, always. See <see cref="RunSettings.MaskApiKey"/>.</summary>
    public string? ApiKey { get; init; }

    public bool Quiet { get; init; }

    public string? LogFile { get; init; }

    public static ManifestSettings From(RunSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new ManifestSettings
        {
            Kind = settings.Kind,
            InputPath = settings.InputPath,
            SetNumber = settings.SetNumber,
            Pages = settings.Pages,
            ColorScheme = settings.ColorScheme,
            IncludeSpares = settings.IncludeSpares,
            Stages = settings.Stages,
            OutputDirectory = settings.OutputDirectory,
            Delimiter = settings.Delimiter.ToString(),
            AsciiStl = settings.AsciiStl,
            KeepOrigin = settings.KeepOrigin,
            ScalePercent = settings.ScalePercent,
            Clearance = settings.Clearance,
            FillGaps = settings.FillGaps,
            NoSeamRepair = settings.NoSeamRepair,
            WeldTolerance = settings.WeldTolerance,
            LDrawDirectory = settings.LDrawDirectory,
            LDrawCache = settings.LDrawCache,
            Offline = settings.Offline,
            IncludeUnofficial = settings.IncludeUnofficial,
            Printer = settings.Printer,
            PlateSize = settings.PlateSize,
            PlateSpacing = settings.PlateSpacing,
            Language = settings.Language,
            ApiKey = RunSettings.MaskApiKey(settings.ApiKey),
            Quiet = settings.Quiet,
            LogFile = settings.LogFile,
        };
    }

    /// <summary>What was chosen, ready to run again.</summary>
    /// <remarks>
    /// The key comes back as nothing rather than as its placeholder: what is on disk is a
    /// stand-in, and handing it to the API would fail in a way nobody could explain.
    /// </remarks>
    public RunSettings ToSettings()
    {
        var defaults = new RunSettings();

        return new RunSettings
        {
            Kind = Kind,
            InputPath = InputPath,
            SetNumber = SetNumber,
            Pages = Pages,
            ColorScheme = ColorScheme,
            IncludeSpares = IncludeSpares,
            Stages = Stages,
            OutputDirectory = OutputDirectory,
            Delimiter = Delimiter is { Length: > 0 } ? Delimiter[0] : defaults.Delimiter,
            AsciiStl = AsciiStl,
            KeepOrigin = KeepOrigin,
            ScalePercent = ScalePercent,
            Clearance = Clearance,
            FillGaps = FillGaps,
            NoSeamRepair = NoSeamRepair,
            WeldTolerance = WeldTolerance,
            LDrawDirectory = LDrawDirectory,
            LDrawCache = LDrawCache,
            Offline = Offline,
            IncludeUnofficial = IncludeUnofficial,
            Printer = Printer ?? defaults.Printer,
            PlateSize = PlateSize,
            PlateSpacing = PlateSpacing,
            Language = Language,
            ApiKey = string.Equals(ApiKey, RunSettings.MaskedApiKey, StringComparison.Ordinal)
                ? null
                : ApiKey,
            Quiet = Quiet,
            LogFile = LogFile,
        };
    }
}

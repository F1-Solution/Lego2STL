using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Text;

namespace Lego2STL.Gui.ViewModels;

/// <summary>
/// Every setting a run can be given, as something the window can bind to.
/// </summary>
/// <remarks>
/// <para>
/// This is the window's half of the parity promise: one property here for every option the
/// command line takes, turning into the same <see cref="RunSettings"/> the terminal builds.
/// Nothing is done here that the command line cannot do, and nothing the command line does is
/// missing here.
/// </para>
/// <para>
/// Every property re-announces the equivalent command when it changes, which is how the
/// window can show what it is about to do in a form that can be copied, kept, or put in a
/// script. It is also the quickest way to see that a setting really did take.
/// </para>
/// </remarks>
public sealed partial class RunOptionsViewModel : ViewModelBase
{
    // ---- Input -------------------------------------------------------------------------

    [ObservableProperty]
    public partial InputKind Kind { get; set; } = InputKind.Document;

    [ObservableProperty]
    public partial string? DocumentPath { get; set; }

    [ObservableProperty]
    public partial string? PartsListPath { get; set; }

    [ObservableProperty]
    public partial string? SetNumber { get; set; }

    [ObservableProperty]
    public partial string? Pages { get; set; }

    [ObservableProperty]
    public partial ColorScheme ColorScheme { get; set; } = ColorScheme.BrickLink;

    [ObservableProperty]
    public partial bool IncludeSpares { get; set; }

    // ---- Stages ------------------------------------------------------------------------

    [ObservableProperty]
    public partial bool MakeShapes { get; set; } = true;

    [ObservableProperty]
    public partial bool MakePlates { get; set; } = true;

    // ---- Output ------------------------------------------------------------------------

    [ObservableProperty]
    public partial string? OutputDirectory { get; set; }

    [ObservableProperty]
    public partial string Delimiter { get; set; } = ";";

    [ObservableProperty]
    public partial bool AsciiStl { get; set; }

    // ---- Geometry ----------------------------------------------------------------------

    [ObservableProperty]
    public partial bool KeepOrigin { get; set; }

    [ObservableProperty]
    public partial double ScalePercent { get; set; } = 100.0;

    [ObservableProperty]
    public partial double Clearance { get; set; }

    [ObservableProperty]
    public partial bool FillGaps { get; set; }

    [ObservableProperty]
    public partial bool NoSeamRepair { get; set; }

    [ObservableProperty]
    public partial double WeldTolerance { get; set; } = VertexWelder.DefaultToleranceMillimetres;

    // ---- Shape library -----------------------------------------------------------------

    [ObservableProperty]
    public partial string? LDrawDirectory { get; set; }

    [ObservableProperty]
    public partial string? LDrawCache { get; set; }

    [ObservableProperty]
    public partial bool Offline { get; set; }

    [ObservableProperty]
    public partial bool IncludeUnofficial { get; set; } = true;

    // ---- Plates ------------------------------------------------------------------------

    [ObservableProperty]
    public partial string Printer { get; set; } = PrintBeds.Default.Name;

    [ObservableProperty]
    public partial string? PlateSize { get; set; }

    [ObservableProperty]
    public partial double PlateSpacing { get; set; } = 3.0;

    // ---- Behaviour ---------------------------------------------------------------------

    [ObservableProperty]
    public partial DisplayLanguage Language { get; set; } = DisplayLanguages.FromEnvironment();

    [ObservableProperty]
    public partial string? ApiKey { get; set; }

    [ObservableProperty]
    public partial string? LogFile { get; set; }

    // ---- What the menus offer ------------------------------------------------------------

    public static IReadOnlyList<ColorScheme> ColorSchemes { get; } = Enum.GetValues<ColorScheme>();

    public static IReadOnlyList<string> Printers { get; } = PrintBeds.Names;

    public static IReadOnlyList<string> Delimiters { get; } = [";", ",", "tab"];

    /// <summary>
    /// The three settings the window states the other way round, because the flag is named
    /// for turning something off.
    /// </summary>
    /// <remarks>
    /// Written out rather than bound through a negation, so that checking the box really does
    /// change the setting. A binding that inverts on the way out but not on the way back is a
    /// control that looks right and does nothing, which is worse than one that looks wrong.
    /// </remarks>
    public bool CsvOnly
    {
        get => !MakeShapes;
        set => MakeShapes = !value;
    }

    public bool NoPlates
    {
        get => !MakePlates;
        set => MakePlates = !value;
    }

    public bool NoUnofficial
    {
        get => !IncludeUnofficial;
        set => IncludeUnofficial = !value;
    }

    partial void OnMakeShapesChanged(bool value) => OnPropertyChanged(nameof(CsvOnly));

    partial void OnMakePlatesChanged(bool value) => OnPropertyChanged(nameof(NoPlates));

    partial void OnIncludeUnofficialChanged(bool value) => OnPropertyChanged(nameof(NoUnofficial));

    /// <summary>The input path for whichever kind of start was chosen.</summary>
    public string? EffectiveInputPath => Kind switch
    {
        InputKind.Document => DocumentPath,
        InputKind.PartsList => PartsListPath,
        _ => null,
    };

    public bool IsDocument => Kind == InputKind.Document;

    public bool IsPartsList => Kind == InputKind.PartsList;

    public bool IsSetNumber => Kind == InputKind.SetNumber;

    // Radio buttons set the kind through a command rather than binding IsChecked both ways:
    // a two-way group binding fires once for the button turning off as well as the one turning
    // on, which makes the selection fight itself.
    [RelayCommand]
    private void SetDocument() => Kind = InputKind.Document;

    [RelayCommand]
    private void SetPartsList() => Kind = InputKind.PartsList;

    [RelayCommand]
    private void SetSetNumber() => Kind = InputKind.SetNumber;

    /// <summary>Everything gathered into the form the pipeline takes.</summary>
    public RunSettings ToSettings() => new()
    {
        Kind = Kind,
        InputPath = EffectiveInputPath,
        SetNumber = SetNumber,
        Pages = Pages,
        ColorScheme = ColorScheme,
        IncludeSpares = IncludeSpares,

        Stages = !MakeShapes
            ? RunStages.PartsListOnly
            : MakePlates
                ? RunStages.ShapesAndPlates
                : RunStages.Shapes,

        OutputDirectory = Blank(OutputDirectory),
        Delimiter = Delimiter == "tab" ? '\t' : (Delimiter.Length > 0 ? Delimiter[0] : ';'),
        AsciiStl = AsciiStl,

        KeepOrigin = KeepOrigin,
        ScalePercent = ScalePercent,
        Clearance = Clearance,
        FillGaps = FillGaps,
        NoSeamRepair = NoSeamRepair,
        WeldTolerance = WeldTolerance,

        LDrawDirectory = Blank(LDrawDirectory),
        LDrawCache = Blank(LDrawCache),
        Offline = Offline,
        IncludeUnofficial = IncludeUnofficial,

        Printer = Printer,
        PlateSize = Blank(PlateSize),
        PlateSpacing = PlateSpacing,

        Language = Language,
        ApiKey = Blank(ApiKey),
        LogFile = Blank(LogFile),
    };

    /// <summary>The command that would do the same thing from a terminal.</summary>
    public string CommandLine => ToSettings().ToCommandLine();

    /// <summary>Anything that would stop the run, ready to show before it is started.</summary>
    public string? Problem => ToSettings().Problems() is { Count: > 0 } problems
        ? string.Join(" ", problems)
        : null;

    public bool CanRun => Problem is null;

    /// <summary>
    /// Every change re-announces the command line and whether the run is ready, because both
    /// are derived from the whole of this object rather than from any one property.
    /// </summary>
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is nameof(CommandLine) or nameof(Problem) or nameof(CanRun))
        {
            return;
        }

        OnPropertyChanged(nameof(CommandLine));
        OnPropertyChanged(nameof(Problem));
        OnPropertyChanged(nameof(CanRun));

        if (e.PropertyName == nameof(Kind))
        {
            OnPropertyChanged(nameof(IsDocument));
            OnPropertyChanged(nameof(IsPartsList));
            OnPropertyChanged(nameof(IsSetNumber));
            OnPropertyChanged(nameof(EffectiveInputPath));
        }
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Text;
using Lego2STL.Gui.Localization;

namespace Lego2STL.Gui.ViewModels;

/// <summary>
/// The eighteen options a run is shaped by, as a list that can be searched and narrowed.
/// </summary>
/// <remarks>
/// <para>
/// Eighteen rows is more than anyone reads, and the reason a setting from three runs ago goes
/// unnoticed. So: search by flag or by what the option does, and a way to show only what was
/// moved off its default. What is hidden is never silent - the toggle carries the count - and
/// hidden rows stay in the list, so nothing has to be re-materialised and nothing drops out of
/// the window's tree while it is out of sight.
/// </para>
/// <para>
/// What counts as changed is measured against a fresh <see cref="RunOptionsViewModel"/>, which
/// is the same comparison the shown command line already makes, so this costs nothing new and
/// cannot disagree with it.
/// </para>
/// </remarks>
public sealed partial class OptionRowsViewModel : ViewModelBase
{
    /// <param name="changedOnly">
    /// Whether to open narrowed to what was changed. Given by whoever knows whether a run has
    /// happened before rather than worked out here, so the history has the two readers it is
    /// meant to have and this stays answerable without one.
    /// </param>
    public OptionRowsViewModel(RunOptionsViewModel options, bool changedOnly = false)
    {
        Rows = Build(options);
        ChangedOnly = changedOnly;

        options.PropertyChanged += WhateverChanged;
        Apply();
    }

    public IReadOnlyList<OptionRowViewModel> Rows { get; }

    [ObservableProperty]
    public partial string? Search { get; set; }

    /// <summary>
    /// Show only what was moved off its default.
    /// </summary>
    /// <remarks>
    /// On from the second run onward and off on a first, because on a first run there is
    /// nothing to have changed and an empty list is a worse introduction than a long one.
    /// Whether a run has ever happened is already recorded, so this needs no preference of
    /// its own.
    /// </remarks>
    [ObservableProperty]
    public partial bool ChangedOnly { get; set; }

    /// <summary>How many rows the filters are keeping out of sight.</summary>
    public int HiddenCount => Rows.Count(row => !row.IsVisible);

    public bool HasHidden => HiddenCount > 0;

    /// <summary>The count in words, so what is out of sight is never out of sight in silence.</summary>
    public string HiddenLabel => Loc.Current.Format(TextKey.UiHiddenCount, HiddenCount);

    partial void OnSearchChanged(string? value) => Apply();

    partial void OnChangedOnlyChanged(bool value) => Apply();

    [RelayCommand]
    private void ResetAll()
    {
        foreach (var row in Rows)
        {
            row.Reset();
        }

        Refresh();
    }

    private void WhateverChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        foreach (var row in Rows)
        {
            row.Refresh();
        }

        Apply();
    }

    private void Apply()
    {
        foreach (var row in Rows)
        {
            row.IsVisible = row.Matches(Search) && (!ChangedOnly || row.IsChanged);
        }

        OnPropertyChanged(nameof(HiddenCount));
        OnPropertyChanged(nameof(HasHidden));
        OnPropertyChanged(nameof(HiddenLabel));
    }

    /// <summary>The chosen printer's bed, falling back to the default rather than throwing.</summary>
    private static PrintBed BedOf(string? printer) =>
        PrintBeds.TryGetByName(printer, out var bed) ? bed : PrintBeds.Default;

    /// <summary>
    /// The nineteen, in five kinds. A fresh instance supplies every default, so a default is
    /// never written down twice and cannot drift from the one a run would really use.
    /// </summary>
    private static List<OptionRowViewModel> Build(RunOptionsViewModel o)
    {
        var fresh = new RunOptionsViewModel();

        return
        [
            new ToggleOptionRow("--csv-only", TextKey.LabelOptCsvOnly, TextKey.HelpOptCsvOnly,
                () => o.CsvOnly, v => o.CsvOnly = v, fresh.CsvOnly),

            new ToggleOptionRow("--no-plates", TextKey.LabelOptNoPlates, TextKey.HelpOptNoPlates,
                () => o.NoPlates, v => o.NoPlates = v, fresh.NoPlates)
            {
                Enabled = () => !o.CsvOnly,
            },

            new ToggleOptionRow("--ascii", TextKey.LabelOptAscii, TextKey.HelpOptAscii,
                () => o.AsciiStl, v => o.AsciiStl = v, fresh.AsciiStl),

            new ToggleOptionRow("--keep-origin", TextKey.LabelOptKeepOrigin, TextKey.HelpOptKeepOrigin,
                () => o.KeepOrigin, v => o.KeepOrigin = v, fresh.KeepOrigin),

            new ToggleOptionRow("--no-repair", TextKey.LabelOptNoRepair, TextKey.HelpOptNoRepair,
                () => o.NoRepair, v => o.NoRepair = v, fresh.NoRepair),

            new ToggleOptionRow("--no-seam-repair", TextKey.LabelOptNoSeamRepair, TextKey.HelpOptNoSeamRepair,
                () => o.NoSeamRepair, v => o.NoSeamRepair = v, fresh.NoSeamRepair),

            new ToggleOptionRow("--offline", TextKey.LabelOptOffline, TextKey.HelpOptOffline,
                () => o.Offline, v => o.Offline = v, fresh.Offline),

            new ToggleOptionRow("--no-unofficial", TextKey.LabelOptNoUnofficial, TextKey.HelpOptNoUnofficial,
                () => o.NoUnofficial, v => o.NoUnofficial = v, fresh.NoUnofficial),

            new NumberOptionRow("--scale", TextKey.LabelOptScale, TextKey.HelpOptScale,
                () => o.ScalePercent, v => o.ScalePercent = v, fresh.ScalePercent)
            {
                Minimum = 1, Maximum = 1000, Increment = 1, Format = "0.##",
            },

            new NumberOptionRow("--clearance", TextKey.LabelOptClearance, TextKey.HelpOptClearance,
                () => o.Clearance, v => o.Clearance = v, fresh.Clearance)
            {
                Minimum = 0, Maximum = 5, Increment = 0.01, Format = "0.000",
            },

            new NumberOptionRow("--weld-tolerance", TextKey.LabelOptWeldTolerance, TextKey.HelpOptWeldTolerance,
                () => o.WeldTolerance, v => o.WeldTolerance = v, fresh.WeldTolerance)
            {
                Minimum = 0, Maximum = 5, Increment = 0.001, Format = "0.000",
            },

            new NumberOptionRow("--plate-spacing", TextKey.LabelOptPlateSpacing, TextKey.HelpOptPlateSpacing,
                () => o.PlateSpacing, v => o.PlateSpacing = v, fresh.PlateSpacing)
            {
                Minimum = 0, Maximum = 50, Increment = 0.5, Format = "0.##",
                Enabled = () => !o.CsvOnly && !o.NoPlates,
            },

            new PathOptionRow("--output-dir", TextKey.LabelOptOutputDir, TextKey.HelpOptOutputDir,
                () => o.OutputDirectory, v => o.OutputDirectory = v, fresh.OutputDirectory),

            new PathOptionRow("--element-map", TextKey.LabelOptElementMap, TextKey.HelpOptElementMap,
                () => o.ElementMap, v => o.ElementMap = v, fresh.ElementMap),

            new PathOptionRow("--ldraw-dir", TextKey.LabelOptLDrawDir, TextKey.HelpOptLDrawDir,
                () => o.LDrawDirectory, v => o.LDrawDirectory = v, fresh.LDrawDirectory),

            new PathOptionRow("--ldraw-cache", TextKey.LabelOptLDrawCache, TextKey.HelpOptLDrawCache,
                () => o.LDrawCache, v => o.LDrawCache = v, fresh.LDrawCache),

            new TextOptionRow("--plate-size", TextKey.LabelOptPlateSize, TextKey.HelpOptPlateSize,
                () => o.PlateSize, v => o.PlateSize = v, fresh.PlateSize)
            {
                // The bed of whichever printer is chosen, so an untouched box says what the run
                // would really use rather than a size no default ever had.
                WhenEmpty = () => BedOf(o.Printer).AsSize,
                Enabled = () => !o.CsvOnly && !o.NoPlates,
            },

            new ChoiceOptionRow("--delimiter", TextKey.LabelOptDelimiter, TextKey.HelpOptDelimiter,
                () => o.Delimiter, v => o.Delimiter = v ?? fresh.Delimiter, fresh.Delimiter,
                RunOptionsViewModel.Delimiters),

            new ChoiceOptionRow("--printer", TextKey.LabelOptPrinter, TextKey.HelpOptPrinter,
                () => o.Printer, v => o.Printer = v ?? fresh.Printer, fresh.Printer,
                RunOptionsViewModel.Printers)
            {
                HelpValues = [string.Join(", ", RunOptionsViewModel.Printers)],
                Enabled = () => !o.CsvOnly && !o.NoPlates,
            },
        ];
    }
}

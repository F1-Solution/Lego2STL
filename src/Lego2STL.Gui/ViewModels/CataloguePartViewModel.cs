using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lego2STL.Core.Run;
using Lego2STL.Gui.Services;

namespace Lego2STL.Gui.ViewModels;

/// <summary>
/// One part in the catalogue: what it is, how many, and what came of it.
/// </summary>
/// <remarks>
/// The warnings are the reason this screen exists rather than a plain list. A shape with gaps
/// in its surface and a shape with walls thinner than the nozzle both print, and both print
/// badly, and neither says so anywhere else. Saying it beside the picture, before anything is
/// sent to a printer, is the whole point.
/// </remarks>
public sealed partial class CataloguePartViewModel : ViewModelBase
{
    /// <remarks>
    /// Takes what the run recorded rather than a shape in memory, because a run reopened weeks
    /// later has no shape in memory and never will: nothing reads a mesh back off the disk, and
    /// a re-welded count of open edges would depend on the tolerance it was welded at. Both
    /// front ends therefore read the same measured facts, and a reopened run cannot quietly
    /// disagree with what the run itself reported.
    /// </remarks>
    public CataloguePartViewModel(RunDocumentPart part, string? shapePath, string? platePath)
    {
        Part = part;
        ShapePath = shapePath;
        PlatePath = platePath;

        Swatch = new SolidColorBrush(Color.FromRgb(part.Rgb.R, part.Rgb.G, part.Rgb.B));
    }

    public RunDocumentPart Part { get; }

    public string? ShapePath { get; }

    public string? PlatePath { get; }

    public string PartNumber => Part.PartNumber;

    public string ColorName => Part.ColorName;

    public int Quantity => Part.Quantity;

    public int BrickLinkColorCode => Part.BrickLinkColorCode;

    public IBrush Swatch { get; }

    [ObservableProperty]
    public partial Bitmap? Picture { get; set; }

    public string Size => Part.Size ?? string.Empty;

    public string? Title => Part.Title;

    public bool HasOpenEdges => Part.HasOpenEdges;

    /// <summary>True when some part of the shape is finer than a nozzle can print.</summary>
    public bool HasThinFeatures => Part.HasThinFeatures;

    public bool HasWarning => Part.HasWarning;

    public IReadOnlyList<string> Warnings
    {
        get
        {
            var warnings = new List<string>();

            if (HasOpenEdges)
            {
                warnings.Add(Localization.Loc.Current.Text(Core.Text.TextKey.UiWarningNotClosed));
            }

            if (HasThinFeatures)
            {
                warnings.Add(Localization.Loc.Current.Text(Core.Text.TextKey.UiWarningThinFeature));
            }

            return warnings;
        }
    }

    public string WarningText => string.Join("  ", Warnings);

    public bool HasShapeFile => ShapePath is not null && File.Exists(ShapePath);

    public bool HasPlateFile => PlatePath is not null && File.Exists(PlatePath);

    [RelayCommand]
    private void OpenShape()
    {
        if (ShapePath is not null)
        {
            Desktop.Open(ShapePath);
        }
    }

    [RelayCommand]
    private void OpenPlate()
    {
        if (PlatePath is not null)
        {
            Desktop.Open(PlatePath);
        }
    }

    [RelayCommand]
    private void OpenFolder() => Desktop.Reveal(ShapePath ?? PlatePath ?? string.Empty);

    /// <summary>Whether this part matches the filter and search the catalogue is showing.</summary>
    public bool Matches(string? colorName, string? search)
    {
        if (colorName is not null && !string.Equals(colorName, ColorName, System.StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var needle = search.Trim();
        return PartNumber.Contains(needle, System.StringComparison.OrdinalIgnoreCase)
               || ColorName.Contains(needle, System.StringComparison.OrdinalIgnoreCase)
               || (Title?.Contains(needle, System.StringComparison.OrdinalIgnoreCase) ?? false);
    }
}

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

/// <summary>Which of a part's two numbers the catalogue shows.</summary>
public enum PartNumbering
{
    /// <summary>The part number, which is what the shape files are named after.</summary>
    BrickLink,

    /// <summary>The LEGO element number, which names a moulding and a colour together.</summary>
    LegoElement,
}

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
    public CataloguePartViewModel(
        RunDocumentPart part, string? shapePath, string? platePath, bool doesNotFitThePlate = false)
    {
        Part = part;
        ShapePath = shapePath;
        PlatePath = platePath;
        DoesNotFitThePlate = doesNotFitThePlate;

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

    /// <summary>True when the shape has no holes but its surfaces pass through each other.</summary>
    public bool HasSelfIntersection => Part.HasSelfIntersection;

    /// <summary>True when some part of the shape is finer than a nozzle can print.</summary>
    public bool HasThinFeatures => Part.HasThinFeatures;

    /// <summary>Which numbering to show. Set by the page, which owns the choice.</summary>
    [ObservableProperty]
    public partial PartNumbering Numbering { get; set; } = PartNumbering.BrickLink;

    partial void OnNumberingChanged(PartNumbering value) => OnPropertyChanged(nameof(ShownNumber));

    /// <summary>
    /// The number on the card, in whichever numbering was asked for.
    /// </summary>
    /// <remarks>
    /// A list read from a CSV or from a set has no element numbers, and says so rather than
    /// showing a blank that reads as missing data.
    /// </remarks>
    public string ShownNumber => Numbering switch
    {
        PartNumbering.LegoElement => Part.ElementId
                                     ?? Localization.Loc.Current.Text(Core.Text.TextKey.UiNoElementNumber),
        _ => PartNumber,
    };

    /// <summary>True when no plate could take this part at the scale the run used.</summary>
    public bool DoesNotFitThePlate { get; }

    public bool HasWarning => Part.HasWarning || DoesNotFitThePlate;

    public IReadOnlyList<string> Warnings
    {
        get
        {
            var warnings = new List<string>();

            if (HasOpenEdges)
            {
                warnings.Add(Localization.Loc.Current.Text(Core.Text.TextKey.UiWarningNotClosed));
            }

            if (HasSelfIntersection)
            {
                warnings.Add(Localization.Loc.Current.Text(Core.Text.TextKey.UiWarningSelfIntersects));
            }

            if (HasThinFeatures)
            {
                warnings.Add(Localization.Loc.Current.Text(Core.Text.TextKey.UiWarningThinFeature));
            }

            if (DoesNotFitThePlate)
            {
                warnings.Add(Localization.Loc.Current.Text(Core.Text.TextKey.UiDoesNotFitThePlate));
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

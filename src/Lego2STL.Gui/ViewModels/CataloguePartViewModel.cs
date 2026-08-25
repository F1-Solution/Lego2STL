using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.Text;
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
    /// <summary>
    /// Below this, in millimetres, a wall is thinner than a common nozzle can lay down and the
    /// slicer will either thicken it or leave it out. 0.4 mm is the usual nozzle; a wall needs
    /// at least one line of it.
    /// </summary>
    private const double NozzleWidth = 0.4;

    public CataloguePartViewModel(
        PartEntry entry,
        PreparedMesh? shape,
        string? shapePath,
        string? platePath,
        DisplayLanguage language = DisplayLanguages.Fallback)
    {
        Entry = entry;
        Shape = shape;
        ShapePath = shapePath;
        PlatePath = platePath;

        // Named once, in the run's language, because the filter list and the search both
        // compare against this and a card saying one thing while the filter says another is
        // a card that cannot be found.
        ColorName = ColorNames.For(language, entry.ColorName);

        Swatch = new SolidColorBrush(Color.FromRgb(entry.Rgb.R, entry.Rgb.G, entry.Rgb.B));
    }

    public PartEntry Entry { get; }

    public PreparedMesh? Shape { get; }

    public string? ShapePath { get; }

    public string? PlatePath { get; }

    public string PartNumber => Entry.PartNumber;

    public string ColorName { get; }

    public int Quantity => Entry.Quantity;

    public IBrush Swatch { get; }

    [ObservableProperty]
    public partial Bitmap? Picture { get; set; }

    public string Size => Shape?.DescribeSize() ?? string.Empty;

    public string? Title => Shape?.Title;

    public bool HasOpenEdges => Shape is { Quality.IsClosed: false };

    /// <summary>True when some part of the shape is finer than a nozzle can print.</summary>
    public bool HasThinFeatures =>
        Shape is not null && ClearanceOffset.ThinnestSpan(Shape.Mesh) < NozzleWidth * 2;

    public bool HasWarning => HasOpenEdges || HasThinFeatures;

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

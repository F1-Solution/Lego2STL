using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Run;
using Lego2STL.Gui.Services;

namespace Lego2STL.Gui.ViewModels;

/// <summary>
/// Turning a run's record into the cards that show it.
/// </summary>
/// <remarks>
/// Kept apart from the page that shows them because it is a different kind of work: this looks
/// in a folder and on a network, while the page it fills is about how a run stands. Which files
/// are actually there is answered by looking rather than from the record, so a shape deleted by
/// hand stops offering to be opened.
/// </remarks>
internal static class RunCatalogue
{
    public static IReadOnlyList<CataloguePartViewModel> Build(RunDocument document)
    {
        var plates = PlatesIn(document.PlateDirectory);

        return
        [
            .. document.Parts.Select(part =>
            {
                var shape = Path.Combine(document.StlDirectory, part.PartNumber + ".stl");

                return new CataloguePartViewModel(
                    part,
                    File.Exists(shape) ? shape : null,
                    PlateFor(plates, part.ColorName));
            }),
        ];
    }

    public static IEnumerable<string> ColoursIn(IEnumerable<CataloguePartViewModel> parts) =>
        parts.Select(part => part.ColorName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

    /// <summary>
    /// Fetches the pictures after the cards are already on screen.
    /// </summary>
    /// <remarks>
    /// So the catalogue appears at once and fills in, rather than waiting on a network that may
    /// not answer at all.
    /// </remarks>
    public static async Task LoadPicturesAsync(
        ThumbnailCache thumbnails,
        IReadOnlyList<CataloguePartViewModel> parts,
        bool offline)
    {
        thumbnails.Offline = offline;

        foreach (var part in parts)
        {
            if (!ColorReference.Table.TryGet(ColorScheme.BrickLink, part.BrickLinkColorCode, out var colour))
            {
                continue;
            }

            part.Picture = await thumbnails.TryGetAsync(part.PartNumber, colour).ConfigureAwait(true);
        }
    }

    /// <remarks>
    /// A plate's file is named for its colour, so the folder answers which plate a colour went
    /// on without the record having to carry a second list that could disagree with what is
    /// actually there.
    /// </remarks>
    private static IReadOnlyList<string> PlatesIn(string directory)
    {
        try
        {
            return Directory.Exists(directory)
                ? [.. Directory.EnumerateFiles(directory, "*.3mf")]
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string? PlateFor(IReadOnlyList<string> plates, string colourName)
    {
        var wanted = colourName.Replace(' ', '-').ToLowerInvariant();

        return plates.FirstOrDefault(file =>
            Path.GetFileNameWithoutExtension(file)
                .StartsWith(wanted, StringComparison.OrdinalIgnoreCase));
    }
}

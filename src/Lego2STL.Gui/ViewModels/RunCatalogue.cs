using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;
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
                    PlateFor(document, plates, part));
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

    /// <summary>
    /// Which plate a part went on: the one the run recorded for its colour code.
    /// </summary>
    /// <remarks>
    /// By code, never by name. A plate is named in the language the run spoke - "bianco.3mf" -
    /// while every colour in the record is kept in the one canonical English, "White", so
    /// matching the two by wording found nothing and disabled every "open plate" button on the
    /// page. The file still has to be there: a plate deleted by hand stops offering to be
    /// opened, which is why the folder is consulted as well as the record.
    /// </remarks>
    private static string? PlateFor(
        RunDocument document, IReadOnlyList<string> plates, RunDocumentPart part)
    {
        foreach (var plate in document.Plates.Where(p => p.ColorCode == part.BrickLinkColorCode))
        {
            var match = plates.FirstOrDefault(file =>
                string.Equals(Path.GetFileName(file), plate.FileName, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        return document.Plates.Count == 0 ? ByName(plates, part.ColorName) : null;
    }

    /// <summary>
    /// The plate for a colour, found by its file name.
    /// </summary>
    /// <remarks>
    /// For runs made before the record listed its plates. Every wording of the colour is tried,
    /// because the file was named in whatever language that run spoke and the record does not
    /// say which. Without this, every run already sitting in someone's folders would stay
    /// broken until it was made again - which, for a run of several hours, is not a fix.
    /// </remarks>
    private static string? ByName(IReadOnlyList<string> plates, string colourName)
    {
        foreach (var language in DisplayLanguages.All)
        {
            var wording = ColorNames.For(language, colourName);

            var match = plates.FirstOrDefault(
                file => PlateFileName.IsPlateOf(Path.GetFileName(file), wording));

            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }
}

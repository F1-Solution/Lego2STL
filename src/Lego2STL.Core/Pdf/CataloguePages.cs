using Lego2STL.Core.Extraction;

namespace Lego2STL.Core.Pdf;

/// <summary>A page that holds a catalogue, and how much of one.</summary>
/// <param name="Number">The page, counting from one.</param>
/// <param name="EntryCount">How many entries it holds, which is what made it a catalogue page.</param>
public sealed record CataloguePage(int Number, int EntryCount);

/// <summary>
/// Where a document's catalogue is, and whether the document was in a position to say.
/// </summary>
/// <param name="Pages">The catalogue pages in order, each with its entry count.</param>
/// <param name="Typeset">
/// Whether the document carries a text layer at all. An empty result from a typeset document
/// means "this book prints no catalogue", which is a real answer; from one without text it
/// means only that nothing was recognised.
/// </param>
public sealed record CataloguePageSearch(IReadOnlyList<CataloguePage> Pages, bool Typeset)
{
    /// <summary>Just the page numbers, for whatever wants a range rather than the counts.</summary>
    public IReadOnlyList<int> Numbers { get; } = [.. Pages.Select(page => page.Number)];
}

/// <summary>
/// Finds the parts catalogue in a document.
/// </summary>
/// <remarks>
/// <para>
/// The document's own text first, and the pixels only when there is no text anywhere. That
/// order is the whole of it, and it is not an optimisation: official instructions come in
/// several books and only one carries the parts list, so a book has to be able to answer
/// "not here" and be believed. Asked to look at the pixels of a book that has none, the
/// label locator finds the "2x" printed beside nearly every building step and reports
/// hundreds of catalogue pages that are not there.
/// </para>
/// <para>
/// Kept here, on its own, because three front ends ask this question - the pipeline, the
/// listing command and the window - and a second copy of the rule is a copy that will
/// disagree with the first.
/// </para>
/// </remarks>
public static class CataloguePages
{
    /// <param name="onPage">
    /// Called with each page as it is examined, so a long search over a document with no text
    /// can say how far it has got. Pages are visited in order.
    /// </param>
    public static CataloguePageSearch Find(
        PdfPageImageSource document,
        Action<int, int>? onPage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var printed = new List<CataloguePage>();
        var typeset = false;

        for (var page = 1; page <= document.PageCount; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            onPage?.Invoke(page, document.PageCount);

            var text = document.ReadText(page);
            typeset |= text.HasText;

            if (text.Entries.Count > 0)
            {
                printed.Add(new CataloguePage(page, text.Entries.Count));
            }
        }

        return typeset || printed.Count > 0
            ? new CataloguePageSearch(printed, typeset)
            : new CataloguePageSearch(Drawn(document, onPage, cancellationToken), false);
    }

    /// <summary>The pages of a document with no text, where the labels are all there is.</summary>
    private static List<CataloguePage> Drawn(
        PdfPageImageSource document,
        Action<int, int>? onPage,
        CancellationToken cancellationToken)
    {
        var locator = new LabelLocator();
        var found = new List<CataloguePage>();

        for (var page = 1; page <= document.PageCount; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            onPage?.Invoke(page, document.PageCount);

            using var image = document.GetPage(page);
            var labels = locator.Locate(image).Count;

            if (labels > 0)
            {
                found.Add(new CataloguePage(page, labels));
            }
        }

        return found;
    }
}

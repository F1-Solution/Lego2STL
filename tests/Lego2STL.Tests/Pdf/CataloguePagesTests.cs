using FluentAssertions;
using Lego2STL.Core.Pdf;
using Xunit;

namespace Lego2STL.Tests.Pdf;

/// <summary>
/// Finding the catalogue in a document, which is the one answer three front ends ask for.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the answer used to be worked out in three places and only two of them
/// were kept up to date. The window went straight to the pixels and so reported 74 catalogue
/// pages in a book that prints two, and 67 in a book that prints none - measured, on the two
/// books below. Both are now one call, and these tests are what keeps them one.
/// </para>
/// <para>
/// The rule the search rests on: a document that carries text has already answered, including
/// when the answer is "not here". Only a document with no text anywhere is worth rasterising,
/// because a building step prints counts too and the pixels cannot tell those from a catalogue.
/// </para>
/// </remarks>
public sealed class CataloguePagesTests
{
    [OfficialInstructionsFact]
    public void A_typeset_book_gives_up_its_catalogue_pages_and_nothing_else()
    {
        using var document = PdfPageImageSource.Open(
            OfficialInstructions.Require(OfficialInstructions.WithCatalogue));

        var found = CataloguePages.Find(document);

        found.Typeset.Should().BeTrue();
        found.Pages.Select(p => p.Number).Should().Equal(
            OfficialInstructions.ExpectedEntriesPerPage.Keys,
            "the other 370 pages print step counts, not a catalogue");
        found.Pages.Select(p => p.EntryCount).Should().Equal(
            OfficialInstructions.ExpectedEntriesPerPage.Values);
    }

    /// <summary>
    /// The companion book, and the answer that matters most: nothing, said with confidence.
    /// </summary>
    /// <remarks>
    /// Falling back to the pixels here is what produced 67 pages that hold no catalogue at
    /// all. A book that has text has answered, and a search that will not take "no" for an
    /// answer is worse than no search.
    /// </remarks>
    [OfficialInstructionsFact]
    public void A_typeset_book_with_no_catalogue_is_believed_rather_than_rasterised()
    {
        using var document = PdfPageImageSource.Open(
            OfficialInstructions.Require(OfficialInstructions.WithoutCatalogue));

        var found = CataloguePages.Find(document);

        found.Typeset.Should().BeTrue("its verdict of 'no catalogue here' is worth believing");
        found.Pages.Should().BeEmpty();
    }

    /// <summary>The other kind of document, where reading the pixels is the only way in.</summary>
    [DocumentFact]
    public void A_document_with_no_text_at_all_is_read_from_its_pixels()
    {
        using var document = PdfPageImageSource.Open(ReferenceDocument.Require());

        var found = CataloguePages.Find(document);

        found.Typeset.Should().BeFalse();
        found.Pages.Select(p => p.Number).Should().Contain(
            ReferenceDocument.ExpectedLabelsPerPage.Keys,
            "the catalogue is drawn rather than typeset, so the labels are all there is to go on");
    }

    [OfficialInstructionsFact]
    public void The_numbers_are_the_pages_the_search_found()
    {
        using var document = PdfPageImageSource.Open(
            OfficialInstructions.Require(OfficialInstructions.WithCatalogue));

        var found = CataloguePages.Find(document);

        found.Numbers.Should().Equal(found.Pages.Select(p => p.Number));
    }
}

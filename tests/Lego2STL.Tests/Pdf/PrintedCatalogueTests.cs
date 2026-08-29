using FluentAssertions;
using Lego2STL.Core.Pdf;
using Xunit;

namespace Lego2STL.Tests.Pdf;

/// <summary>
/// Reading a catalogue out of a document's own text, checked against official instructions.
/// </summary>
/// <remarks>
/// The numbers here were counted off the printed pages rather than produced by running the
/// reader, so this is a yardstick and not a snapshot. What it is really guarding is the two
/// claims the fast path rests on: that a catalogue page can be told from a building step
/// without ambiguity, and that every entry on one is found.
/// </remarks>
public sealed class PrintedCatalogueTests
{
    [OfficialInstructionsFact]
    public void The_catalogue_pages_are_the_only_pages_that_print_entries()
    {
        using var document = PdfPageImageSource.Open(
            OfficialInstructions.Require(OfficialInstructions.WithCatalogue));

        document.PageCount.Should().Be(OfficialInstructions.ExpectedPageCount);

        var found = new Dictionary<int, int>();

        for (var page = 1; page <= document.PageCount; page++)
        {
            var entries = document.ReadPrintedCatalogue(page);
            if (entries.Count > 0)
            {
                found[page] = entries.Count;
            }
        }

        found.Should().Equal(
            OfficialInstructions.ExpectedEntriesPerPage,
            "a building step prints counts too, and none of the 370 that do must be mistaken " +
            "for the catalogue");
    }

    [OfficialInstructionsFact]
    public void Every_entry_on_a_catalogue_page_is_read_with_its_count()
    {
        using var document = PdfPageImageSource.Open(
            OfficialInstructions.Require(OfficialInstructions.WithCatalogue));

        var entries = OfficialInstructions.ExpectedEntriesPerPage.Keys
            .SelectMany(document.ReadPrintedCatalogue)
            .ToList();

        entries.Should().HaveCount(OfficialInstructions.ExpectedEntryTotal);
        entries.Sum(e => e.Quantity).Should().Be(OfficialInstructions.ExpectedPieceTotal);

        entries.Should().OnlyContain(e => e.Quantity > 0);
        entries.Select(e => e.ElementId).Should().OnlyHaveUniqueItems(
            "the catalogue lists an element once");
    }

    /// <summary>
    /// The entries the document prints twice, one impression over the other.
    /// </summary>
    /// <remarks>
    /// Kept as a test of its own because taking the text layer at face value loses exactly
    /// these and nothing else, which makes them silent: the reader would come back with 214
    /// confident entries instead of 223, and nothing about the result would look wrong.
    /// </remarks>
    [OfficialInstructionsFact]
    public void An_entry_printed_twice_over_itself_is_read_once()
    {
        using var document = PdfPageImageSource.Open(
            OfficialInstructions.Require(OfficialInstructions.WithCatalogue));

        var read = OfficialInstructions.ExpectedEntriesPerPage.Keys
            .SelectMany(document.ReadPrintedCatalogue)
            .Select(e => e.ElementId)
            .ToList();

        read.Should().Contain(OfficialInstructions.OverprintedElements);
    }

    /// <summary>
    /// A book with no catalogue says so, and says it in a way that can be told apart from a
    /// document that simply has no text to read.
    /// </summary>
    [OfficialInstructionsFact]
    public void A_book_that_prints_no_catalogue_still_says_it_has_text()
    {
        using var document = PdfPageImageSource.Open(
            OfficialInstructions.Require(OfficialInstructions.WithoutCatalogue));

        var texts = Enumerable.Range(1, document.PageCount).Select(document.ReadText).ToList();

        texts.Should().OnlyContain(t => t.Entries.Count == 0, "this book carries no parts list");
        texts.Count(t => t.HasText).Should().BeGreaterThan(document.PageCount / 2,
            "it is typeset, so its verdict of 'no catalogue here' is worth believing");
    }

    /// <summary>The other kind of document, whose pages carry no text at all.</summary>
    [DocumentFact]
    public void A_document_with_no_text_layer_reports_neither_text_nor_entries()
    {
        using var document = PdfPageImageSource.Open(ReferenceDocument.Require());

        var texts = Enumerable.Range(1, document.PageCount).Select(document.ReadText).ToList();

        texts.Should().OnlyContain(t => !t.HasText && t.Entries.Count == 0);
    }

    [OfficialInstructionsFact]
    public void A_page_outside_the_document_is_refused_rather_than_read_as_empty()
    {
        using var document = PdfPageImageSource.Open(
            OfficialInstructions.Require(OfficialInstructions.WithCatalogue));

        var beyond = () => document.ReadText(document.PageCount + 1);

        beyond.Should().Throw<ArgumentOutOfRangeException>();
    }
}

using FluentAssertions;
using Lego2STL.Core.Extraction;
using Lego2STL.Core.Ocr;
using Lego2STL.Core.Pdf;

namespace Lego2STL.Tests.Ocr;

/// <summary>
/// The gate for the reading stage: every entry on the reference document's catalogue pages
/// must come back exactly as it was transcribed by hand.
/// </summary>
public sealed class LabelReadingAccuracyTests
{
    [DocumentFact]
    public async Task Reads_every_catalogue_entry_of_the_reference_document_correctly()
    {
        var actual = await ReadCatalogueAsync();

        var expected = ExpectedCatalogue.Entries.ToList();

        // Compared as multisets: the same entry can legitimately appear twice, and the
        // ordering between two entries at the same height on a page is not meaningful.
        actual.Should().BeEquivalentTo(expected);
    }

    [DocumentFact]
    public async Task Reads_the_expected_number_of_entries()
    {
        var actual = await ReadCatalogueAsync();

        actual.Should().HaveCount(ExpectedCatalogue.Entries.Count);
    }

    [DocumentTheory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Reads_each_page_correctly(int pageNumber)
    {
        var actual = await ReadCatalogueAsync(pageNumber);

        actual.Should().BeEquivalentTo(ExpectedCatalogue.ForPage(pageNumber));
    }

    /// <summary>
    /// The quantity line is where the recogniser makes its mistakes, returning a letter for
    /// the digit one. Worth asserting the awkward values specifically.
    /// </summary>
    [DocumentFact]
    public async Task Reads_the_quantities_that_the_recogniser_gets_wrong_on_its_own()
    {
        var actual = await ReadCatalogueAsync();

        // "1x" comes back as "Ix" or "lx", and "10x" as "IOX" or "1 ox".
        actual.Should().Contain(new CatalogueEntry(2, 1, "6628", 11));
        actual.Should().Contain(new CatalogueEntry(4, 10, "4265c", 9));
        actual.Should().Contain(new CatalogueEntry(5, 38, "32556", 7));
        actual.Should().Contain(new CatalogueEntry(2, 15, "2780", 11));
    }

    /// <summary>A part number with a letter suffix must keep its suffix, not lose or digitise it.</summary>
    [DocumentFact]
    public async Task Keeps_a_letter_suffix_on_a_part_number()
    {
        var actual = await ReadCatalogueAsync(4);

        actual.Should().ContainSingle(e => e.PartNumber == "4265c");
    }

    [DocumentFact]
    public async Task Every_entry_is_read_completely()
    {
        var result = await ReadRawAsync();

        result.Unresolved.Select(u => $"page {u.Page} at {u.Bounds}: {u.Reason}")
            .Should().BeEmpty();
    }

    /// <summary>
    /// The part lines are what teach the recogniser this document's digits, so they have to
    /// be read by the text engine; the quantities are then filled in from those shapes.
    /// </summary>
    [DocumentFact]
    public async Task Quantities_the_text_engine_declines_are_recovered_from_the_learned_lettering()
    {
        var result = await ReadRawAsync();

        result.Entries.Should().OnlyContain(e => e.PartSource == ReadingSource.TextRecogniser);
        result.Entries.Should().Contain(e => e.QuantitySource == ReadingSource.LearnedShapes,
            "the text engine returns nothing for some short quantity lines");
    }

    private static async Task<List<CatalogueEntry>> ReadCatalogueAsync(int? onlyPage = null)
    {
        var result = await ReadRawAsync(onlyPage);

        return result.Entries
            .Select(e => new CatalogueEntry(e.Page, e.Quantity, e.PartNumber, e.ColorCode))
            .ToList();
    }

    private static async Task<CatalogueReadResult> ReadRawAsync(int? onlyPage = null)
    {
        using var source = PdfPageImageSource.Open(ReferenceDocument.Require());

        IReadOnlyList<int> pages = onlyPage is { } single
            ? [single]
            : PageRange.Parse(ReferenceDocument.PartsListRange);

        return await new CatalogueReader(WindowsOcrEngine.Create()).ReadAsync(source, pages);
    }
}

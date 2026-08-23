using FluentAssertions;
using Lego2STL.Core.Extraction;
using Lego2STL.Core.Pdf;

namespace Lego2STL.Tests.Extraction;

/// <summary>
/// The gate for the location stage: the reference document's catalogue pages hold exactly
/// 53 entries, counted by hand, and every one is two lines of text.
/// </summary>
public sealed class LabelLocatorTests
{
    [DocumentTheory]
    [InlineData(2, 22)]
    [InlineData(3, 5)]
    [InlineData(4, 17)]
    [InlineData(5, 9)]
    public void Finds_exactly_the_labels_that_are_on_the_page(int pageNumber, int expected)
    {
        using var source = PdfPageImageSource.Open(ReferenceDocument.Require());
        using var page = source.GetPage(pageNumber);

        var labels = new LabelLocator().Locate(page);

        labels.Should().HaveCount(expected);
    }

    [DocumentFact]
    public void Finds_the_expected_total_across_the_catalogue()
    {
        using var source = PdfPageImageSource.Open(ReferenceDocument.Require());
        var locator = new LabelLocator();

        var total = 0;
        foreach (var pageNumber in PageRange.Parse(ReferenceDocument.PartsListRange))
        {
            using var page = source.GetPage(pageNumber);
            total += locator.Locate(page).Count;
        }

        total.Should().Be(ReferenceDocument.ExpectedLabelTotal);
    }

    [DocumentFact]
    public void Every_label_is_exactly_two_lines_of_text()
    {
        using var source = PdfPageImageSource.Open(ReferenceDocument.Require());
        var locator = new LabelLocator();

        foreach (var pageNumber in PageRange.Parse(ReferenceDocument.PartsListRange))
        {
            using var page = source.GetPage(pageNumber);
            foreach (var label in locator.Locate(page))
            {
                label.Rows.Should().HaveCount(2, $"page {pageNumber} label at {label.Bounds}");
            }
        }
    }

    /// <summary>
    /// The quantity line is short ("1x", "38x") and the part line is long ("32525, 11"),
    /// which is a cheap structural check that the two lines were not swapped.
    /// </summary>
    [DocumentFact]
    public void The_quantity_line_is_above_and_shorter_than_the_part_line()
    {
        using var source = PdfPageImageSource.Open(ReferenceDocument.Require());
        var locator = new LabelLocator();

        foreach (var pageNumber in PageRange.Parse(ReferenceDocument.PartsListRange))
        {
            using var page = source.GetPage(pageNumber);
            foreach (var label in locator.Locate(page))
            {
                label.QuantityRow.Bounds.Bottom.Should().BeLessThan(label.PartRow.Bounds.Top);
                label.QuantityRow.Glyphs.Count.Should().BeLessThan(label.PartRow.Glyphs.Count);
            }
        }
    }

    /// <summary>
    /// Real text runs 73-96 ink pixels per glyph on these pages, while the speck clusters
    /// inside part renders run 13-28. Everything kept should be comfortably in the first band.
    /// </summary>
    [DocumentFact]
    public void Every_kept_line_is_as_dense_as_real_text()
    {
        using var source = PdfPageImageSource.Open(ReferenceDocument.Require());
        var locator = new LabelLocator();

        foreach (var pageNumber in PageRange.Parse(ReferenceDocument.PartsListRange))
        {
            using var page = source.GetPage(pageNumber);
            foreach (var row in locator.Locate(page).SelectMany(l => l.Rows))
            {
                row.MeanGlyphInk.Should().BeGreaterThan(40);
            }
        }
    }

    /// <summary>
    /// Labels come back top-to-bottom then left-to-right, because the CSV's ids follow the
    /// order a reader would work through the page.
    /// </summary>
    [DocumentFact]
    public void Labels_come_back_in_reading_order()
    {
        using var source = PdfPageImageSource.Open(ReferenceDocument.Require());
        using var page = source.GetPage(2);

        var labels = new LabelLocator().Locate(page);

        labels.Should().BeInAscendingOrder(l => l.Bounds.Top);
    }

    /// <summary>
    /// The dilation radii sit on a plateau rather than a knife edge, which is the difference
    /// between a tuned constant and a lucky one. Every combination in this range finds all
    /// 53 entries.
    /// </summary>
    [DocumentTheory]
    [InlineData(16, 16)]
    [InlineData(16, 20)]
    [InlineData(18, 18)]
    [InlineData(20, 16)]
    [InlineData(20, 20)]
    public void The_dilation_radii_are_not_knife_edged(int dilateY, int dilateX)
    {
        using var source = PdfPageImageSource.Open(ReferenceDocument.Require());
        var locator = new LabelLocator(new LabelLocatorOptions { DilateY = dilateY, DilateX = dilateX });

        var total = 0;
        foreach (var pageNumber in PageRange.Parse(ReferenceDocument.PartsListRange))
        {
            using var page = source.GetPage(pageNumber);
            total += locator.Locate(page).Count;
        }

        total.Should().Be(ReferenceDocument.ExpectedLabelTotal);
    }

    /// <summary>Dilating too far horizontally merges neighbouring entries; documents the upper bound.</summary>
    [DocumentFact]
    public void Dilating_too_far_horizontally_merges_neighbouring_labels()
    {
        using var source = PdfPageImageSource.Open(ReferenceDocument.Require());
        using var page = source.GetPage(2);

        var merged = new LabelLocator(new LabelLocatorOptions { DilateX = 40 }).Locate(page);

        merged.Count.Should().BeLessThan(ReferenceDocument.ExpectedLabelsPerPage[2]);
    }

    [DocumentFact]
    public void A_page_with_no_catalogue_entries_yields_none()
    {
        using var source = PdfPageImageSource.Open(ReferenceDocument.Require());

        // Page 6 is the "Building Instructions" divider: one line of very large text.
        using var divider = source.GetPage(6);

        new LabelLocator().Locate(divider).Should().BeEmpty();
    }
}

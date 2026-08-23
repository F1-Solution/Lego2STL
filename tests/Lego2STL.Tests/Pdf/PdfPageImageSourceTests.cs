using FluentAssertions;
using Lego2STL.Core.Pdf;

namespace Lego2STL.Tests.Pdf;

public sealed class PdfPageImageSourceTests
{
    [Fact]
    public void Opening_a_missing_file_says_so()
    {
        var act = () => PdfPageImageSource.Open(Path.Combine(Path.GetTempPath(), "definitely-not-here.pdf"));

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Opening_something_that_is_not_a_pdf_says_so()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lego2stl-not-a-pdf-{Guid.NewGuid():N}.pdf");
        File.WriteAllText(path, "this is not a PDF");

        try
        {
            var act = () => PdfPageImageSource.Open(path);

            act.Should().Throw<InvalidDataException>().WithMessage("*as a PDF*");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [DocumentFact]
    public void The_reference_document_has_the_expected_page_count()
    {
        using var source = PdfPageImageSource.Open(ReferenceDocument.Require());

        source.PageCount.Should().Be(ReferenceDocument.ExpectedPageCount);
    }

    /// <summary>
    /// The fast path is the one that matters for OCR quality: it yields the exact pixels the
    /// labels were drawn at, and re-rendering above that resolution is only interpolation.
    /// </summary>
    [DocumentTheory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Parts_list_pages_come_from_the_embedded_image_at_its_native_size(int pageNumber)
    {
        using var source = PdfPageImageSource.Open(ReferenceDocument.Require());

        using var page = source.GetPage(pageNumber);

        page.Origin.Should().Be(PageImageOrigin.EmbeddedImage);
        page.Width.Should().Be(1684);
        page.Height.Should().Be(1192);
        page.PageNumber.Should().Be(pageNumber);
    }

    [DocumentFact]
    public void Every_page_of_the_reference_document_yields_pixels()
    {
        using var source = PdfPageImageSource.Open(ReferenceDocument.Require());

        for (var pageNumber = 1; pageNumber <= source.PageCount; pageNumber++)
        {
            using var page = source.GetPage(pageNumber);
            page.Width.Should().BeGreaterThan(0, $"page {pageNumber} must decode");
            page.Height.Should().BeGreaterThan(0, $"page {pageNumber} must decode");
        }
    }

    [DocumentTheory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(127)]
    public void An_out_of_range_page_is_rejected_naming_the_real_count(int pageNumber)
    {
        using var source = PdfPageImageSource.Open(ReferenceDocument.Require());

        var act = () => source.GetPage(pageNumber);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*126 pages*");
    }

    /// <summary>
    /// Sanity check that the pixels are the real page and not a blank: the catalogue pages
    /// are mostly white paper with dark part renders and black label text on them.
    /// </summary>
    [DocumentFact]
    public void A_parts_list_page_contains_both_paper_and_ink()
    {
        using var source = PdfPageImageSource.Open(ReferenceDocument.Require());
        using var page = source.GetPage(2);

        var light = 0;
        var dark = 0;

        for (var y = 0; y < page.Height; y += 4)
        {
            for (var x = 0; x < page.Width; x += 4)
            {
                var pixel = page.Bitmap.GetPixel(x, y);
                if (pixel.Red > 235 && pixel.Green > 235 && pixel.Blue > 235)
                {
                    light++;
                }
                else if (pixel.Red < 90 && pixel.Green < 90 && pixel.Blue < 90)
                {
                    dark++;
                }
            }
        }

        light.Should().BeGreaterThan(dark, "the page is mostly paper");
        dark.Should().BeGreaterThan(500, "but it has renders and label text on it");
    }
}

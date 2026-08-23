using PDFtoImage;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Tokens;

namespace Lego2STL.Core.Pdf;

/// <summary>
/// Gives access to a PDF's pages as bitmaps, preferring each page's embedded image over
/// re-rendering it.
/// </summary>
/// <remarks>
/// <para>
/// Instruction PDFs of this kind are commonly a sequence of full-page JPEGs with no text
/// layer at all: the reference document is 126 pages, every one a single 1684x1192 JPEG,
/// and asking a PDF text extractor for its content returns nothing. Pulling that JPEG out
/// and decoding it is both lossless and much faster than rasterising the page, and it
/// yields exactly the pixels the labels were drawn at.
/// </para>
/// <para>
/// The single-image assumption is checked rather than trusted. A page that turns out to
/// hold vector content, several images, or an unexpected filter falls through to the
/// renderer, so the tool still works on documents that are not built this way.
/// </para>
/// </remarks>
public sealed class PdfPageImageSource : IDisposable
{
    /// <summary>
    /// DPI used when a page has to be rendered. Chosen to match the reference document's
    /// embedded images (1684 px across an 842 pt page is exactly 144 DPI), so both paths
    /// produce comparable pixel sizes.
    /// </summary>
    public const int FallbackRenderDpi = 144;

    private readonly PdfDocument _document;
    private readonly Lazy<byte[]> _fileBytes;

    private PdfPageImageSource(PdfDocument document, string path)
    {
        _document = document;
        Path = path;
        // Only read for the render fallback, which most pages never need.
        _fileBytes = new Lazy<byte[]>(() => File.ReadAllBytes(path));
    }

    public string Path { get; }

    public int PageCount => _document.NumberOfPages;

    public static PdfPageImageSource Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No such PDF: {path}", path);
        }

        try
        {
            return new PdfPageImageSource(PdfDocument.Open(path), path);
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            throw new InvalidDataException($"Could not open '{path}' as a PDF: {ex.Message}", ex);
        }
    }

    /// <summary>Gets one page's pixels. The caller owns the returned object.</summary>
    /// <param name="pageNumber">1-based page number.</param>
    public PageImage GetPage(int pageNumber)
    {
        if (pageNumber < 1 || pageNumber > PageCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber),
                pageNumber,
                $"The document has {PageCount} page{(PageCount == 1 ? "" : "s")}.");
        }

        var embedded = TryDecodeEmbeddedImage(pageNumber);
        return embedded is not null
            ? new PageImage(pageNumber, embedded, PageImageOrigin.EmbeddedImage)
            : new PageImage(pageNumber, Render(pageNumber), PageImageOrigin.Rendered);
    }

    /// <summary>
    /// Decodes the page's single embedded image, or returns null when the page is not
    /// shaped that way.
    /// </summary>
    private SKBitmap? TryDecodeEmbeddedImage(int pageNumber)
    {
        try
        {
            var page = _document.GetPage(pageNumber);
            var images = page.GetImages().ToList();

            // Exactly one image, or we cannot claim it is the whole page.
            if (images.Count != 1)
            {
                return null;
            }

            var image = images[0];
            if (image.IsImageMask || !IsDirectlyDecodable(image))
            {
                return null;
            }

            var bytes = image.RawMemory;
            if (!LooksLikeJpeg(bytes.Span))
            {
                return null;
            }

            var bitmap = SKBitmap.Decode(bytes.Span);
            if (bitmap is null)
            {
                return null;
            }

            // Sanity check: the decoded pixels should be the size the PDF claims.
            if (bitmap.Width != image.WidthInSamples || bitmap.Height != image.HeightInSamples)
            {
                bitmap.Dispose();
                return null;
            }

            return bitmap;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The fast path is an optimisation; anything unexpected falls back to rendering.
            return null;
        }
    }

    /// <summary>
    /// True when the image's bytes are a self-contained JPEG. Any other filter chain
    /// (Flate, CCITT, JPX, or a filter stack) is left to the renderer.
    /// </summary>
    private static bool IsDirectlyDecodable(IPdfImage image)
    {
        if (!image.ImageDictionary.TryGet(NameToken.Filter, out var filter))
        {
            return false;
        }

        return filter switch
        {
            NameToken name => IsJpegFilter(name),
            ArrayToken array => array.Data.Count == 1
                               && array.Data[0] is NameToken only
                               && IsJpegFilter(only),
            _ => false,
        };
    }

    private static bool IsJpegFilter(NameToken name) =>
        name.Data is "DCTDecode" or "DCT";

    private static bool LooksLikeJpeg(ReadOnlySpan<byte> bytes) =>
        bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    private SKBitmap Render(int pageNumber)
    {
        try
        {
            // PDFtoImage takes a 0-based page index.
            return Conversion.ToImage(
                _fileBytes.Value,
                page: new Index(pageNumber - 1),
                password: null,
                options: new RenderOptions(Dpi: FallbackRenderDpi));
        }
        catch (Exception ex)
        {
            throw new InvalidDataException(
                $"Page {pageNumber} of '{Path}' is neither a single embedded JPEG nor renderable: {ex.Message}",
                ex);
        }
    }

    public void Dispose() => _document.Dispose();
}

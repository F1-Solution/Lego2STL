using SkiaSharp;

namespace Lego2STL.Core.Pdf;

/// <summary>How a page's bitmap was obtained.</summary>
public enum PageImageOrigin
{
    /// <summary>
    /// The page's single full-page JPEG, decoded as-is. Lossless with respect to what the
    /// document actually contains, and the resolution the labels were drawn at.
    /// </summary>
    EmbeddedImage,

    /// <summary>
    /// The page rasterised by the PDF renderer, used when the page is not one embedded
    /// image (vector content, several images, overlaid annotations).
    /// </summary>
    Rendered,
}

/// <summary>One page as pixels, with a note of where the pixels came from.</summary>
/// <remarks>
/// The origin matters for OCR quality, which is why it is carried rather than discarded.
/// Measured on the reference document: reading a label from the embedded image at its own
/// resolution gets both text lines right, while the same crop upscaled 4x loses the colour
/// code entirely. Resolution beyond the embedded image's own is interpolation, so the
/// embedded path is preferred wherever it is available.
/// </remarks>
public sealed class PageImage : IDisposable
{
    public PageImage(int pageNumber, SKBitmap bitmap, PageImageOrigin origin)
    {
        PageNumber = pageNumber;
        Bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        Origin = origin;
    }

    /// <summary>1-based page number in the document.</summary>
    public int PageNumber { get; }

    public SKBitmap Bitmap { get; }

    public PageImageOrigin Origin { get; }

    public int Width => Bitmap.Width;

    public int Height => Bitmap.Height;

    public void Dispose() => Bitmap.Dispose();
}

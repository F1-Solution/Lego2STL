using SkiaSharp;

namespace Lego2STL.Core.Ocr;

/// <summary>
/// Reads text from a small image holding a single line.
/// </summary>
/// <remarks>
/// An interface rather than a direct call for two reasons. It keeps the platform-specific
/// recogniser out of the pipeline, so the rest of the extraction is testable without one;
/// and it leaves room for a different engine later, which is the only thing tying this tool
/// to Windows.
/// </remarks>
public interface IOcrEngine
{
    /// <summary>A short name for the engine, used in reports.</summary>
    string Name { get; }

    /// <summary>
    /// Reads the given image. Returns the recognised text, or an empty string when nothing
    /// was recognised - which is a normal outcome, not an error.
    /// </summary>
    Task<string> ReadAsync(SKBitmap image, CancellationToken cancellationToken = default);
}

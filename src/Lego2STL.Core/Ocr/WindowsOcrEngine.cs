using SkiaSharp;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Lego2STL.Core.Ocr;

/// <summary>
/// Text recognition using the engine built into Windows.
/// </summary>
/// <remarks>
/// Chosen over the alternatives because it needs nothing installed, downloads no models and
/// adds no native binaries to the executable - which matters for a single-file build - and
/// because it was measured to be excellent at exactly the job here. Given one cropped line
/// at its native resolution it read the reference document's labels correctly; given a whole
/// page it returned three characters of nonsense. The pipeline is built around that fact.
/// </remarks>
public sealed class WindowsOcrEngine : IOcrEngine
{
    private readonly OcrEngine _engine;

    private WindowsOcrEngine(OcrEngine engine, string languageTag)
    {
        _engine = engine;
        Name = $"Windows OCR ({languageTag})";
    }

    public string Name { get; }

    /// <summary>
    /// Creates an engine, preferring English because the text is digits and Latin letters.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When Windows has no OCR language pack available at all.
    /// </exception>
    public static WindowsOcrEngine Create(string? languageTag = null)
    {
        if (!string.IsNullOrWhiteSpace(languageTag))
        {
            var requested = OcrEngine.TryCreateFromLanguage(new Language(languageTag))
                ?? throw new InvalidOperationException(
                    $"Windows has no OCR language pack for '{languageTag}'. " +
                    $"Available: {DescribeAvailableLanguages()}");

            return new WindowsOcrEngine(requested, requested.RecognizerLanguage.LanguageTag);
        }

        // Any English variant is equivalent for digits and Latin letters.
        foreach (var candidate in OcrEngine.AvailableRecognizerLanguages
                     .Where(l => l.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase)))
        {
            var engine = OcrEngine.TryCreateFromLanguage(candidate);
            if (engine is not null)
            {
                return new WindowsOcrEngine(engine, candidate.LanguageTag);
            }
        }

        var fallback = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? throw new InvalidOperationException(
                "Windows OCR is not available: no recogniser language pack is installed. " +
                "Add one under Settings > Time & language > Language & region.");

        return new WindowsOcrEngine(fallback, fallback.RecognizerLanguage.LanguageTag);
    }

    /// <summary>Language tags Windows can currently recognise, for diagnostics.</summary>
    public static string DescribeAvailableLanguages()
    {
        var tags = OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag).ToList();
        return tags.Count == 0 ? "none" : string.Join(", ", tags);
    }

    public async Task<string> ReadAsync(SKBitmap image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        using var software = await ToSoftwareBitmapAsync(image, cancellationToken).ConfigureAwait(false);
        var result = await _engine.RecognizeAsync(software).AsTask(cancellationToken).ConfigureAwait(false);

        // Keep the engine's own line breaks: they are information, and the caller scans the
        // text for the shapes it expects rather than relying on any particular joining.
        return string.Join('\n', result.Lines.Select(l => l.Text)).Trim();
    }

    /// <summary>
    /// Bridges SkiaSharp to WinRT imaging. Goes via PNG because it is the one encoding both
    /// sides agree on without hand-marshalling pixel buffers and worrying about stride,
    /// premultiplication and channel order.
    /// </summary>
    private static async Task<SoftwareBitmap> ToSoftwareBitmapAsync(
        SKBitmap image,
        CancellationToken cancellationToken)
    {
        var png = RowCrop.ToPng(image);

        using var stream = new InMemoryRandomAccessStream();

        // DataWriter rather than the old AsBuffer() extension, which modern .NET no longer has.
        var writer = new DataWriter(stream);
        writer.WriteBytes(png);
        await writer.StoreAsync().AsTask(cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync().AsTask(cancellationToken).ConfigureAwait(false);
        writer.DetachStream();
        stream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken).ConfigureAwait(false);
        return await decoder.GetSoftwareBitmapAsync().AsTask(cancellationToken).ConfigureAwait(false);
    }
}

using CoreGraphics;
using Foundation;
using ImageIO;
using SkiaSharp;
using Vision;

namespace Lego2STL.Core.Ocr;

/// <summary>
/// Text recognition using Apple's Vision framework.
/// </summary>
/// <remarks>
/// One class, not two: <c>VNRecognizeTextRequest</c> is the identical API on iOS and macOS,
/// so this file compiles into both the <c>net10.0-ios</c> and <c>net10.0-macos</c> targets
/// rather than being written twice.
/// </remarks>
public sealed class AppleOcrEngine : IOcrEngine
{
    private readonly string[] _languages;

    private AppleOcrEngine(string languageTag)
    {
        _languages = [languageTag];
        Name = $"Vision ({languageTag})";
    }

    public string Name { get; }

    public static AppleOcrEngine Create(string? languageTag = null)
        => new(languageTag ?? "en-US");

    public Task<string> ReadAsync(SKBitmap image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        var png = RowCrop.ToPng(image);
        using var data = NSData.FromArray(png);
        using var source = CGImageSource.FromData(data);
        using var cgImage = source.CreateImage(0, null)
            ?? throw new InvalidOperationException("Vision could not decode the cropped row as an image.");

        var completionSource = new TaskCompletionSource<string>();

        var request = new VNRecognizeTextRequest((request, error) =>
        {
            if (error is not null)
            {
                completionSource.TrySetException(new InvalidOperationException(error.LocalizedDescription));
                return;
            }

            var observations = request.GetResults<VNRecognizedTextObservation>()
                ?? Array.Empty<VNRecognizedTextObservation>();

            // Keep the engine's own line breaks, the same choice WindowsOcrEngine and
            // AndroidOcrEngine make: the caller scans the text for the shapes it expects
            // rather than relying on any particular joining. One candidate per line - the
            // text here is short and unambiguous enough that a second guess adds nothing.
            var lines = observations
                .Select(o => o.TopCandidates(1).FirstOrDefault()?.String)
                .Where(text => !string.IsNullOrEmpty(text));

            completionSource.TrySetResult(string.Join('\n', lines));
        })
        {
            RecognitionLevel = VNRequestTextRecognitionLevel.Accurate,
            RecognitionLanguages = _languages,
            // Off, the same reason RowCrop crops one row under a per-row grammar rather than
            // a whole page: the text is digits and part numbers, and a language model
            // correcting toward a real word is exactly the wrong kind of help here.
            UsesLanguageCorrection = false,
        };

        using var handler = new VNImageRequestHandler(cgImage, new NSDictionary());
        handler.Perform([request], out var performError);
        if (performError is not null)
        {
            completionSource.TrySetException(new InvalidOperationException(performError.LocalizedDescription));
        }

        cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken));

        return completionSource.Task;
    }
}

using Android.Gms.Extensions;
using Android.Graphics;
using SkiaSharp;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Text.Latin;

namespace Lego2STL.Core.Ocr;

/// <summary>
/// Text recognition using ML Kit's on-device recogniser.
/// </summary>
/// <remarks>
/// The bundled model, not the Play-Services one: it ships inside the app rather than being
/// downloaded on first use, which is the same reason <see cref="WindowsOcrEngine"/> was
/// chosen over anything that downloads a model - a run should need no network access and no
/// Play Services to read a page.
/// </remarks>
public sealed class AndroidOcrEngine : IOcrEngine
{
    private readonly ITextRecognizer _recognizer;

    private AndroidOcrEngine(ITextRecognizer recognizer, string languageTag)
    {
        _recognizer = recognizer;
        Name = $"ML Kit ({languageTag})";
    }

    public string Name { get; }

    /// <summary>
    /// Creates an engine. ML Kit's Latin recogniser covers the digits and Latin letters the
    /// text here is made of regardless of which language tag is asked for, so the tag is
    /// carried only for <see cref="Name"/> and is not passed to ML Kit itself.
    /// </summary>
    public static AndroidOcrEngine Create(string? languageTag = null)
    {
        var recognizer = TextRecognition.GetClient(TextRecognizerOptions.DefaultOptions);
        return new AndroidOcrEngine(recognizer, languageTag ?? "latin");
    }

    public async Task<string> ReadAsync(SKBitmap image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        using var bitmap = ToAndroidBitmap(image);
        var input = InputImage.FromBitmap(bitmap, 0);

        // Process(...) returns a Java Task, bridged to a .NET one so this method can be
        // awaited like every other IOcrEngine implementation.
        var result = await _recognizer.Process(input)
            .AsAsync<Xamarin.Google.MLKit.Vision.Text.Text>()
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        // Keep the engine's own line breaks, the same choice WindowsOcrEngine makes: the
        // caller scans the text for the shapes it expects rather than relying on any
        // particular joining.
        return string.Join('\n', result.TextBlocks.Select(b => b.Text)).Trim();
    }

    /// <summary>
    /// Bridges SkiaSharp to Android's bitmap type. Goes via PNG, the same route
    /// <see cref="WindowsOcrEngine"/> uses to bridge to WinRT imaging, for the same reason:
    /// it needs no hand-marshalled pixel buffer and no stride, premultiplication or channel
    /// order to get wrong.
    /// </summary>
    private static Bitmap ToAndroidBitmap(SKBitmap image)
    {
        var png = RowCrop.ToPng(image);
        return BitmapFactory.DecodeByteArray(png, 0, png.Length)
            ?? throw new InvalidOperationException("Android could not decode the cropped row as a bitmap.");
    }
}

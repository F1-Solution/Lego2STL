using Lego2STL.Core.Ocr;
using SkiaSharp;

namespace Lego2STL.OcrSmokeTest;

/// <summary>The result of running one engine against the synthetic fixture.</summary>
public sealed record SmokeResult(bool Passed, string ExpectedText, string ActualText);

/// <summary>
/// A label rendered from nothing, not cropped from a real page.
/// </summary>
/// <remarks>
/// This project's job is narrower than <c>LabelReadingAccuracyTests</c>: prove a binding is
/// wired correctly and returns real recognised text, not re-prove OCR accuracy against a
/// genuine catalogue page. A rendered fixture needs no dependency on the undistributed
/// reference PDF and carries no copyright question, which is exactly why it is the right
/// choice for a project that has to be committed.
/// </remarks>
public static class SyntheticFixture
{
    public const string ExpectedText = "5x\n32523, 11";

    /// <summary>
    /// Draws the fixture text in the same shape <see cref="RowCrop"/> produces for the real
    /// pipeline: a white margin around plain black text, at native resolution, never scaled.
    /// </summary>
    public static SKBitmap BuildLabelImage()
    {
        const int width = 160;
        const int height = 90;

        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        using var font = new SKFont(typeface, 24);
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
        };

        canvas.DrawText("5x", 20, 34, SKTextAlign.Left, font, paint);
        canvas.DrawText("32523, 11", 20, 66, SKTextAlign.Left, font, paint);

        return bitmap;
    }

    /// <summary>Runs the given engine against the fixture and reports pass or fail.</summary>
    public static async Task<SmokeResult> RunAsync(IOcrEngine engine, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(engine);

        using var image = BuildLabelImage();
        var actual = await engine.ReadAsync(image, cancellationToken).ConfigureAwait(false);

        // Compared loosely: this project checks that the binding is wired and reads real
        // text, not that punctuation and line breaks match a specific engine's habits.
        var passed = actual.Contains("32523", StringComparison.Ordinal)
            && actual.Contains("11", StringComparison.Ordinal);

        return new SmokeResult(passed, ExpectedText, actual);
    }
}

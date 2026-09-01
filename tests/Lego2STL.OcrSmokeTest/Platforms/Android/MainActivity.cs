using Android.App;
using Android.OS;
using Android.Widget;
using Lego2STL.Core.Ocr;

namespace Lego2STL.OcrSmokeTest.Platforms.Android;

/// <summary>
/// The whole of the Android smoke test: one screen, run by a person before a release,
/// exactly as far as headless CI cannot reach.
/// </summary>
[Activity(Label = "Lego2STL OCR Smoke Test", MainLauncher = true)]
public sealed class MainActivity : Activity
{
    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var text = new TextView(this) { Text = "Running..." };
        SetContentView(text);

        try
        {
            var engine = OcrEngines.Create();
            var result = await SyntheticFixture.RunAsync(engine);

            text.Text = result.Passed
                ? $"PASS ({engine.Name})\n\nRead: {result.ActualText}"
                : $"FAIL ({engine.Name})\n\nExpected: {result.ExpectedText}\nRead: {result.ActualText}";
        }
        catch (Exception ex)
        {
            text.Text = $"FAIL - threw {ex.GetType().Name}: {ex.Message}";
        }
    }
}

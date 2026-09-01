using Foundation;
using Lego2STL.Core.Ocr;
using UIKit;

namespace Lego2STL.OcrSmokeTest.Platforms.iOS;

/// <summary>
/// The whole of the iOS smoke test: one screen, run in the Simulator by a person before a
/// release. Vision works in the Simulator, so nothing here needs a physical device.
/// </summary>
[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    public override bool FinishedLaunching(UIApplication app, NSDictionary options)
    {
        Window = new UIWindow(UIScreen.MainScreen.Bounds);

        var label = new UILabel(UIScreen.MainScreen.Bounds)
        {
            Text = "Running...",
            Lines = 0,
            TextAlignment = UITextAlignment.Center,
        };

        Window.RootViewController = new UIViewController { View = label };
        Window.MakeKeyAndVisible();

        RunAsync(label);

        return true;
    }

    private static async void RunAsync(UILabel label)
    {
        try
        {
            var engine = OcrEngines.Create();
            var result = await SyntheticFixture.RunAsync(engine);

            label.Text = result.Passed
                ? $"PASS ({engine.Name})\n\nRead: {result.ActualText}"
                : $"FAIL ({engine.Name})\n\nExpected: {result.ExpectedText}\nRead: {result.ActualText}";
        }
        catch (Exception ex)
        {
            label.Text = $"FAIL - threw {ex.GetType().Name}: {ex.Message}";
        }
    }
}

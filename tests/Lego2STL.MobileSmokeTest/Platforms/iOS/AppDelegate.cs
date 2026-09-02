using Foundation;
using UIKit;

namespace Lego2STL.MobileSmokeTest.Platforms.iOS;

/// <summary>
/// The whole of the iOS smoke test: one screen, launched by CI on a booted simulator.
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
        var documents = NSFileManager.DefaultManager
            .GetUrls(NSSearchPathDirectory.DocumentDirectory, NSSearchPathDomain.User)[0]
            .Path!;

        var result = await PipelineFixture.RunAsync(documents);
        var line = (result.Passed ? "SMOKE PASS " : "SMOKE FAIL ") + result.Detail;

        // simctl launch --console carries stdout, which is what CI reads.
        Console.WriteLine(line);
        label.Text = line;
    }
}

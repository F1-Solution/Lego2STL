using Android.App;
using Android.OS;
using Android.Widget;

namespace Lego2STL.MobileSmokeTest.Platforms.Android;

/// <summary>
/// The whole of the Android smoke test: one screen, launched by CI on a booted emulator.
/// </summary>
[Activity(Label = "Lego2STL Pipeline Smoke Test", MainLauncher = true)]
public sealed class MainActivity : Activity
{
    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var text = new TextView(this) { Text = "Running..." };
        SetContentView(text);

        var result = await PipelineFixture.RunAsync(FilesDir!.AbsolutePath);
        var line = (result.Passed ? "SMOKE PASS " : "SMOKE FAIL ") + result.Detail;

        // Qualified: this file's own namespace segment "Android" shadows the global one.
        global::Android.Util.Log.Info("Lego2STL", line);
        text.Text = line;
    }
}

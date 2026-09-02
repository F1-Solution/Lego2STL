using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace Lego2STL.Gui.Platforms.Android;

[Activity(
    Label = "Lego2STL",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity;

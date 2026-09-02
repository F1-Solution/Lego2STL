using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Lego2STL.Core.Run;
using Lego2STL.Gui.Services;

namespace Lego2STL.Gui.Platforms.Android;

// Avalonia 12.1.1 builds the AppBuilder from the Application subclass, not the Activity:
// AvaloniaMainActivity is no longer generic over the app type.
[Application]
public sealed class MainApplication(nint handle, JniHandleOwnership ownership)
    : AvaloniaAndroidApplication<App>(handle, ownership)
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        RunHomes.Current = new ApplicationStorageRunHome(UserSettings.StorageRoot);

        return base.CustomizeAppBuilder(builder).WithInterFont();
    }
}

using Avalonia;
using Avalonia.iOS;
using Foundation;
using Lego2STL.Core.Run;
using Lego2STL.Gui.Services;

namespace Lego2STL.Gui.Platforms.iOS;

[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        RunHomes.Current = new ApplicationStorageRunHome(UserSettings.StorageRoot);
        Desktop.Handler = new AppleShareActions();

        return base.CustomizeAppBuilder(builder).WithInterFont();
    }
}

using UIKit;

namespace Lego2STL.Gui.Platforms.iOS;

public static class Application
{
    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}

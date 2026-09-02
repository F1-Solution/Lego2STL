using System;
using System.IO;
using System.Linq;
using Foundation;
using Lego2STL.Gui.Services;
using UIKit;

namespace Lego2STL.Gui.Platforms.iOS;

/// <summary>The share sheet, which is the only way anything leaves a sandboxed application.</summary>
public sealed class AppleShareActions : IDesktopActions
{
    public void Open(string path)
    {
        if (Uri.IsWellFormedUriString(path, UriKind.Absolute) && !File.Exists(path))
        {
            UIApplication.SharedApplication.OpenUrl(new NSUrl(path), new NSDictionary(), null);
            return;
        }

        if (!File.Exists(path))
        {
            return;
        }

        var controller = new UIActivityViewController([NSUrl.FromFilename(path)], null);
        var root = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(scene => scene.Windows)
            .FirstOrDefault(window => window.IsKeyWindow)?
            .RootViewController;

        // An iPad presents this from a point rather than full screen, and throws without one.
        if (controller.PopoverPresentationController is { } popover && root is not null)
        {
            popover.SourceView = root.View;
            popover.SourceRect = new CoreGraphics.CGRect(root.View!.Bounds.GetMidX(), root.View.Bounds.GetMidY(), 0, 0);
            popover.PermittedArrowDirections = 0;
        }

        root?.PresentViewController(controller, animated: true, completionHandler: null);
    }

    public void Reveal(string path) => Open(path);
}

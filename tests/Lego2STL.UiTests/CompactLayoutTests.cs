using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Lego2STL.Gui.ViewModels;
using Lego2STL.Gui.Views;

namespace Lego2STL.UiTests;

/// <summary>
/// What the window does when it is only as wide as a phone.
/// </summary>
/// <remarks>
/// The threshold is a property of the view and not of the platform, so a narrow desktop
/// window behaves exactly as a phone does - which is what makes this testable at all,
/// since no test here runs on a phone. 360 x 780 is a common small Android screen in
/// device-independent pixels; 1040 x 720 is the desktop window's own size.
/// </remarks>
public sealed class CompactLayoutTests
{
    private static Window Showing(MainViewModel model, double width, double height)
    {
        var window = new Window { Width = width, Height = height, Content = new MainView { DataContext = model } };
        window.Show();
        window.CaptureRenderedFrame();
        return window;
    }

    [AvaloniaFact]
    public void A_desktop_width_keeps_the_rail_docked()
    {
        using var model = new MainViewModel();

        var window = Showing(model, 1040, 720);

        model.IsCompact.Should().BeFalse();
    }

    [AvaloniaFact]
    public void A_phone_width_collapses_the_rail()
    {
        using var model = new MainViewModel();

        var window = Showing(model, 360, 780);

        model.IsCompact.Should().BeTrue();
    }

    [AvaloniaFact]
    public void The_view_still_draws_at_a_phone_size()
    {
        using var model = new MainViewModel();

        var window = Showing(model, 360, 780);

        window.CaptureRenderedFrame().Should().NotBeNull();
    }
}

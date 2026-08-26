using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Lego2STL.Gui;
using Lego2STL.Gui.ViewModels;

namespace Lego2STL.UiTests;

/// <summary>
/// The locator has to hand back the same view for the same view model.
/// </summary>
/// <remarks>
/// A fresh view every time would cost the log its scroll position each time the rail moved away
/// and back, and would leave a screen's controls out of the logical tree while it is not the one
/// showing - which is exactly what the option parity test walks.
/// </remarks>
public sealed class ViewLocatorTests
{
    [AvaloniaFact]
    public void The_same_view_model_gets_the_same_view()
    {
        var locator = new ViewLocator();
        var model = new RunOptionsViewModel();

        var first = locator.Build(model);

        first.Should().NotBeNull();
        locator.Build(model).Should().BeSameAs(first);
    }

    [AvaloniaFact]
    public void Two_view_models_get_two_views()
    {
        var locator = new ViewLocator();

        var first = locator.Build(new RunOptionsViewModel());
        var second = locator.Build(new RunOptionsViewModel());

        second.Should().NotBeSameAs(first);
    }

    [AvaloniaFact]
    public void Nothing_at_all_gets_no_view()
    {
        new ViewLocator().Build(null).Should().BeNull();
    }
}

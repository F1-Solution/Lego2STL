using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Lego2STL.Gui.ViewModels;
using Lego2STL.Gui.Views;

namespace Lego2STL.UiTests;

/// <summary>
/// The window's content, drawn on its own rather than inside a window.
/// </summary>
/// <remarks>
/// A phone has no window to host, so everything the window shows has to be a control that
/// stands by itself. Drawing it outside a Window is exactly what the single-view lifetime
/// will do on Android and iOS, so this test is that lifetime rehearsed on the desktop.
/// </remarks>
public sealed class MainViewTests
{
    [AvaloniaFact]
    public void The_view_draws_without_a_window_of_its_own()
    {
        using var model = new MainViewModel();

        var window = new Window { Width = 1040, Height = 720, Content = new MainView { DataContext = model } };

        window.Show();
        window.CaptureRenderedFrame().Should().NotBeNull();
    }

    [AvaloniaFact]
    public void The_window_hosts_the_same_view()
    {
        using var model = new MainViewModel();

        var window = new MainWindow { DataContext = model };

        window.Show();
        window.CaptureRenderedFrame();

        window.Content.Should().BeOfType<MainView>();
    }
}

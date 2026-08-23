using Avalonia;
using Avalonia.Headless;
using Lego2STL.UiTests;

[assembly: AvaloniaTestApplication(typeof(TestApp))]

namespace Lego2STL.UiTests;

/// <summary>
/// Sets up the application for the tests, drawing for real but without a screen.
/// </summary>
/// <remarks>
/// Drawing is switched on rather than stubbed out. A window that is merely constructed proves
/// very little; one that is actually drawn proves the layout resolves, every binding finds
/// what it names, and no template throws. Those are exactly the faults that otherwise wait
/// until the window is opened by a person.
/// </remarks>
public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Lego2STL.Gui.App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

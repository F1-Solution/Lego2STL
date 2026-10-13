using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FluentAssertions;
using Lego2STL.Gui.ViewModels;
using Lego2STL.Gui.Views;

namespace Lego2STL.UiTests;

/// <summary>
/// The setup screen on a small desktop display.
/// </summary>
/// <remarks>
/// Issue #3: at 1280 x 720 with a document chosen, the equivalent command line was docked
/// under the whole window and took so much height that the option list had no room left to
/// show a single row. The command now sits at the end of the options as a button that copies
/// it, and the option list keeps room to be used.
/// </remarks>
public sealed class SetupLayoutTests
{
    private static Window Showing(MainViewModel model)
    {
        var window = new Window { Width = 1280, Height = 720, Content = new MainView { DataContext = model } };
        window.Show();
        window.CaptureRenderedFrame();
        return window;
    }

    [AvaloniaFact]
    public void The_command_line_is_not_docked_under_the_window()
    {
        using var model = new MainViewModel();
        model.Setup.Options.SetDocumentCommand.Execute(null);

        var window = Showing(model);

        window.GetVisualDescendants().OfType<SelectableTextBlock>()
            .Where(t => t.IsEffectivelyVisible)
            .Should().NotContain(t => t.Text == model.Setup.CommandLine);
    }

    [AvaloniaFact]
    public void The_setup_screen_offers_the_command_as_a_copy_button()
    {
        using var model = new MainViewModel();
        model.Setup.Options.SetDocumentCommand.Execute(null);

        var window = Showing(model);

        var button = window.GetVisualDescendants().OfType<SetupView>().Single()
            .GetVisualDescendants().OfType<Button>()
            .Single(b => b.Classes.Contains("copy"));

        button.IsEffectivelyVisible.Should().BeTrue();
        button.Tag.Should().Be(model.Setup.CommandLine);
        ToolTip.GetTip(button).Should().Be(model.Setup.CommandLine);
    }

    /// <summary>At 1280 x 720 in document mode, a few option rows fit, not a sliver.</summary>
    [AvaloniaFact]
    public void The_options_keep_room_at_1280_by_720()
    {
        using var model = new MainViewModel();
        model.Setup.Options.SetDocumentCommand.Execute(null);

        var window = Showing(model);

        var options = window.GetVisualDescendants().OfType<OptionListView>().Single()
            .GetVisualDescendants().OfType<ScrollViewer>().Single(v => v.Content is ItemsControl);

        options.Viewport.Height.Should().BeGreaterThan(150);
    }
}

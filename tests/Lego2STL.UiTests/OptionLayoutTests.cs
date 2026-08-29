using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using FluentAssertions;
using Lego2STL.Gui.ViewModels;
using Lego2STL.Gui.Views;

namespace Lego2STL.UiTests;

/// <summary>
/// Where the option rows put things, measured on a drawn window rather than reasoned about.
/// </summary>
/// <remarks>
/// Both claims here are about position and nothing else, so neither can be checked by reading
/// a view model: the put-back button's opacity is only a stand-in for the thing that actually
/// went wrong, which was every description on the page jumping sideways.
/// </remarks>
public sealed class OptionLayoutTests
{
    private static Window Showing(OptionRowsViewModel rows)
    {
        var window = new Window
        {
            Width = 1000,
            Height = 700,
            Content = new OptionListView { DataContext = rows },
        };

        window.Show();
        window.CaptureRenderedFrame();
        return window;
    }

    /// <summary>
    /// Changing an option leaves every description exactly where it was.
    /// </summary>
    /// <remarks>
    /// The put-back button used to be hidden outright, which collapsed its column and pulled
    /// the whole row across the moment anything was touched.
    /// </remarks>
    [AvaloniaFact]
    public void Changing_an_option_moves_no_description_on_the_page()
    {
        var options = new RunOptionsViewModel();
        var rows = new OptionRowsViewModel(options);
        var window = Showing(rows);

        var before = DescriptionLefts(window);
        before.Should().HaveCountGreaterThan(1, "there are descriptions to be moved");

        var clearance = rows.Rows.Single(row => row.Flag == "--clearance");
        clearance.ResetOpacity.Should().Be(0);

        options.Clearance = 0.15;
        clearance.Refresh();
        window.CaptureRenderedFrame();

        clearance.ResetOpacity.Should().Be(1,
            "the button has to actually appear, or this proves nothing");

        DescriptionLefts(window).Should().Equal(before,
            "the put-back column is there whether or not the button in it is offered");
    }

    /// <summary>
    /// A check box sits where every other control sits: to the right of the name it belongs to.
    /// </summary>
    [AvaloniaFact]
    public void A_check_box_starts_where_the_other_controls_start()
    {
        var rows = new OptionRowsViewModel(new RunOptionsViewModel());
        var window = Showing(rows);

        var box = window.GetVisualDescendants().OfType<CheckBox>()
            .First(c => c.DataContext is ToggleOptionRow);
        var number = window.GetVisualDescendants().OfType<NumericUpDown>()
            .First(n => n.DataContext is NumberOptionRow);

        var boxLeft = box.TranslatePoint(default, window)!.Value.X;
        var numberLeft = number.TranslatePoint(default, window)!.Value.X;

        boxLeft.Should().BeApproximately(numberLeft, 1);

        var label = box.FindLogicalAncestorOfType<Grid>()!
            .GetVisualDescendants().OfType<TextBlock>().First();

        boxLeft.Should().BeGreaterThan(label.TranslatePoint(default, window)!.Value.X,
            "the box belongs to the right of its label, not in front of it");
    }

    /// <summary>Every description on the page starts at the same place, from the top down.</summary>
    private static double[] DescriptionLefts(Window window) =>
    [
        .. window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(text => text.Classes.Contains("help"))
            .Select(text => text.TranslatePoint(default, window)?.X ?? double.NaN),
    ];
}

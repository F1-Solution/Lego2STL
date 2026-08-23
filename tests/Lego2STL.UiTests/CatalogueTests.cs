using System;
using System.IO;
using System.Linq;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;
using Lego2STL.Gui.ViewModels;
using Lego2STL.Gui.Views;

namespace Lego2STL.UiTests;

/// <summary>
/// The catalogue, shown a run that has already happened.
/// </summary>
/// <remarks>
/// The cards are the part of the window with the most going on - a picture or a colour swatch
/// in its place, a quantity, a warning that appears only sometimes - so they are the part most
/// worth drawing and looking at rather than reasoning about.
/// </remarks>
public sealed class CatalogueTests
{
    private static RunOutcome APretendRun()
    {
        var entries = new[]
        {
            new PartEntry(1, "32523", 11, "Black", Rgb24.Parse("#05131D"), 4),
            new PartEntry(2, "3705", 5, "Red", Rgb24.Parse("#C91A09"), 12),
            new PartEntry(3, "4265c", 9, "Light Gray", Rgb24.Parse("#9BA19D"), 8),
            new PartEntry(4, "32250", 85, "Dark Bluish Gray", Rgb24.Parse("#6C6E68"), 2),
            new PartEntry(5, "2780", 7, "Blue", Rgb24.Parse("#0055BF"), 20),
            new PartEntry(6, "32017", 2, "Tan", Rgb24.Parse("#E4CD9E"), 1),
        };

        return new RunOutcome
        {
            Result = RunResult.Complete,
            Settings = new RunSettings { Kind = InputKind.PartsList, InputPath = "parts.csv", Offline = true },
            Layout = RunLayout.For(Path.Combine(Path.GetTempPath(), "lego2stl-catalogue", "parts.csv")),
            PartsList = new PartsList(entries, []),
        };
    }

    [AvaloniaFact]
    public void Every_part_gets_a_card()
    {
        var model = new MainViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();

        model.ShowCatalogue(APretendRun());
        model.Screen = Screen.Catalogue;
        window.CaptureRenderedFrame().Should().NotBeNull();

        model.Parts.Should().HaveCount(6);
        model.VisibleParts.Should().HaveCount(6);
    }

    [AvaloniaFact]
    public void The_colours_present_are_offered_as_a_filter_and_the_filter_narrows_the_list()
    {
        var model = new MainViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();

        model.ShowCatalogue(APretendRun());
        model.Screen = Screen.Catalogue;

        model.Colours.Should().BeEquivalentTo(
            ["Black", "Blue", "Dark Bluish Gray", "Light Gray", "Red", "Tan"]);

        model.ColourFilter = "Red";
        window.CaptureRenderedFrame();

        model.VisibleParts.Should().ContainSingle().Which.PartNumber.Should().Be("3705");
    }

    [AvaloniaFact]
    public void Searching_matches_a_part_number()
    {
        var model = new MainViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();

        model.ShowCatalogue(APretendRun());
        model.Search = "4265";
        window.CaptureRenderedFrame();

        model.VisibleParts.Should().ContainSingle().Which.PartNumber.Should().Be("4265c");
    }

    [AvaloniaFact]
    public void A_picture_of_a_filled_catalogue_is_written()
    {
        var model = new MainViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();

        model.ShowCatalogue(APretendRun());
        model.Screen = Screen.Catalogue;

        var frame = window.CaptureRenderedFrame();
        frame.Should().NotBeNull();

        var directory = Environment.GetEnvironmentVariable("LEGO2STL_UI_SHOTS");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        using var file = File.Create(Path.Combine(directory, "Catalogue-filled.png"));
#pragma warning disable CS0618
        frame!.Save(file);
#pragma warning restore CS0618
    }
}

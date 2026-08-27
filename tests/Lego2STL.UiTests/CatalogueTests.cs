using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
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
/// worth drawing and looking at rather than reasoning about. The run reaches them the way every
/// run does now: through the record it kept and the one projection of it.
/// </remarks>
public sealed class CatalogueTests
{
    private static RunDocumentViewModel APretendRun()
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

        var layout = RunLayout.For(
            Path.Combine(Path.GetTempPath(), "lego2stl-catalogue", "parts.csv"));

        var outcome = new RunOutcome
        {
            Result = RunResult.Complete,
            Settings = new RunSettings { Kind = InputKind.PartsList, InputPath = "parts.csv", Offline = true },
            Layout = layout,
            PartsList = new PartsList(entries, []),
        };

        return RunDocumentViewModel.Of(RunDocument.From(
            RunManifest.From(outcome, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null), layout));
    }

    private static Window Showing(RunDocumentViewModel run)
    {
        var window = new Window
        {
            Width = 1000,
            Height = 700,
            Content = new RunDocumentView { DataContext = run },
        };

        window.Show();
        return window;
    }

    [AvaloniaFact]
    public void Every_part_gets_a_card()
    {
        using var run = APretendRun();
        var window = Showing(run);

        window.CaptureRenderedFrame().Should().NotBeNull();

        run.Parts.Should().HaveCount(6);
        run.VisibleParts.Should().HaveCount(6);
    }

    [AvaloniaFact]
    public void The_colours_present_are_offered_as_a_filter_and_the_filter_narrows_the_list()
    {
        using var run = APretendRun();
        var window = Showing(run);

        run.Colours.Should().BeEquivalentTo(
            ["Black", "Blue", "Dark Bluish Gray", "Light Gray", "Red", "Tan"]);

        run.ColourFilter = "Red";
        window.CaptureRenderedFrame();

        run.VisibleParts.Should().ContainSingle().Which.PartNumber.Should().Be("3705");
    }

    [AvaloniaFact]
    public void Searching_matches_a_part_number()
    {
        using var run = APretendRun();
        var window = Showing(run);

        run.Search = "4265";
        window.CaptureRenderedFrame();

        run.VisibleParts.Should().ContainSingle().Which.PartNumber.Should().Be("4265c");
    }

    [AvaloniaFact]
    public void A_picture_of_a_filled_catalogue_is_written()
    {
        using var run = APretendRun();
        var window = Showing(run);

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

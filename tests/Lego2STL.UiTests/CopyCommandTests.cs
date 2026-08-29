using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using FluentAssertions;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;
using Lego2STL.Gui.ViewModels;
using Lego2STL.Gui.Views;

namespace Lego2STL.UiTests;

/// <summary>
/// Taking a run's command away with you.
/// </summary>
/// <remarks>
/// The command a run stored is the one thing on these screens that is worth having somewhere
/// else, and reading it off the screen by hand is exactly what storing it was meant to avoid.
/// Both places that show one now offer to copy it.
/// </remarks>
public sealed class CopyCommandTests
{
    [AvaloniaFact]
    public async Task The_run_list_copies_the_command_of_the_row_that_was_asked()
    {
        var (rows, expected) = await ARunListAsync();
        var window = Showing(new RunsView { DataContext = rows });

        expected.Should().StartWith("lego2stl build", "the row read a real run off the disk");

        await ClickTheCopyButtonAsync(window);

        (await Clipboard(window).TryGetTextAsync()).Should().Be(expected);
    }

    [AvaloniaFact]
    public async Task A_run_s_own_page_copies_the_command_it_shows()
    {
        using var run = APretendRun();
        var window = Showing(new RunDocumentView { DataContext = run });

        await ClickTheCopyButtonAsync(window);

        var copied = await Clipboard(window).TryGetTextAsync();

        copied.Should().Be(run.CommandLine);
        copied.Should().StartWith("lego2stl build", "it is meant to be run in a terminal as it is");
    }

    /// <summary>A button carrying no command copies nothing, rather than emptying the clipboard.</summary>
    [AvaloniaFact]
    public async Task Nothing_is_copied_when_there_is_no_command()
    {
        var window = Showing(new RunDocumentView { DataContext = APretendRun() });
        await Clipboard(window).SetTextAsync("something already there");

        await Lego2STL.Gui.Services.Clip.CopyAsync(window, null);

        (await Clipboard(window).TryGetTextAsync()).Should().Be("something already there");
    }

    private static IClipboard Clipboard(Window window) =>
        TopLevel.GetTopLevel(window)!.Clipboard!;

    private static async Task ClickTheCopyButtonAsync(Window window)
    {
        var button = window.GetVisualDescendants().OfType<Button>()
            .First(b => b.Classes.Contains("copy"));

        button.Tag.Should().BeOfType<string>("the button carries the command it will copy");

        await Lego2STL.Gui.Services.Clip.CopyAsync(button, (string)button.Tag!);
    }

    private static Window Showing(Control content)
    {
        var window = new Window { Width = 1000, Height = 700, Content = content };
        window.Show();
        window.CaptureRenderedFrame();
        return window;
    }

    private static RunLayout ARunFolder() => RunLayout.For(Path.Combine(
        Path.GetTempPath(), "lego2stl-copy-" + Guid.NewGuid().ToString("N"), "parts.csv"));

    private static RunDocumentViewModel APretendRun()
    {
        var layout = ARunFolder();

        return RunDocumentViewModel.Of(RunDocument.From(
            RunManifest.From(AnOutcome(layout), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null),
            layout));
    }

    private static RunOutcome AnOutcome(RunLayout layout)
    {
        return new RunOutcome
        {
            Result = RunResult.Complete,
            Settings = new RunSettings
            {
                Kind = InputKind.PartsList,
                InputPath = layout.PartsListPath,
                Offline = true,
                ScalePercent = 200,
            },
            Layout = layout,
            PartsList = new PartsList(
                [new PartEntry(1, "32523", 11, "Black", Rgb24.Parse("#05131D"), 4)], []),
        };
    }

    /// <summary>
    /// A list holding one run that really is on the disk.
    /// </summary>
    /// <remarks>
    /// A row reads every word from the folder it names, so the manifest has to have been
    /// written: a row over an empty folder carries no command and would let a broken button
    /// pass by copying nothing and being asked for nothing.
    /// </remarks>
    private static async Task<(RunsViewModel Rows, string Command)> ARunListAsync()
    {
        var layout = ARunFolder();
        layout.CreateDirectories();

        var manifest = RunManifest.From(
            AnOutcome(layout), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);

        await RunManifest.WriteAsync(layout, manifest);

        var model = new RunsViewModel();
        model.Rows.Add(new RunRowViewModel(RunFolder.Read(layout.Root)));

        return (model, manifest.CommandLine);
    }
}

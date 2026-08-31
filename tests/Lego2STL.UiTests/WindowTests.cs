using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using FluentAssertions;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Text;
using Lego2STL.Gui.Localization;
using Lego2STL.Gui.ViewModels;
using Lego2STL.Gui.Views;

namespace Lego2STL.UiTests;

/// <summary>
/// Opens the window for real, off-screen, and checks what it shows.
/// </summary>
/// <remarks>
/// A window is the one part of a program that cannot be checked by reading it. Drawing it
/// here catches the faults that only appear when it is opened - a binding naming something
/// that is not there, a layout that will not resolve, a template that throws - without anyone
/// having to open it.
/// </remarks>
public sealed class WindowTests
{
    private static MainWindow Open(out MainViewModel model)
    {
        model = new MainViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        return window;
    }

    [AvaloniaFact]
    public void The_window_opens_and_draws()
    {
        var window = Open(out _);

        var frame = window.CaptureRenderedFrame();

        frame.Should().NotBeNull();
        frame!.PixelSize.Width.Should().BeGreaterThan(400);
        frame.PixelSize.Height.Should().BeGreaterThan(400);
    }

    [AvaloniaFact]
    public void Every_screen_the_rail_reaches_draws()
    {
        var window = Open(out var model);

        foreach (var screen in Rail(model))
        {
            model.Show(screen);
            window.CaptureRenderedFrame().Should().NotBeNull(
                "the {0} screen has to draw", screen.GetType().Name);
        }
    }

    /// <summary>
    /// A key that names nothing comes back as itself, so any label still showing a key name
    /// is a phrase that was never written.
    /// </summary>
    [AvaloniaFact]
    public void No_label_is_showing_the_name_of_a_missing_phrase()
    {
        var window = Open(out var model);
        var keys = System.Enum.GetNames<TextKey>().ToHashSet();

        foreach (var screen in Rail(model))
        {
            model.Show(screen);
            window.CaptureRenderedFrame();

            foreach (var text in Texts(window))
            {
                keys.Should().NotContain(text, "a label is showing the key '{0}' instead of a phrase", text);
            }
        }
    }

    [AvaloniaFact]
    public void Choosing_a_language_changes_the_window_at_once()
    {
        var window = Open(out var model);

        model.SelectedLanguage = Loc.Choices.First(c => c.Language == DisplayLanguage.English);
        window.CaptureRenderedFrame();
        var english = Texts(window).ToList();

        model.SelectedLanguage = Loc.Choices.First(c => c.Language == DisplayLanguage.Italian);
        window.CaptureRenderedFrame();
        var italian = Texts(window).ToList();

        // Asked for by key rather than by wording, so re-wording a phrase does not fail this:
        // the claim is that the window re-reads itself, not that it says any one thing.
        var inEnglish = Strings.For(DisplayLanguage.English)[TextKey.UiInputKind];
        var inItalian = Strings.For(DisplayLanguage.Italian)[TextKey.UiInputKind];

        english.Should().Contain(inEnglish);
        italian.Should().Contain(inItalian);
        italian.Should().NotContain(inEnglish);
    }

    [AvaloniaFact]
    public void The_language_choice_reaches_the_settings_a_run_would_use()
    {
        var window = Open(out var model);

        model.SelectedLanguage = Loc.Choices.First(c => c.Language == DisplayLanguage.Italian);
        window.CaptureRenderedFrame();

        model.Options.ToSettings().Language.Should().Be(DisplayLanguage.Italian);
    }

    /// <summary>
    /// The three ways in are exclusive, and choosing one has to show its own fields.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(InputKind.Document)]
    [InlineData(InputKind.PartsList)]
    [InlineData(InputKind.SetNumber)]
    public void Each_way_in_can_be_chosen(InputKind kind)
    {
        var window = Open(out var model);

        model.Options.Kind = kind;
        window.CaptureRenderedFrame().Should().NotBeNull();

        model.Options.ToSettings().Kind.Should().Be(kind);
    }

    [AvaloniaFact]
    public void The_shown_command_follows_what_is_set()
    {
        var window = Open(out var model);

        model.Options.Kind = InputKind.PartsList;
        model.Options.PartsListPath = "parts.csv";
        model.Options.Clearance = 0.15;
        model.Options.NoRepair = true;
        window.CaptureRenderedFrame();

        model.Options.CommandLine.Should().Contain("--clearance 0.15").And.Contain("--no-repair");
        Texts(window).Should().Contain(t => t.Contains("--clearance 0.15"));
    }

    [AvaloniaFact]
    public void A_run_with_nothing_chosen_cannot_be_started_and_says_why()
    {
        var window = Open(out var model);
        window.CaptureRenderedFrame();

        model.Setup.CanStart.Should().BeFalse();
        model.Setup.Problem.Should().NotBeNull();
        Texts(window).Should().Contain(t => t == model.Setup.Problem);
    }

    [AvaloniaFact]
    public void The_options_screen_offers_every_printer()
    {
        var window = Open(out var model);
        model.Show(model.Setup);
        model.Setup.Rows.ChangedOnly = false;
        window.CaptureRenderedFrame();

        var boxes = window.GetLogicalDescendants().OfType<ComboBox>().ToList();

        boxes.Should().Contain(b =>
            b.ItemsSource != null && b.ItemsSource.Cast<object>().Any(i => (i as string) == "A1mini"));
    }

    /// <summary>Saves what the window looks like, so it can be looked at rather than imagined.</summary>
    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void A_picture_of_each_screen_is_written(int which)
    {
        var window = Open(out var model);
        var screen = Rail(model)[which];

        model.Show(screen);

        var frame = window.CaptureRenderedFrame();
        frame.Should().NotBeNull();

        var directory = Environment.GetEnvironmentVariable("LEGO2STL_UI_SHOTS");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        using var file = File.Create(Path.Combine(directory, $"{screen.GetType().Name}.png"));

        // The replacement takes an options type with no public implementation in this version.
        // This path only ever runs when someone asks for pictures, so the old call will do.
#pragma warning disable CS0618
        frame!.Save(file);
#pragma warning restore CS0618
    }

    /// <summary>
    /// The window in Italian, which is where a layout breaks if it is going to: the wording is
    /// longer than the English it was laid out against.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void A_picture_of_each_screen_in_italian_is_written(int which)
    {
        var window = Open(out var model);
        var screen = Rail(model)[which];

        model.SelectedLanguage = Loc.Choices.First(c => c.Language == DisplayLanguage.Italian);
        model.Show(screen);

        var frame = window.CaptureRenderedFrame();
        frame.Should().NotBeNull();

        var directory = Environment.GetEnvironmentVariable("LEGO2STL_UI_SHOTS");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        using var file = File.Create(Path.Combine(directory, $"{screen.GetType().Name}-it.png"));
#pragma warning disable CS0618
        frame!.Save(file);
#pragma warning restore CS0618
    }

    /// <summary>
    /// Moving away from a run and back leaves its log where it was left.
    /// </summary>
    /// <remarks>
    /// This is what stops the view locator's cache from being quietly removed later. Building
    /// a fresh view each time would scroll every log back to the top on every move of the
    /// rail, and nothing else in the suite would notice.
    /// </remarks>
    [AvaloniaFact]
    public void Switching_screens_keeps_the_logs_place()
    {
        var window = Open(out var model);
        var run = Rail(model)[2];

        model.Show(run);
        window.CaptureRenderedFrame();

        foreach (var line in Enumerable.Range(1, 200))
        {
            model.OpenRun!.Log.Add("line " + line);
        }

        window.CaptureRenderedFrame();

        var scroll = window.GetLogicalDescendants().OfType<ScrollViewer>()
            .First(viewer => viewer.Name == "LogScroll");

        scroll.Offset = scroll.Offset.WithY(40);
        window.CaptureRenderedFrame();

        var left = scroll.Offset;

        left.Y.Should().BeGreaterThan(0, "otherwise this proves nothing about keeping a place");

        model.Show(model.Setup);
        window.CaptureRenderedFrame();

        model.Show(run);
        window.CaptureRenderedFrame();

        window.GetLogicalDescendants().OfType<ScrollViewer>()
            .First(viewer => viewer.Name == "LogScroll")
            .Offset.Should().Be(left, "the run's page is the same control it was left as");
    }

    /// <summary>
    /// The four the rail reaches, in its own order: the runs, setting one up, the open run,
    /// and what belongs to this machine.
    /// </summary>
    /// <remarks>
    /// A run has to be opened for the third to exist at all, which is itself the claim that
    /// the rail is over the run folder rather than over a fixed set of pages.
    /// </remarks>
    private static IReadOnlyList<ViewModelBase> Rail(MainViewModel model)
    {
        if (model.OpenRun is null)
        {
            model.Show(model.Setup);
            model.Setup.Options.Kind = InputKind.PartsList;
            model.Setup.Options.PartsListPath = APartsList();
            model.Setup.StartCommand.Execute(null);
        }

        return [model.Runs, model.Setup, model.OpenRun!, model.Settings];
    }

    private static string APartsList()
    {
        var folder = Path.Combine(
            Path.GetTempPath(), "lego2stl-window-" + Guid.NewGuid().ToString("N"), "pistola");

        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, "pistola.csv");
        File.WriteAllText(path, "id;part;colour");

        return path;
    }

    /// <summary>Every piece of text the window is currently showing.</summary>
    /// <summary>The window carries its own icon rather than the framework's.</summary>
    [AvaloniaFact]
    public void The_window_has_an_icon_of_its_own()
    {
        var window = new MainWindow();

        window.Icon.Should().NotBeNull();
    }

    private static IEnumerable<string> Texts(Visual root)
    {
        foreach (var control in root.GetLogicalDescendants().OfType<Control>())
        {
            // TextBlock covers the selectable kind, and ContentControl covers every button,
            // check box and radio button, which all carry their label as content.
            var text = control switch
            {
                TextBlock block => block.Text,
                ContentControl { Content: string label } => label,
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return text!;
            }
        }
    }
}

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
    public void All_four_screens_draw()
    {
        var window = Open(out var model);

        foreach (var screen in new[] { Screen.Input, Screen.Options, Screen.Run, Screen.Catalogue })
        {
            model.Screen = screen;
            window.CaptureRenderedFrame().Should().NotBeNull("the {0} screen has to draw", screen);
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

        foreach (var screen in new[] { Screen.Input, Screen.Options, Screen.Run, Screen.Catalogue })
        {
            model.Screen = screen;
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

        english.Should().Contain("Input");
        italian.Should().Contain("Partenza");
        italian.Should().NotContain("Input");
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
        model.Options.FillGaps = true;
        window.CaptureRenderedFrame();

        model.Options.CommandLine.Should().Contain("--clearance 0.15").And.Contain("--repair");
        Texts(window).Should().Contain(t => t.Contains("--clearance 0.15"));
    }

    [AvaloniaFact]
    public void A_run_with_nothing_chosen_cannot_be_started_and_says_why()
    {
        var window = Open(out var model);
        window.CaptureRenderedFrame();

        model.CanStart.Should().BeFalse();
        model.Options.Problem.Should().NotBeNull();
        Texts(window).Should().Contain(t => t == model.Options.Problem);
    }

    /// <summary>
    /// Every option the command line takes has to be reachable here. This is the parity
    /// promise, checked rather than asserted in a comment.
    /// </summary>
    [AvaloniaFact]
    public void Every_option_the_command_line_takes_is_named_on_the_options_screen()
    {
        var window = Open(out var model);
        model.Screen = Screen.Options;
        window.CaptureRenderedFrame();

        var shown = string.Join(" ", Texts(window));

        string[] options =
        [
            "--csv-only", "--no-plates", "--output-dir", "--delimiter", "--ascii",
            "--scale", "--clearance", "--repair", "--keep-origin", "--no-seam-repair",
            "--weld-tolerance", "--ldraw-dir", "--ldraw-cache", "--offline", "--no-unofficial",
            "--printer", "--plate-size", "--plate-spacing", "--lang", "--api-key", "--log",
        ];

        foreach (var option in options)
        {
            shown.Should().Contain(option, "{0} has to be reachable from the window", option);
        }
    }

    [AvaloniaFact]
    public void The_options_screen_offers_every_printer()
    {
        var window = Open(out var model);
        model.Screen = Screen.Options;
        window.CaptureRenderedFrame();

        var boxes = window.GetLogicalDescendants().OfType<ComboBox>().ToList();

        boxes.Should().Contain(b =>
            b.ItemsSource != null && b.ItemsSource.Cast<object>().Any(i => (i as string) == "A1mini"));
    }

    /// <summary>Saves what the window looks like, so it can be looked at rather than imagined.</summary>
    [AvaloniaTheory]
    [InlineData(Screen.Input)]
    [InlineData(Screen.Options)]
    [InlineData(Screen.Run)]
    [InlineData(Screen.Catalogue)]
    public void A_picture_of_each_screen_is_written(Screen screen)
    {
        var window = Open(out var model);
        model.Screen = screen;

        var frame = window.CaptureRenderedFrame();
        frame.Should().NotBeNull();

        var directory = Environment.GetEnvironmentVariable("LEGO2STL_UI_SHOTS");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        using var file = File.Create(Path.Combine(directory, $"{screen}.png"));

        // The replacement takes an options type with no public implementation in this version.
        // This path only ever runs when someone asks for pictures, so the old call will do.
#pragma warning disable CS0618
        frame!.Save(file);
#pragma warning restore CS0618
    }

    /// <summary>Every piece of text the window is currently showing.</summary>
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

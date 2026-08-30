using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using FluentAssertions;
using Lego2STL.Cli;
using Lego2STL.Cli.Commands;
using Lego2STL.Core.Text;
using Lego2STL.Gui.ViewModels;
using Lego2STL.Gui.Views;

namespace Lego2STL.UiTests;

/// <summary>
/// The parity promise, checked against the command line itself.
/// </summary>
/// <remarks>
/// The previous version of this walked a hardcoded list of names, which is how --quiet came to
/// be absent from the window while the test stayed green: an option nobody remembered to add to
/// the array could not be missed. Asking the real declaration what it accepts is the only
/// version of this test that cannot rot.
/// </remarks>
public sealed class OptionParityTests
{
    /// <summary>
    /// Every flag the two pipeline commands register, from the real declaration.
    /// </summary>
    /// <remarks>
    /// --set and --list-pages are left out because they are the input itself, chosen in the
    /// window by radio button rather than typed; --help and --version belong to the parser.
    /// </remarks>
    public static IReadOnlyList<string> EveryFlag()
    {
        var words = Strings.English;

        return
        [
            .. new[] { ExtractCommand.Create(words), BuildCommand.Create(words) }
                .SelectMany(command => command.Options)
                .Select(option => option.Name)
                .Concat([CommonOptions.Language.Name])
                .Where(name => name.StartsWith("--", StringComparison.Ordinal))
                .Where(name => name is not ("--help" or "--version" or "--list-pages" or "--set"))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// A guard on the guard: if the command line ever stops registering these, the test below
    /// would quietly check less, so the count is pinned where a change to it is obvious.
    /// </summary>
    [Fact]
    public void The_command_line_registers_the_options_this_test_expects_to_find()
    {
        EveryFlag().Should().HaveCount(26)
            .And.Contain("--quiet")
            .And.Contain("--element-map")
            .And.Contain("--include-spares")
            .And.Contain("--color-scheme")
            .And.Contain("--lang");
    }

    [AvaloniaFact]
    public void Every_option_the_command_line_takes_is_named_in_the_window()
    {
        var model = new MainViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();

        var shown = string.Join(" ", EverythingTheWindowSays(window, model));

        foreach (var flag in EveryFlag())
        {
            shown.Should().Contain(flag, "{0} has to be reachable from the window", flag);
        }
    }

    /// <summary>
    /// Every screen an option can live on, visited in turn.
    /// </summary>
    /// <remarks>
    /// Setup and Settings both, because the options divide across the two now, and with the
    /// changed-only filter off - a hidden row is still in the tree, but this should not depend
    /// on that subtlety holding to find a flag.
    /// </remarks>
    private static IEnumerable<string> EverythingTheWindowSays(Window window, MainViewModel model)
    {
        var said = new List<string>();

        model.Setup.Rows.ChangedOnly = false;
        model.Setup.Rows.Search = null;

        foreach (var screen in new ViewModelBase[] { model.Setup, model.Settings })
        {
            model.Show(screen);
            window.CaptureRenderedFrame();
            said.AddRange(Texts(window));
        }

        return said;
    }

    /// <summary>Every piece of text a tree is currently showing.</summary>
    internal static IEnumerable<string> Texts(Visual root)
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

using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using FluentAssertions;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;
using Lego2STL.Gui.Localization;
using Lego2STL.Gui.ViewModels;
using Lego2STL.Gui.Views;

namespace Lego2STL.UiTests;

/// <summary>
/// The one screen a run is set up on, and the one place a run can be started from.
/// </summary>
/// <remarks>
/// Start living here and nowhere else is what makes starting a run from the wrong screen
/// impossible rather than merely discouraged. The log tests are the other half: a run's log
/// belongs beside what the run produced, and the window has to arrive at the same path the
/// shown command line would, or the two have already disagreed before anything has run.
/// </remarks>
public sealed class SetupTests
{
    [AvaloniaFact]
    public void A_run_with_nothing_chosen_cannot_start_and_says_why()
    {
        var setup = new SetupViewModel(new RunOptionsViewModel());

        setup.CanStart.Should().BeFalse();
        setup.Problem.Should().NotBeNullOrWhiteSpace();
    }

    [AvaloniaFact]
    public void Choosing_a_parts_list_makes_it_startable()
    {
        var setup = new SetupViewModel(new RunOptionsViewModel());

        setup.Options.Kind = InputKind.PartsList;
        setup.Options.PartsListPath = AFileThatExists("pistola.csv");

        setup.Problem.Should().BeNull();
        setup.CanStart.Should().BeTrue();
    }

    [AvaloniaFact]
    public void The_log_follows_the_input_into_the_folder_the_run_will_use()
    {
        var setup = new SetupViewModel(new RunOptionsViewModel());
        var input = AFileThatExists("pistola.csv");

        setup.Options.Kind = InputKind.PartsList;
        setup.Options.PartsListPath = input;

        var planned = RunLayout.Plan(setup.Options.ToSettings())!;

        setup.PlannedFolder.Should().Be(planned.Root);
        setup.Options.LogFile.Should().Be(planned.LogPath);
        setup.CommandLine.Should().Contain("--log");
    }

    [AvaloniaFact]
    public void Choosing_a_different_input_moves_the_log_with_it()
    {
        var setup = new SetupViewModel(new RunOptionsViewModel());

        setup.Options.Kind = InputKind.PartsList;
        setup.Options.PartsListPath = AFileThatExists("pistola.csv");
        var first = setup.Options.LogFile;

        setup.Options.PartsListPath = AFileThatExists("castello.csv");

        setup.Options.LogFile.Should().NotBe(first);
        setup.Options.LogFile.Should().Be(RunLayout.Plan(setup.Options.ToSettings())!.LogPath);
    }

    /// <summary>
    /// A path someone typed is theirs. Following the input is a convenience for the common
    /// case, and a convenience that overwrites a deliberate choice is not one.
    /// </summary>
    [AvaloniaFact]
    public void A_log_that_was_chosen_by_hand_survives_a_change_of_input()
    {
        var setup = new SetupViewModel(new RunOptionsViewModel());

        setup.Options.Kind = InputKind.PartsList;
        setup.Options.PartsListPath = AFileThatExists("pistola.csv");

        var mine = Path.Combine(Path.GetTempPath(), "somewhere-of-my-own.log");
        setup.Options.LogFile = mine;

        setup.Options.PartsListPath = AFileThatExists("castello.csv");

        setup.Options.LogFile.Should().Be(mine);
    }

    [AvaloniaFact]
    public void A_log_that_was_cleared_stays_cleared()
    {
        var setup = new SetupViewModel(new RunOptionsViewModel());

        setup.Options.Kind = InputKind.PartsList;
        setup.Options.PartsListPath = AFileThatExists("pistola.csv");
        setup.Options.LogFile = null;

        setup.Options.PartsListPath = AFileThatExists("castello.csv");

        setup.Options.LogFile.Should().BeNull("nothing was asked for, so nothing is offered again");
    }

    [AvaloniaFact]
    public void The_count_of_what_was_changed_is_what_the_rail_would_show()
    {
        var setup = new SetupViewModel(new RunOptionsViewModel());

        setup.ChangedCount.Should().Be(0);

        setup.Options.Clearance = 0.15;
        setup.Options.Offline = true;

        setup.ChangedCount.Should().Be(2);
        setup.Rows.Rows.Count(row => row.IsChanged).Should().Be(setup.ChangedCount);
    }

    [AvaloniaFact]
    public void A_run_can_only_be_started_when_it_could_actually_run()
    {
        var setup = new SetupViewModel(new RunOptionsViewModel());
        var started = 0;

        setup.Started += (_, _) => started++;

        setup.StartCommand.Execute(null);
        started.Should().Be(0, "there is nothing to run yet");

        setup.Options.Kind = InputKind.PartsList;
        setup.Options.PartsListPath = AFileThatExists("pistola.csv");

        setup.StartCommand.Execute(null);
        started.Should().Be(1);
    }

    [AvaloniaTheory]
    [InlineData(DisplayLanguage.English)]
    [InlineData(DisplayLanguage.Italian)]
    public void The_screen_draws_in_either_language(DisplayLanguage language)
    {
        Loc.Current.Use(language);

        try
        {
            var window = new Window
            {
                Width = 1000,
                Height = 700,
                Content = new SetupView { DataContext = new SetupViewModel(new RunOptionsViewModel()) },
            };

            window.Show();
            window.CaptureRenderedFrame();

            var keys = Enum.GetNames<TextKey>();

            foreach (var text in window.GetLogicalDescendants().OfType<TextBlock>()
                         .Select(block => block.Text)
                         .Where(text => !string.IsNullOrWhiteSpace(text)))
            {
                keys.Should().NotContain(text, "a label is showing the key '{0}' instead of a phrase", text);
            }
        }
        finally
        {
            Loc.Current.Use(DisplayLanguage.English);
        }
    }

    private static string AFileThatExists(string name)
    {
        var folder = Path.Combine(
            Path.GetTempPath(), "lego2stl-setup-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, name);
        File.WriteAllText(path, "id;part;colour\n");

        return path;
    }
}

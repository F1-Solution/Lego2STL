using FluentAssertions;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;
using Xunit;

namespace Lego2STL.Tests.Run;

/// <summary>
/// Where a run decides to put its folder.
/// </summary>
/// <remarks>
/// "Beside the input" is a decision and not a law of nature: a sandboxed application has no
/// such place, because the document arrives from a picker. These check that the decision is
/// now something a caller can supply, and that supplying nothing keeps the old behaviour
/// exactly - which is what every desktop and command-line run depends on.
/// </remarks>
public sealed class RunHomeTests
{
    [Fact]
    public void Beside_the_input_is_what_a_run_does_when_nobody_says_otherwise()
    {
        var input = Path.Combine(Path.GetTempPath(), "lego2stl-home", "6324712.csv");
        var settings = new RunSettings { InputPath = input };

        var layout = new BesideTheInputRunHome().Plan(settings);

        layout.Should().NotBeNull();
        layout!.Root.Should().Be(RunLayout.Plan(settings)!.Root);
        layout.Name.Should().Be("6324712");
    }

    [Fact]
    public void An_input_too_incomplete_to_name_a_folder_plans_nothing()
    {
        new BesideTheInputRunHome().Plan(new RunSettings()).Should().BeNull();
    }

    [Fact]
    public void A_home_of_its_own_puts_the_run_where_it_says()
    {
        var root = Path.Combine(Path.GetTempPath(), "lego2stl-elsewhere");
        var settings = new RunSettings { InputPath = Path.Combine(Path.GetTempPath(), "in", "6324712.csv") };

        var layout = new StubHome(root).Plan(settings);

        layout!.Root.Should().Be(Path.Combine(root, "6324712"));
    }

    private sealed class StubHome(string root) : IRunHome
    {
        public RunLayout? Plan(RunSettings settings) =>
            settings.InputPath is null ? null : RunLayout.For(settings.InputPath, root);
    }
}

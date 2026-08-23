using FluentAssertions;
using Lego2STL.Core.Pipeline;
using Lego2STL.Gui.ViewModels;

namespace Lego2STL.UiTests;

/// <summary>
/// The three settings the window states the other way round from how they are stored.
/// </summary>
/// <remarks>
/// A control bound through an inversion can look right and do nothing, because the negation
/// applies on the way out and not on the way back. These check that ticking the box really
/// does reach the settings a run would use.
/// </remarks>
public sealed class OptionRoundTripTests
{
    [Fact]
    public void Asking_for_the_parts_list_only_stops_after_it()
    {
        var options = new RunOptionsViewModel { Kind = InputKind.SetNumber, SetNumber = "42100-1" };

        options.CsvOnly = true;

        options.MakeShapes.Should().BeFalse();
        options.ToSettings().Stages.Should().Be(RunStages.PartsListOnly);
        options.CommandLine.Should().Contain("--csv-only");
    }

    [Fact]
    public void Turning_the_plates_off_leaves_the_shapes_on()
    {
        var options = new RunOptionsViewModel { Kind = InputKind.SetNumber, SetNumber = "42100-1" };

        options.NoPlates = true;

        options.ToSettings().Stages.Should().Be(RunStages.Shapes);
        options.CommandLine.Should().Contain("--no-plates");
    }

    [Fact]
    public void Turning_off_the_unofficial_collection_reaches_the_settings()
    {
        var options = new RunOptionsViewModel { Kind = InputKind.SetNumber, SetNumber = "42100-1" };

        options.NoUnofficial = true;

        options.ToSettings().IncludeUnofficial.Should().BeFalse();
        options.CommandLine.Should().Contain("--no-unofficial");
    }

    /// <summary>Unticking has to put it back, or the box only works once.</summary>
    [Fact]
    public void Unticking_puts_each_one_back()
    {
        var options = new RunOptionsViewModel { Kind = InputKind.SetNumber, SetNumber = "42100-1" };

        options.CsvOnly = true;
        options.NoPlates = true;
        options.NoUnofficial = true;

        options.CsvOnly = false;
        options.NoPlates = false;
        options.NoUnofficial = false;

        options.ToSettings().Stages.Should().Be(RunStages.ShapesAndPlates);
        options.ToSettings().IncludeUnofficial.Should().BeTrue();
        options.CommandLine.Should().Be("lego2stl build --set 42100-1");
    }
}

using FluentAssertions;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;

namespace Lego2STL.Tests.Run;

/// <summary>
/// Where a run puts everything, worked out before the run starts.
/// </summary>
/// <remarks>
/// The window shows the folder it is about to write to, and fills --log with a file inside it,
/// before anything has run. That is only honest if the answer it shows is the answer the
/// pipeline reaches, which is why the last test here compares the two rather than trusting
/// that two similar-looking pieces of code agree.
/// </remarks>
public sealed class RunLayoutTests
{
    private static string Temp() =>
        Path.Combine(Path.GetTempPath(), "lego2stl-layout-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void A_folder_is_named_after_itself()
    {
        var layout = RunLayout.At(Path.Combine(Temp(), "42100-1"));

        layout.Name.Should().Be("42100-1");
        layout.PartsListPath.Should().Be(Path.Combine(layout.Root, "42100-1.csv"));
    }

    [Fact]
    public void The_manifest_and_the_log_are_inside_the_run_folder()
    {
        var layout = RunLayout.At(Path.Combine(Temp(), "42100-1"));

        layout.ManifestPath.Should().Be(Path.Combine(layout.Root, "run.json"));
        layout.LogPath.Should().Be(Path.Combine(layout.Root, "run.log"));
    }

    [Theory]
    [InlineData(InputKind.Document)]
    [InlineData(InputKind.PartsList)]
    [InlineData(InputKind.SetNumber)]
    public void Nothing_chosen_yet_plans_nothing(InputKind kind)
    {
        RunLayout.Plan(new RunSettings { Kind = kind }).Should().BeNull();
    }

    [Fact]
    public void A_documents_run_folder_sits_beside_it()
    {
        var folder = Temp();
        var document = Path.Combine(folder, "pistola.pdf");

        var layout = RunLayout.Plan(new RunSettings { Kind = InputKind.Document, InputPath = document });

        layout!.Root.Should().Be(Path.Combine(folder, "pistola"));
    }

    /// <summary>Re-running from a previous run's parts list stays in that run's folder.</summary>
    [Fact]
    public void A_parts_list_from_an_earlier_run_plans_that_same_folder()
    {
        var run = Path.Combine(Temp(), "pistola");
        var partsList = Path.Combine(run, "pistola.csv");

        var layout = RunLayout.Plan(new RunSettings { Kind = InputKind.PartsList, InputPath = partsList });

        layout!.Root.Should().Be(run);
    }

    [Theory]
    [InlineData("42100-1", "set-42100-1")]
    [InlineData("42100", "set-42100-1")]
    [InlineData(" 42100-1 ", "set-42100-1")]
    public void A_set_number_plans_a_folder_named_for_it(string setNumber, string expected)
    {
        var output = Temp();

        var layout = RunLayout.Plan(new RunSettings
        {
            Kind = InputKind.SetNumber,
            SetNumber = setNumber,
            OutputDirectory = output,
        });

        layout!.Root.Should().Be(Path.Combine(Path.GetFullPath(output), expected));
        layout.Name.Should().Be(expected);
    }

    [Fact]
    public void An_output_directory_moves_a_documents_run_folder()
    {
        var output = Temp();

        var layout = RunLayout.Plan(new RunSettings
        {
            Kind = InputKind.Document,
            InputPath = Path.Combine(Temp(), "pistola.pdf"),
            OutputDirectory = output,
        });

        layout!.Root.Should().Be(Path.Combine(Path.GetFullPath(output), "pistola"));
    }

    /// <summary>A half-typed path is the normal state of a window being filled in.</summary>
    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public void A_path_that_cannot_name_a_folder_plans_nothing(string input)
    {
        RunLayout.Plan(new RunSettings { Kind = InputKind.PartsList, InputPath = input })
            .Should().BeNull();
    }

    /// <summary>A drive or a filesystem root names no run, because it names no input.</summary>
    [Fact]
    public void A_root_plans_nothing()
    {
        var root = Path.GetPathRoot(Path.GetTempPath())!;

        RunLayout.Plan(new RunSettings { Kind = InputKind.PartsList, InputPath = root })
            .Should().BeNull();
    }

    /// <summary>
    /// The claim the window's shown folder rests on: planning ahead and working it out during
    /// the run are the same function, so they cannot give different answers.
    /// </summary>
    [Fact]
    public void What_is_planned_is_what_For_produces()
    {
        var document = Path.Combine(Temp(), "pistola.pdf");
        var output = Temp();

        RunLayout.Plan(new RunSettings { Kind = InputKind.Document, InputPath = document })!
            .Root.Should().Be(RunLayout.For(document).Root);

        RunLayout.Plan(new RunSettings
            {
                Kind = InputKind.Document,
                InputPath = document,
                OutputDirectory = output,
            })!
            .Root.Should().Be(RunLayout.For(document, output).Root);
    }
}

using FluentAssertions;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;
using Xunit;

namespace Lego2STL.Tests.Run;

/// <summary>
/// A run's folder on a machine where "beside the input" does not exist.
/// </summary>
/// <remarks>
/// The point of the copy-in step is that a run folder on a phone ends up the same shape as a
/// run folder on a desktop, so the runs list - which reads folders and knows nothing about
/// platforms - keeps working untouched.
/// </remarks>
public sealed class ApplicationStorageRunHomeTests
{
    [Fact]
    public void A_document_run_lands_under_the_storage_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "lego2stl-storage");
        var settings = new RunSettings { InputPath = Path.Combine(root, "imports", "6324712.pdf") };

        var layout = new ApplicationStorageRunHome(root).Plan(settings);

        layout!.Root.Should().Be(Path.Combine(root, "6324712"));
        layout.Name.Should().Be("6324712");
    }

    [Fact]
    public void A_set_run_lands_under_the_storage_root_too()
    {
        var root = Path.Combine(Path.GetTempPath(), "lego2stl-storage");
        var settings = new RunSettings { Kind = InputKind.SetNumber, SetNumber = "42100-1" };

        var layout = new ApplicationStorageRunHome(root).Plan(settings);

        layout!.Root.Should().Be(Path.Combine(root, "set-42100-1"));
    }

    [Fact]
    public void Nothing_to_name_a_folder_from_plans_nothing()
    {
        new ApplicationStorageRunHome(Path.GetTempPath()).Plan(new RunSettings()).Should().BeNull();
    }

    [Fact]
    public void An_explicit_output_directory_still_wins()
    {
        var root = Path.Combine(Path.GetTempPath(), "lego2stl-storage");
        var chosen = Path.Combine(Path.GetTempPath(), "lego2stl-chosen");
        var settings = new RunSettings
        {
            InputPath = Path.Combine(root, "imports", "6324712.pdf"),
            OutputDirectory = chosen,
        };

        new ApplicationStorageRunHome(root).Plan(settings)!.Root
            .Should().Be(Path.Combine(chosen, "6324712"));
    }
}

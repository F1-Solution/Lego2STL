using FluentAssertions;
using Lego2STL.Core.Run;

namespace Lego2STL.Tests.Run;

/// <summary>
/// The runs that have happened.
/// </summary>
/// <remarks>
/// Every test here points the settings folder at a temporary directory of its own, so a suite
/// run never touches the history of whoever is running it. The collection these share with
/// <see cref="AppDataDirectoryTests"/> keeps them off each other's variable: it is one
/// process-wide setting, and two classes changing it at once would fail for no reason.
/// </remarks>
[Collection(AppDataFolder.Name)]
public sealed class RunIndexTests
{
    [Fact]
    public void The_newest_run_is_first()
    {
        using var settings = new AppDataFolder();

        RunIndex.Record(ARunFolder("pistola"));
        RunIndex.Record(ARunFolder("castello"));

        RunIndex.Read().Should().HaveCount(2);
        RunIndex.Read()[0].Should().EndWith("castello");
        RunIndex.Read()[1].Should().EndWith("pistola");
    }

    [Fact]
    public void A_run_recorded_twice_is_one_row_moved_to_the_front()
    {
        using var settings = new AppDataFolder();

        var pistola = ARunFolder("pistola");
        RunIndex.Record(pistola);
        RunIndex.Record(ARunFolder("castello"));
        RunIndex.Record(pistola);

        RunIndex.Read().Should().HaveCount(2, "a folder re-run is the same folder");
        RunIndex.Read()[0].Should().EndWith("pistola");
    }

    [Fact]
    public void The_same_folder_spelt_differently_is_still_the_same_folder()
    {
        using var settings = new AppDataFolder();

        var layout = ARunFolder("pistola");
        RunIndex.Record(layout);
        RunIndex.Record(RunLayout.At(layout.Root.ToUpperInvariant()));

        RunIndex.Read().Should().HaveCount(1);
    }

    [Fact]
    public void Forgetting_one_leaves_the_rest_in_order()
    {
        using var settings = new AppDataFolder();

        var pistola = ARunFolder("pistola");
        var castello = ARunFolder("castello");
        var nave = ARunFolder("nave");

        RunIndex.Record(pistola);
        RunIndex.Record(castello);
        RunIndex.Record(nave);
        RunIndex.Forget(castello.Root);

        RunIndex.Read().Should().HaveCount(2);
        RunIndex.Read()[0].Should().EndWith("nave");
        RunIndex.Read()[1].Should().EndWith("pistola");
    }

    [Fact]
    public void Forgetting_a_run_that_was_never_recorded_changes_nothing()
    {
        using var settings = new AppDataFolder();

        RunIndex.Record(ARunFolder("pistola"));
        RunIndex.Forget(ARunFolder("nave").Root);

        RunIndex.Read().Should().HaveCount(1);
    }

    [Fact]
    public void Forgetting_everything_empties_it()
    {
        using var settings = new AppDataFolder();

        RunIndex.Record(ARunFolder("pistola"));
        RunIndex.Record(ARunFolder("castello"));
        RunIndex.ForgetEverything();

        RunIndex.Read().Should().BeEmpty();
    }

    [Fact]
    public void No_history_yet_reads_as_no_runs()
    {
        using var settings = new AppDataFolder();

        RunIndex.Read().Should().BeEmpty();
    }

    [Fact]
    public void A_history_that_will_not_parse_reads_as_no_runs()
    {
        using var settings = new AppDataFolder();

        Directory.CreateDirectory(AppDataDirectory.Path);
        File.WriteAllText(RunIndex.FilePath, "{ this is not json");

        RunIndex.Read().Should().BeEmpty();

        RunIndex.Record(ARunFolder("pistola"));

        RunIndex.Read().Should().ContainSingle("a broken history is replaced, not preserved");
    }

    [Fact]
    public void The_history_lands_under_the_folder_the_variable_names()
    {
        using var settings = new AppDataFolder();

        RunIndex.Record(ARunFolder("pistola"));

        RunIndex.FilePath.Should().Be(Path.Combine(settings.Path, "runs.json"));
        File.Exists(RunIndex.FilePath).Should().BeTrue();
    }

    [Fact]
    public void Recording_twice_in_a_row_survives_the_file_moving_underneath_it()
    {
        using var settings = new AppDataFolder();

        // What two copies running at once look like from inside one of them: the file it just
        // wrote is gone by the time it writes again. Losing a history row must not cost a run.
        RunIndex.Record(ARunFolder("pistola"));
        File.Delete(RunIndex.FilePath);

        var recordAgain = () => RunIndex.Record(ARunFolder("castello"));

        recordAgain.Should().NotThrow();
        RunIndex.Read().Should().ContainSingle();
    }

    private static RunLayout ARunFolder(string name) =>
        RunLayout.At(APretendRun.TempFolder(name));
}

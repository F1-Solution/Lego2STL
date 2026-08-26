using FluentAssertions;
using Lego2STL.Core.Run;

namespace Lego2STL.Tests.Run;

/// <summary>
/// Where the few things kept between one use and the next live.
/// </summary>
/// <remarks>
/// One definition rather than two: the history of runs and the window's preferences are both
/// files in the same place, and two copies of the rule for finding it would eventually
/// disagree - leaving a copy carried on a stick writing its history into the account it
/// happened to be run under.
/// </remarks>
public sealed class AppDataDirectoryTests
{
    [Fact]
    public void The_variable_decides_when_it_is_set()
    {
        var chosen = Path.Combine(Path.GetTempPath(), "lego2stl-appdata-" + Guid.NewGuid().ToString("N"));
        var previous = Environment.GetEnvironmentVariable(AppDataDirectory.Variable);

        try
        {
            Environment.SetEnvironmentVariable(AppDataDirectory.Variable, chosen);

            AppDataDirectory.Path.Should().Be(chosen);
            AppDataDirectory.File("runs.json").Should().Be(Path.Combine(chosen, "runs.json"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppDataDirectory.Variable, previous);
        }
    }

    [Fact]
    public void Without_it_the_account_decides()
    {
        var previous = Environment.GetEnvironmentVariable(AppDataDirectory.Variable);

        try
        {
            Environment.SetEnvironmentVariable(AppDataDirectory.Variable, null);

            AppDataDirectory.Path.Should().Be(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lego2STL"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppDataDirectory.Variable, previous);
        }
    }

    [Fact]
    public void An_empty_variable_counts_as_unset()
    {
        var previous = Environment.GetEnvironmentVariable(AppDataDirectory.Variable);

        try
        {
            Environment.SetEnvironmentVariable(AppDataDirectory.Variable, string.Empty);

            AppDataDirectory.Path.Should().NotBeEmpty().And.EndWith("Lego2STL");
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppDataDirectory.Variable, previous);
        }
    }
}

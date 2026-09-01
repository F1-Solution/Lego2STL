using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Lego2STL.Cli.Commands;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;

namespace Lego2STL.UiTests;

/// <summary>
/// The half of the calibration command that records a measurement instead of building one.
/// </summary>
/// <remarks>
/// Two modes in one command is a real cost, taken deliberately: the roadmap asked for
/// calibration --save and the alternative was a second command for four flags. The tests here
/// exist mostly to hold the line that the management flags never build a plate, which is the way
/// two modes in one command goes wrong.
/// </remarks>
public sealed class CalibrationManagementTests : IDisposable
{
    private readonly string _output = Path.Combine(
        Path.GetTempPath(), "lego2stl-calmgmt-" + Guid.NewGuid().ToString("N"));

    /// <summary>This assembly shares one settings folder, so the store is emptied, not moved.</summary>
    public CalibrationManagementTests() => Clear();

    public void Dispose()
    {
        Clear();

        if (Directory.Exists(_output))
        {
            Directory.Delete(_output, recursive: true);
        }
    }

    private static void Clear()
    {
        if (File.Exists(TolerancePresets.FilePath))
        {
            File.Delete(TolerancePresets.FilePath);
        }
    }

    /// <summary>
    /// The command as the parser really assembles it.
    /// </summary>
    /// <remarks>
    /// Wrapped in a root command so the arguments read the way a person types them, and so the
    /// test drives the declaration the user drives rather than a copy of it.
    /// </remarks>
    internal static async Task<int> RunAsync(params string[] args)
    {
        var words = Strings.English;
        var root = new RootCommand("test") { CalibrationCommand.Create(words) };

        return await root.Parse(args).InvokeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Saving_a_measurement_records_it_and_builds_nothing()
    {
        var exit = await RunAsync("calibration", "--save", "0.15", "--name", "black",
            "--output-dir", _output);

        exit.Should().Be(0);
        TolerancePresets.Load().Should().ContainSingle(p => p.Name == "black" && p.Millimetres == 0.15);
        Directory.Exists(_output).Should().BeFalse("nothing was built, so nothing was written");
    }

    [Fact]
    public async Task Saving_can_mark_it_preferred_at_the_same_time()
    {
        await RunAsync("calibration", "--save", "0.15", "--name", "black", "--preferred");

        TolerancePresets.Load().Should().ContainSingle(p => p.Preferred);
    }

    /// <summary>A figure with no name is refused: an unnamed preset cannot be asked for again.</summary>
    [Fact]
    public async Task Saving_without_a_name_is_refused()
    {
        var exit = await RunAsync("calibration", "--save", "0.15");

        exit.Should().NotBe(0);
        TolerancePresets.Load().Should().BeEmpty();
    }

    [Fact]
    public async Task Preferring_and_forgetting_do_what_they_say()
    {
        await RunAsync("calibration", "--save", "0.10", "--name", "first");
        await RunAsync("calibration", "--save", "0.20", "--name", "second");

        await RunAsync("calibration", "--prefer", "second");
        TolerancePresets.Load().Should().ContainSingle(p => p.Preferred && p.Name == "second");

        await RunAsync("calibration", "--forget", "first");
        TolerancePresets.Load().Should().ContainSingle(p => p.Name == "second");
    }

    [Fact]
    public async Task Listing_builds_nothing()
    {
        await RunAsync("calibration", "--save", "0.15", "--name", "black");

        var exit = await RunAsync("calibration", "--list", "--output-dir", _output);

        exit.Should().Be(0);
        Directory.Exists(_output).Should().BeFalse();
    }
}

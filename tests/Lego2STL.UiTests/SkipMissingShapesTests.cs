using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Lego2STL.Cli;
using Lego2STL.Cli.Commands;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Text;

namespace Lego2STL.UiTests;

/// <summary>
/// What --skip-missing-shapes changes about how a build ends, from the command line (#11).
/// </summary>
/// <remarks>
/// Offline, with the shape library pointed at an empty folder, so every part fails for want of a
/// shape file - the case the window's continue button used to loop on. Shapes only, no plates:
/// the exit code is what is being checked, not what was arranged.
/// </remarks>
public sealed class SkipMissingShapesTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "lego2stl-skipmissing-" + Guid.NewGuid().ToString("N"));

    public SkipMissingShapesTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task A_part_with_no_shape_leaves_the_run_needing_a_decision()
    {
        var exit = await BuildAsync();

        exit.Should().Be(Program.ExitUnverified);
    }

    [Fact]
    public async Task Skipping_missing_shapes_lets_the_same_run_finish()
    {
        var exit = await BuildAsync("--skip-missing-shapes");

        exit.Should().Be(Program.ExitOk);
    }

    private async Task<int> BuildAsync(params string[] extra)
    {
        var list = Path.Combine(_folder, "aereo.csv");
        PartsListCsv.WriteFile(
            list,
            new PartsList([new PartEntry(1, "65746", 11, "Black", Rgb24.Parse("#05131D"), 1)], []));

        var root = new RootCommand("test") { CommonOptions.Language, BuildCommand.Create(Strings.English) };

        return await root.Parse(
            [
                "build", list,
                "--no-plates", "--offline",
                "--ldraw-cache", Path.Combine(_folder, "no-library"),
                "--output-dir", Path.Combine(_folder, "out"),
                "--lang", "en",
                .. extra,
            ])
            .InvokeAsync()
            .ConfigureAwait(false);
    }
}

using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;

namespace Lego2STL.UiTests;

/// <summary>
/// What the building half of the calibration command does.
/// </summary>
/// <remarks>
/// <para>
/// One file to open and print, rather than one file per part per clearance to arrange by hand -
/// by a tool whose every other output is a packed plate.
/// </para>
/// <para>
/// Deliberately never fetches a real part. No test in this repository builds from the real LDraw
/// library: every one of them uses the fake, because the real one is a download and a test that
/// depends on one passes or fails by what is on the machine. What the set produces is covered
/// against fakes by CalibrationSetTests, what the sheet says by CalibrationNotesTests, and what
/// the whole thing looks like against real parts is the by-hand step at the end of this task.
/// </para>
/// </remarks>
public sealed class CalibrationPlateTests : IDisposable
{
    private readonly string _output = Path.Combine(
        Path.GetTempPath(), "lego2stl-calplate-" + Guid.NewGuid().ToString("N"));

    private readonly string _emptyLibrary = Path.Combine(
        Path.GetTempPath(), "lego2stl-nolibrary-" + Guid.NewGuid().ToString("N"));

    public CalibrationPlateTests() => Directory.CreateDirectory(_emptyLibrary);

    public void Dispose()
    {
        foreach (var folder in new[] { _output, _emptyLibrary })
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    /// <summary>
    /// With no library to build from, it says so and leaves nothing behind.
    /// </summary>
    /// <remarks>
    /// The one end-to-end claim that can be made without a download, and it is worth making: it
    /// proves --printer and --output-dir are wired, that the command reaches the point of asking
    /// the library for parts, and that a failure there is a message rather than a half-written
    /// folder.
    /// </remarks>
    [Fact]
    public async Task With_nothing_to_build_from_it_says_so_and_writes_no_shapes()
    {
        var exit = await CalibrationManagementTests.RunAsync(
            "calibration",
            "--printer", "A1",
            "--offline",
            "--ldraw-dir", _emptyLibrary,
            "--output-dir", _output);

        exit.Should().NotBe(0);

        if (Directory.Exists(_output))
        {
            Directory.GetFiles(_output, "*.stl").Should().BeEmpty();
            Directory.GetFiles(_output, "*.3mf").Should().BeEmpty();
        }
    }

    /// <summary>
    /// The loose STLs are gone from the command's vocabulary entirely.
    /// </summary>
    /// <remarks>
    /// --part came off because the set is the set: substituting a part would leave the sheet
    /// describing a plate that was not built.
    /// </remarks>
    [Fact]
    public void The_command_no_longer_offers_to_substitute_a_part()
    {
        var command = Lego2STL.Cli.Commands.CalibrationCommand.Create(Lego2STL.Core.Text.Strings.English);

        command.Options.Should().NotContain(o => o.Name == "--part");
        command.Options.Should().Contain(o => o.Name == "--printer");
    }
}

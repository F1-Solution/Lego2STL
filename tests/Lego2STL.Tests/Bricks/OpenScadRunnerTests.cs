using FluentAssertions;
using Lego2STL.Core.Bricks;

namespace Lego2STL.Tests.Bricks;

/// <summary>
/// The part of brick generation that can be checked without OpenSCAD: finding the pieces,
/// saying clearly when they are not there, and writing a description that says what was asked
/// for. Whether OpenSCAD then produces the shape is OpenSCAD's business.
/// </summary>
public sealed class OpenScadRunnerTests : IDisposable
{
    private readonly string _temporary =
        Path.Combine(Path.GetTempPath(), "lego2stl-bricks-" + Guid.NewGuid().ToString("N"));

    /// <summary>A folder shaped like the library, with an empty file where the real one goes.</summary>
    private string ALibrary(string relative = "lib/block.scad")
    {
        var path = Path.Combine(_temporary, "machineblocks", relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "// stand-in for the real one\n");
        return Path.Combine(_temporary, "machineblocks");
    }

    /// <summary>A file standing in for OpenSCAD, so the search can be exercised.</summary>
    private string APretendOpenScad()
    {
        Directory.CreateDirectory(_temporary);
        var path = Path.Combine(_temporary, "openscad-stand-in");
        File.WriteAllText(path, string.Empty);
        return path;
    }

    [Fact]
    public void The_library_is_found_by_the_file_that_matters()
    {
        OpenScadRunner.FindLibrary(ALibrary()).Should().NotBeNull();
    }

    [Fact]
    public void The_library_is_found_whether_or_not_it_has_a_lib_folder()
    {
        OpenScadRunner.FindLibrary(ALibrary("block.scad")).Should().NotBeNull();
    }

    /// <summary>
    /// A folder called machineblocks that does not hold the library is not the library. Saying
    /// so now beats a puzzling error out of OpenSCAD later.
    /// </summary>
    [Fact]
    public void A_folder_without_the_library_in_it_is_not_the_library()
    {
        var empty = Path.Combine(_temporary, "not-really");
        Directory.CreateDirectory(empty);

        OpenScadRunner.FindLibrary(empty).Should().BeNull();
    }

    [Fact]
    public void A_missing_folder_is_not_the_library()
    {
        OpenScadRunner.FindLibrary(Path.Combine(_temporary, "nowhere")).Should().BeNull();
    }

    [Fact]
    public void A_named_openscad_that_is_not_there_is_not_found()
    {
        OpenScadRunner.FindOpenScad(Path.Combine(_temporary, "nowhere.exe")).Should().BeNull();
    }

    [Fact]
    public void Not_having_openscad_says_where_to_get_it()
    {
        var act = () => OpenScadRunner.Create(
            Path.Combine(_temporary, "nowhere.exe"), ALibrary());

        act.Should().Throw<OpenScadUnavailableException>()
            .WithMessage("*openscad.org*");
    }

    /// <summary>
    /// The library is not included, and the message has to say why rather than merely that it
    /// is absent: someone looking for it needs to know it is theirs to fetch.
    /// </summary>
    [Fact]
    public void Not_having_the_library_says_why_it_is_not_included()
    {
        var act = () => OpenScadRunner.Create(
            APretendOpenScad(), Path.Combine(_temporary, "nowhere"));

        act.Should().Throw<OpenScadUnavailableException>()
            .Which.Message.Should().Contain("non-commercial").And.Contain("machineblocks");
    }

    private OpenScadRunner AReadyRunner() => OpenScadRunner.Create(APretendOpenScad(), ALibrary());

    [Fact]
    public void The_description_asks_for_the_piece_that_was_specified()
    {
        var scad = AReadyRunner().Describe(BrickSpec.Parse("2x4x6"));

        // The names are the library's own: one size vector with the height in plates as its
        // third number, studs for the knobs on top and pillars for the tubes underneath.
        scad.Should().Contain("machineblock(");
        scad.Should().Contain("size = [2, 4, 6]");
        scad.Should().Contain("studs = true");
        scad.Should().Contain("pillars = true");
    }

    [Fact]
    public void The_description_points_at_the_library_it_was_given()
    {
        var library = ALibrary();

        var scad = OpenScadRunner.Create(APretendOpenScad(), library).Describe(BrickSpec.Parse("2x4"));

        // Forward slashes, which OpenSCAD accepts on every system, including Windows.
        scad.Should().Contain("use <").And.Contain("block.scad>");
        scad.Should().NotContain("\\");
    }

    [Fact]
    public void A_tile_asks_for_no_knobs()
    {
        AReadyRunner().Describe(BrickSpec.Parse("2x4", BrickKind.Tile))
            .Should().Contain("studs = false");
    }

    [Fact]
    public void A_solid_piece_asks_for_no_stud_holes()
    {
        AReadyRunner().Describe(BrickSpec.Parse("2x4", studHoles: false))
            .Should().Contain("pillars = false");
    }

    /// <summary>
    /// Two of the library's defaults are file paths written relative to its own examples
    /// folder. A description written anywhere else has to name them, or OpenSCAD looks for a
    /// pattern beside the output and fails on a piece that asked for no pattern at all.
    /// </summary>
    [Fact]
    public void The_description_leaves_no_default_pointing_at_a_file_that_is_not_there()
    {
        var scad = AReadyRunner().Describe(BrickSpec.Parse("2x4"));

        scad.Should().Contain("studIcon = \"none\"");
        scad.Should().Contain("surfacePattern = \"none\"");
        scad.Should().NotContain(".svg");
    }

    /// <summary>
    /// The licence travels with what it covers. Someone who finds one of these files later
    /// should not have to work out where the shape came from.
    /// </summary>
    [Fact]
    public void The_description_carries_the_licence_the_shape_comes_under()
    {
        var scad = AReadyRunner().Describe(BrickSpec.Parse("2x4"));

        scad.Should().Contain("MachineBlocks");
        scad.Should().Contain("CC BY-NC-SA");
    }

    [Fact]
    public async Task Asking_for_the_description_alone_writes_it_and_stops()
    {
        var into = Path.Combine(_temporary, "out");

        var result = await AReadyRunner()
            .GenerateAsync(BrickSpec.Parse("2x4"), into, describeOnly: true);

        result.ScadPath.Should().EndWith("brick-2x4x3.scad");
        File.Exists(result.ScadPath).Should().BeTrue();
        result.ShapePath.Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporary))
        {
            Directory.Delete(_temporary, recursive: true);
        }
    }
}

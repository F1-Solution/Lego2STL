using FluentAssertions;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.LDraw;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;
using Lego2STL.Tests.LDraw;

namespace Lego2STL.Tests;

/// <summary>
/// The whole tool's output, compared against copies kept in the repository.
/// </summary>
/// <remarks>
/// Built from a small library defined here rather than from the real one, so these run
/// anywhere, need no network, and cannot change because a part was revised upstream. What they
/// watch is this tool's own behaviour: a change in how a shape is welded, how a plate is laid
/// out, or how a report is worded shows up here immediately.
/// </remarks>
public sealed class SnapshotTests
{
    /// <summary>
    /// A library of three small parts, written out in full. Small enough to check by hand,
    /// and between them they exercise the things that matter: a closed shape, one with a gap
    /// in its surface, and one that reaches its geometry through a reference.
    /// </summary>
    private static FakeLDrawLibrary ALibrary()
    {
        // A closed square-based pyramid: five faces, no gaps.
        const string pyramid = """
            0 Pyramid
            0 BFC CERTIFY CCW
            3 16 -10 0 -10  10 0 -10  0 -20 0
            3 16 10 0 -10  10 0 10  0 -20 0
            3 16 10 0 10  -10 0 10  0 -20 0
            3 16 -10 0 10  -10 0 -10  0 -20 0
            4 16 -10 0 -10  -10 0 10  10 0 10  10 0 -10
            """;

        // The same shape with its base left off, which is how real parts arrive.
        const string openPyramid = """
            0 Open pyramid
            0 BFC CERTIFY CCW
            3 16 -10 0 -10  10 0 -10  0 -20 0
            3 16 10 0 -10  10 0 10  0 -20 0
            3 16 10 0 10  -10 0 10  0 -20 0
            3 16 -10 0 10  -10 0 -10  0 -20 0
            """;

        // A part that is nothing but a placement of another, which is how most really work.
        const string placed = """
            0 Placed pyramid
            0 BFC CERTIFY CCW
            1 16 20 0 0  1 0 0  0 1 0  0 0 1  pyramid.dat
            """;

        return new FakeLDrawLibrary()
            .Add("pyramid.dat", pyramid)
            .Add("openpyramid.dat", openPyramid)
            .Add("placed.dat", placed);
    }

    private static PartsList AList() =>
        new(
            [
                new PartEntry(1, "pyramid", 11, "Black", Rgb24.Parse("#05131D"), 3),
                new PartEntry(2, "openpyramid", 5, "Red", Rgb24.Parse("#C91A09"), 2),
                new PartEntry(3, "placed", 11, "Black", Rgb24.Parse("#05131D"), 1),
            ],
            []);

    private static async Task<List<PreparedMesh>> BuildAsync(MeshPipelineOptions options)
    {
        var builder = new LDrawMeshBuilder(ALibrary());
        var shapes = new List<PreparedMesh>();

        foreach (var part in AList().DistinctPartNumbers)
        {
            shapes.Add(MeshPipeline.Prepare(await builder.BuildAsync(part), options));
        }

        return shapes;
    }

    // ---- The parts list ---------------------------------------------------------------------

    [Theory]
    [InlineData(DisplayLanguage.English, "parts-list-en.csv")]
    [InlineData(DisplayLanguage.Italian, "parts-list-it.csv")]
    public void The_parts_list_is_written_the_same_way(DisplayLanguage language, string name)
    {
        Snapshot.Matches(name, PartsListCsv.Write(AList(), language: language));
    }

    // ---- The shapes -------------------------------------------------------------------------

    [Fact]
    public async Task A_shape_file_is_written_the_same_way()
    {
        var shapes = await BuildAsync(new MeshPipelineOptions());
        var pyramid = shapes.Single(s => s.PartNumber == "pyramid");

        Snapshot.Matches("pyramid.stl", StlWriter.WriteBinary(pyramid.Mesh, "pyramid"));
    }

    [Fact]
    public async Task The_readable_form_of_a_shape_is_written_the_same_way()
    {
        var shapes = await BuildAsync(new MeshPipelineOptions());
        var pyramid = shapes.Single(s => s.PartNumber == "pyramid");

        Snapshot.Matches("pyramid.ascii.stl", StlWriter.WriteText(pyramid.Mesh, "pyramid"));
    }

    /// <summary>
    /// Taking the faces in is the change most likely to go quietly wrong, because the result
    /// still looks like the part. Keeping a copy makes any drift visible.
    /// </summary>
    [Fact]
    public async Task A_shape_taken_in_is_written_the_same_way()
    {
        var shapes = await BuildAsync(new MeshPipelineOptions
        {
            FillGaps = true,
            ClearanceMillimetres = 0.15f,
        });

        var pyramid = shapes.Single(s => s.PartNumber == "pyramid");
        pyramid.ClearanceApplied.Should().BeTrue();

        Snapshot.Matches("pyramid-0.15mm.stl", StlWriter.WriteBinary(pyramid.Mesh, "pyramid"));
    }

    // ---- The plates -------------------------------------------------------------------------

    [Fact]
    public async Task A_plate_is_written_the_same_way()
    {
        var shapes = await BuildAsync(new MeshPipelineOptions());
        var byPart = shapes.ToDictionary(s => s.PartNumber, s => s.Mesh, StringComparer.OrdinalIgnoreCase);

        var into = Path.Combine(Path.GetTempPath(), "lego2stl-plates-" + Guid.NewGuid().ToString("N"));

        try
        {
            var result = await PlateBuilder.WriteAsync(AList(), byPart, into);

            result.Plates.Should().HaveCount(2, "there are two colours in the list");

            Snapshot.Matches("black.3mf", await File.ReadAllBytesAsync(Path.Combine(into, "black.3mf")));
        }
        finally
        {
            if (Directory.Exists(into))
            {
                Directory.Delete(into, recursive: true);
            }
        }
    }

    // ---- The report -------------------------------------------------------------------------

    [Theory]
    [InlineData(DisplayLanguage.English, "report-en.txt")]
    [InlineData(DisplayLanguage.Italian, "report-it.txt")]
    public async Task The_report_reads_the_same_way(DisplayLanguage language, string name)
    {
        var settings = new RunSettings
        {
            Kind = InputKind.PartsList,
            InputPath = "parts.csv",
            Language = language,
            Clearance = 0.15,
            FillGaps = true,
        };

        var shapes = await BuildAsync(settings.MeshOptions);

        var outcome = new RunOutcome
        {
            Result = RunResult.Complete,
            Settings = settings,
            Layout = RunLayout.For(Path.Combine(Path.GetTempPath(), "snapshot", "parts.csv")),
            PartsList = AList(),
            Shapes = shapes,
            GeometrySource = "in-memory test library",
        };

        Snapshot.Matches(name, RunReport.Compose(outcome));
    }

    // ---- The command a set of settings amounts to ---------------------------------------------

    [Fact]
    public void The_shown_command_is_written_the_same_way()
    {
        var lines = new[]
        {
            new RunSettings { Kind = InputKind.Document, InputPath = "model.pdf", Pages = "2-5" },
            new RunSettings { Kind = InputKind.PartsList, InputPath = "parts.csv" },
            new RunSettings { Kind = InputKind.SetNumber, SetNumber = "42100-1", IncludeSpares = true },
            new RunSettings
            {
                Kind = InputKind.PartsList,
                InputPath = "parts.csv",
                Clearance = 0.15,
                FillGaps = true,
                Printer = "A1mini",
                PlateSpacing = 5,
                Language = DisplayLanguage.Italian,
            },
        }.Select(s => s.ToCommandLine());

        Snapshot.Matches("command-lines.txt", string.Join("\n", lines));
    }
}

using System.Numerics;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Run;

namespace Lego2STL.Tests.Run;

/// <summary>
/// A run that never happened, built by hand, so what a manifest records can be checked without
/// a document, a network or a shape library.
/// </summary>
/// <remarks>
/// Shared by the manifest, document, folder and pipeline suites deliberately: they are all
/// making claims about the same journey - outcome to manifest to document and back - and the
/// claims only mean something if they are made about one and the same run.
/// </remarks>
internal static class APretendRun
{
    public static readonly DateTimeOffset Started = new(2026, 8, 26, 9, 30, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Finished = new(2026, 8, 26, 9, 34, 12, TimeSpan.Zero);

    public static string TempFolder(string name = "run")
    {
        var folder = Path.Combine(
            Path.GetTempPath(), "lego2stl-run-" + Guid.NewGuid().ToString("N"), name);

        Directory.CreateDirectory(folder);
        return folder;
    }

    public static PartsList AList() => new(
    [
        new PartEntry(1, "32523", 11, "Black", Rgb24.Parse("#05131D"), 4),
        new PartEntry(2, "3705", 5, "Red", Rgb24.Parse("#C91A09"), 12),
        new PartEntry(3, "4265c", 9, "Light Gray", Rgb24.Parse("#9BA19D"), 8),
    ],
    ["one note about what was read"]);

    /// <summary>A shape whose measurements are worth recording: closed, and not thin.</summary>
    public static PreparedMesh AClosedShape(string partNumber) =>
        AShape(partNumber, openEdges: 0, span: 8f);

    /// <summary>A shape with holes in its surface and a wall under a nozzle's width.</summary>
    public static PreparedMesh AnOpenAndThinShape(string partNumber) =>
        AShape(partNumber, openEdges: 14, span: 0.3f);

    private static PreparedMesh AShape(string partNumber, int openEdges, float span)
    {
        // A box as tall and deep as it is asked to be thin, so ThinnestSpan is the span given.
        var mesh = VertexWelder.Weld(
        [
            new Triangle(new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(0, 10, 0)),
            new Triangle(new Vector3(0, 0, 0), new Vector3(0, 10, 0), new Vector3(0, 10, span)),
        ]);

        var quality = new MeshQuality(mesh.TriangleCount, mesh.VertexCount, 5, openEdges, 0);

        return new PreparedMesh(
            partNumber,
            Title: partNumber + " a made-up part",
            Mesh: mesh,
            Quality: quality,
            QualityBeforeRepair: quality,
            SeamsClosed: 0,
            GapsFilled: 0,
            DegenerateTrianglesRemoved: 0,
            MovedTo: null,
            MissingReferences: []);
    }

    public static RunSettings ASetting(string? apiKey = null) => new()
    {
        Kind = InputKind.PartsList,
        InputPath = "pistola.csv",
        Stages = RunStages.ShapesAndPlates,
        Clearance = 0.15,
        ScalePercent = 100,
        Printer = PrintBeds.Default.Name,
        ApiKey = apiKey,
    };

    /// <summary>A run that produced everything it was asked for.</summary>
    public static RunOutcome Complete(RunLayout layout, string? apiKey = null) => new()
    {
        Result = RunResult.Complete,
        Settings = ASetting(apiKey),
        Layout = layout,
        PartsList = AList(),
        Shapes = [AClosedShape("32523"), AnOpenAndThinShape("3705"), AClosedShape("4265c")],
        Plates = new PlateBuildResult(
        [
            new BuiltPlate("black-1.3mf", "Black", Rgb24.Parse("#05131D"), 1, 4, "60 x 40 mm"),
            new BuiltPlate("red-1.3mf", "Red", Rgb24.Parse("#C91A09"), 1, 12, "80 x 50 mm"),
        ],
        []),
        GeometrySource = "a made-up library",
        Notes = ["one note about what was read"],
    };

    /// <summary>A run that wrote a parts list and then refused to go on.</summary>
    public static RunOutcome NeedsADecision(RunLayout layout) => new()
    {
        Result = RunResult.Unverified,
        Settings = ASetting(),
        Layout = layout,
        PartsList = AList(),
        Shapes = [AClosedShape("32523")],
        Failed = [new FailedPart("3705", "no shape file for this part number")],
        GeometrySource = "a made-up library",
        Notes = [],
    };
}

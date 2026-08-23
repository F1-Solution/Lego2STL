using System.CommandLine;
using System.Text;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.LDraw;
using Lego2STL.Core.Run;

namespace Lego2STL.Cli.Commands;

/// <summary>
/// Turns a parts list into one shape file per distinct part.
/// </summary>
internal static class BuildCommand
{
    public static Command Create()
    {
        var input = new Argument<FileInfo>("parts-list")
        {
            Description = "A parts list written by the extract command.",
        };

        var ldrawDirectory = new Option<DirectoryInfo?>("--ldraw-dir")
        {
            Description = "An existing LDraw library to use instead of downloading anything.",
        };

        var offline = new Option<bool>("--offline")
        {
            Description = "Never use the network; a part that is not already available is reported.",
        };

        var asText = new Option<bool>("--ascii")
        {
            Description = "Write the readable form of STL instead of the compact one.",
        };

        var keepOrigin = new Option<bool>("--keep-origin")
        {
            Description = "Keep each part's own origin instead of standing it on the bed.",
        };

        var scale = new Option<double>("--scale")
        {
            Description = "Scale as a percentage. 100 is true size.",
            DefaultValueFactory = _ => 100.0,
        };

        var noRepair = new Option<bool>("--no-seam-repair")
        {
            Description = "Skip closing seams where a corner lies part-way along another edge.",
        };

        var outputDirectory = new Option<DirectoryInfo?>("--output-dir")
        {
            Description = "Where to put the run folder. Defaults to the parts list's own folder.",
        };

        var command = new Command("build", "Turn a parts list into one shape file per part.")
        {
            input, ldrawDirectory, offline, asText, keepOrigin, scale, noRepair, outputDirectory,
        };

        command.SetAction(async (parseResult, cancellationToken) => await RunAsync(
            parseResult.GetRequiredValue(input),
            parseResult.GetValue(ldrawDirectory),
            parseResult.GetValue(offline),
            parseResult.GetValue(asText),
            parseResult.GetValue(keepOrigin),
            parseResult.GetValue(scale),
            parseResult.GetValue(noRepair),
            parseResult.GetValue(outputDirectory),
            cancellationToken).ConfigureAwait(false));

        return command;
    }

    private static async Task<int> RunAsync(
        FileInfo input,
        DirectoryInfo? ldrawDirectory,
        bool offline,
        bool asText,
        bool keepOrigin,
        double scalePercent,
        bool noRepair,
        DirectoryInfo? outputDirectory,
        CancellationToken cancellationToken)
    {
        if (!input.Exists)
        {
            Console.Error.WriteLine($"Error: no such parts list: {input.FullName}");
            return Program.ExitFailure;
        }

        var list = await PartsListCsv.ReadFileAsync(input.FullName, cancellationToken).ConfigureAwait(false);
        var layout = RunLayout.For(input.FullName, outputDirectory?.FullName);
        layout.CreateDirectories();

        Console.WriteLine($"{list.Entries.Count} entries, {list.DistinctPartNumbers.Count} distinct parts.");

        var options = new MeshPipelineOptions
        {
            RepairSeams = !noRepair,
            PlaceOnBed = !keepOrigin,
            ScalePercent = (float)scalePercent,
        };

        using var library = new EscalatingLDrawLibrary(
            new LDrawSourceOptions
            {
                LocalDirectory = ldrawDirectory?.FullName,
                Offline = offline,
            },
            message => Console.WriteLine("  " + message));

        var builder = new LDrawMeshBuilder(library);

        var prepared = new List<PreparedMesh>();
        var failed = new List<(string Part, string Reason)>();

        foreach (var partNumber in list.DistinctPartNumbers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var mesh = await builder.BuildAsync(partNumber, cancellationToken).ConfigureAwait(false);

                if (mesh.IsEmpty)
                {
                    failed.Add((partNumber, "the part file contains no surfaces"));
                    continue;
                }

                var ready = MeshPipeline.Prepare(mesh, options);
                prepared.Add(ready);

                var path = Path.Combine(layout.StlDirectory, partNumber + (asText ? ".stl" : ".stl"));
                await StlWriter.WriteFileAsync(path, ready.Mesh, asText, partNumber, cancellationToken)
                    .ConfigureAwait(false);

                Console.WriteLine(
                    $"  {partNumber,-8} {ready.Mesh.TriangleCount,6} triangles  {ready.DescribeSize(),-28} " +
                    (ready.Quality.IsClosed ? "closed" : $"{ready.Quality.OpenEdgeCount} open edge(s)"));
            }
            catch (LDrawPartNotFoundException)
            {
                failed.Add((partNumber, "no LDraw file for this part number"));
            }
        }

        await WriteReportAsync(layout, list, prepared, failed, library, options, cancellationToken)
            .ConfigureAwait(false);

        var closed = prepared.Count(p => p.Quality.IsClosed);

        Console.WriteLine();
        Console.WriteLine($"Wrote {prepared.Count} shape file(s) to {layout.StlDirectory}");
        Console.WriteLine($"  {closed} closed, {prepared.Count - closed} with open edges " +
                          "(a slicer will repair those, and the report lists them).");
        Console.WriteLine($"Report: {layout.ReportPath}");

        if (failed.Count > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"{failed.Count} part(s) produced nothing:");
            foreach (var (part, reason) in failed)
            {
                Console.Error.WriteLine($"  {part}: {reason}");
            }

            return Program.ExitUnverified;
        }

        return Program.ExitOk;
    }

    private static async Task WriteReportAsync(
        RunLayout layout,
        PartsList list,
        List<PreparedMesh> prepared,
        List<(string Part, string Reason)> failed,
        EscalatingLDrawLibrary library,
        MeshPipelineOptions options,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Lego2STL shape report");
        sb.AppendLine(new string('-', 78));
        sb.AppendLine($"Geometry from : {library.Description}");
        sb.AppendLine($"Units         : millimetres, upright, {(options.PlaceOnBed ? "standing on zero" : "original origin")}");
        sb.AppendLine($"Scale         : {options.ScalePercent:0.##}%");
        sb.AppendLine($"Seam repair   : {(options.RepairSeams ? "on" : "off")}");
        sb.AppendLine();

        sb.AppendLine($"{"part",-10}{"tris",8}{"open",7}{"seams",7}  size (mm)");
        foreach (var p in prepared.OrderBy(p => p.PartNumber, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine(
                $"{p.PartNumber,-10}{p.Mesh.TriangleCount,8}{p.Quality.OpenEdgeCount,7}" +
                $"{p.SeamsClosed,7}  {p.DescribeSize()}" +
                (p.Title is null ? "" : $"  {p.Title}"));
        }

        sb.AppendLine();

        var closed = prepared.Count(p => p.Quality.IsClosed);
        sb.AppendLine($"{closed} of {prepared.Count} shapes are closed.");

        var seamTotal = prepared.Sum(p => p.SeamsClosed);
        var fixedByRepair = prepared.Count(p => !p.QualityBeforeRepair.IsClosed && p.Quality.IsClosed);
        sb.AppendLine($"Seam repair closed {seamTotal} edge(s) and completed {fixedByRepair} shape(s).");
        sb.AppendLine();

        var redirected = prepared.Where(p => p.MovedTo is not null).ToList();
        if (redirected.Count > 0)
        {
            sb.AppendLine("Retired numbers, where the shape comes from a replacement part:");
            foreach (var p in redirected)
            {
                sb.AppendLine($"  {p.PartNumber} -> {p.MovedTo}");
            }

            sb.AppendLine();
        }

        var withMissing = prepared.Where(p => p.MissingReferences.Count > 0).ToList();
        if (withMissing.Count > 0)
        {
            sb.AppendLine("Parts built with something missing:");
            foreach (var p in withMissing)
            {
                sb.AppendLine($"  {p.PartNumber}: {string.Join(", ", p.MissingReferences.Take(6))}");
            }

            sb.AppendLine();
        }

        if (failed.Count > 0)
        {
            sb.AppendLine("Produced nothing:");
            foreach (var (part, reason) in failed)
            {
                sb.AppendLine($"  {part}: {reason}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("Note on printing");
        sb.AppendLine("  These shapes are the catalogue's nominal dimensions, with no allowance for");
        sb.AppendLine("  the clearance a real moulded part has. Printed at true size they will be");
        sb.AppendLine("  slightly oversized on every face and will not clip together without a");
        sb.AppendLine("  clearance allowance calibrated for your own printer and material.");

        await File.WriteAllTextAsync(layout.ReportPath, sb.ToString(), new UTF8Encoding(true), cancellationToken)
            .ConfigureAwait(false);
    }
}

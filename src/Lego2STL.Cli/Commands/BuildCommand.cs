using System.CommandLine;
using System.Globalization;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.LDraw;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;

namespace Lego2STL.Cli.Commands;

/// <summary>
/// Turns a parts list into one shape file per distinct part, plus a plate per colour.
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

        var clearance = new Option<double>("--clearance")
        {
            Description =
                "Take every face in by this many millimetres, so printed parts clip together. " +
                "Run the calibration command to find the right figure for your printer.",
            DefaultValueFactory = _ => 0.0,
        };

        var repair = new Option<bool>("--repair")
        {
            Description =
                "Also cover over the gaps left in a shape's surface, making it a solid. Needed " +
                "before a clearance can be applied to a part that has any.",
        };

        var noRepair = new Option<bool>("--no-seam-repair")
        {
            Description = "Skip closing seams where a corner lies part-way along another edge.",
        };

        var outputDirectory = new Option<DirectoryInfo?>("--output-dir")
        {
            Description = "Where to put the run folder. Defaults to the parts list's own folder.",
        };

        var printer = new Option<string>("--printer")
        {
            Description =
                $"Which printer's bed to lay the plates out for: {string.Join(", ", PrintBeds.Names)}.",
            DefaultValueFactory = _ => PrintBeds.Default.Name,
        };

        var plateSize = new Option<string?>("--plate-size")
        {
            Description =
                "A bed size in millimetres instead of a printer name, e.g. 220x220 or 300x300x400.",
        };

        var plateSpacing = new Option<double>("--plate-spacing")
        {
            Description = "Millimetres to leave between parts on a plate.",
            DefaultValueFactory = _ => 3.0,
        };

        var noPlates = new Option<bool>("--no-plates")
        {
            Description = "Write only the shape files, no coloured plates.",
        };

        var command = new Command("build", "Turn a parts list into shape files and coloured plates.")
        {
            input, ldrawDirectory, offline, asText, keepOrigin, scale, clearance, repair, noRepair,
            outputDirectory, printer, plateSize, plateSpacing, noPlates,
        };

        command.SetAction(async (parseResult, cancellationToken) => await RunAsync(
            new Settings(
                parseResult.GetRequiredValue(input),
                parseResult.GetValue(ldrawDirectory),
                parseResult.GetValue(offline),
                parseResult.GetValue(asText),
                parseResult.GetValue(keepOrigin),
                parseResult.GetValue(scale),
                parseResult.GetValue(clearance),
                parseResult.GetValue(repair),
                parseResult.GetValue(noRepair),
                parseResult.GetValue(outputDirectory),
                parseResult.GetValue(printer) ?? PrintBeds.Default.Name,
                parseResult.GetValue(plateSize),
                parseResult.GetValue(plateSpacing),
                parseResult.GetValue(noPlates),
                parseResult.GetValue(CommonOptions.Language)),
            cancellationToken).ConfigureAwait(false));

        return command;
    }

    /// <summary>Everything the command was asked for, gathered so the run reads as one step.</summary>
    private sealed record Settings(
        FileInfo Input,
        DirectoryInfo? LDrawDirectory,
        bool Offline,
        bool AsText,
        bool KeepOrigin,
        double ScalePercent,
        double Clearance,
        bool FillGaps,
        bool NoSeamRepair,
        DirectoryInfo? OutputDirectory,
        string Printer,
        string? PlateSize,
        double PlateSpacing,
        bool NoPlates,
        DisplayLanguage Language);

    private static async Task<int> RunAsync(Settings settings, CancellationToken cancellationToken)
    {
        var words = Strings.For(settings.Language);

        if (!settings.Input.Exists)
        {
            Console.Error.WriteLine(
                $"{words[TextKey.MsgError]}: " +
                words.Format(TextKey.MsgNoSuchPartsList, settings.Input.FullName));
            return Program.ExitFailure;
        }

        if (settings.Clearance < 0)
        {
            Console.Error.WriteLine(
                $"{words[TextKey.MsgError]}: " +
                words.Format(TextKey.ErrClearanceNegative, settings.Clearance));
            return Program.ExitFailure;
        }

        var bed = settings.PlateSize is { Length: > 0 }
            ? PrintBeds.Parse(settings.PlateSize)
            : PrintBeds.Parse(settings.Printer);

        var list = await PartsListCsv.ReadFileAsync(settings.Input.FullName, cancellationToken)
            .ConfigureAwait(false);

        var layout = RunLayout.For(settings.Input.FullName, settings.OutputDirectory?.FullName);
        layout.CreateDirectories();

        Console.WriteLine(words.Format(
            TextKey.MsgEntriesAndParts, list.Entries.Count, list.DistinctPartNumbers.Count));

        var options = new MeshPipelineOptions
        {
            RepairSeams = !settings.NoSeamRepair,
            PlaceOnBed = !settings.KeepOrigin,
            FillGaps = settings.FillGaps,
            ScalePercent = (float)settings.ScalePercent,
            ClearanceMillimetres = (float)settings.Clearance,
        };

        using var library = new EscalatingLDrawLibrary(
            new LDrawSourceOptions
            {
                LocalDirectory = settings.LDrawDirectory?.FullName,
                Offline = settings.Offline,
            },
            message => Console.WriteLine("  " + message));

        var builder = new LDrawMeshBuilder(library);

        var prepared = new List<PreparedMesh>();
        var failed = new List<(string Part, string Reason)>();
        var shapes = new Dictionary<string, IndexedMesh>(StringComparer.OrdinalIgnoreCase);

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
                shapes[partNumber] = ready.Mesh;

                var path = Path.Combine(layout.StlDirectory, partNumber + ".stl");
                await StlWriter
                    .WriteFileAsync(path, ready.Mesh, settings.AsText, partNumber, cancellationToken)
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

        var plates = await WritePlatesAsync(settings, words, list, shapes, layout, bed, failed, cancellationToken)
            .ConfigureAwait(false);

        await BuildReport
            .WriteAsync(layout, words, prepared, failed, plates, library.Description, options, bed, cancellationToken)
            .ConfigureAwait(false);

        var closed = prepared.Count(p => p.Quality.IsClosed);

        Console.WriteLine();
        ReportClearance(prepared, words, settings.Clearance, settings.FillGaps);
        Console.WriteLine(words.Format(TextKey.MsgWroteShapes, prepared.Count, layout.StlDirectory));
        Console.WriteLine("  " + words.Format(TextKey.MsgClosedAndOpen, closed, prepared.Count - closed));

        if (plates is not null)
        {
            Console.WriteLine(words.Format(
                TextKey.MsgWrotePlates, plates.Plates.Count, layout.PlateDirectory));
            Console.WriteLine("  " + words.Format(
                TextKey.ReportPlateSummary, plates.Plates.Count, plates.ColorCount, plates.PieceCount));
        }

        Console.WriteLine(words.Format(TextKey.MsgReportWritten, layout.ReportPath));

        if (failed.Count > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(words.Format(TextKey.MsgProducedNothing, failed.Count));
            foreach (var (part, reason) in failed)
            {
                Console.Error.WriteLine($"  {part}: {reason}");
            }

            return Program.ExitUnverified;
        }

        return Program.ExitOk;
    }

    /// <summary>
    /// Plates come last and only when everything resolved, because a plate that silently
    /// leaves out the parts that failed looks complete and is not.
    /// </summary>
    private static async Task<PlateBuildResult?> WritePlatesAsync(
        Settings settings,
        Strings words,
        PartsList list,
        IReadOnlyDictionary<string, IndexedMesh> shapes,
        RunLayout layout,
        PrintBed bed,
        List<(string Part, string Reason)> failed,
        CancellationToken cancellationToken)
    {
        if (settings.NoPlates)
        {
            Console.WriteLine(words[TextKey.MsgNoPlatesRequested]);
            return null;
        }

        if (failed.Count > 0)
        {
            Console.WriteLine(words[TextKey.MsgPlatesSkippedUnverified]);
            return null;
        }

        var result = await PlateBuilder.WriteAsync(
            list,
            shapes,
            layout.PlateDirectory,
            new PackingOptions { Bed = bed, Spacing = (float)settings.PlateSpacing },
            cancellationToken).ConfigureAwait(false);

        foreach (var note in result.Skipped)
        {
            Console.WriteLine("  " + note);
        }

        return result;
    }

    /// <summary>
    /// One line for the clearance, not one paragraph per part. Which parts were refused, and
    /// why, is in the report; the console says how many and what to do about it.
    /// </summary>
    private static void ReportClearance(
        IReadOnlyList<PreparedMesh> prepared, Strings words, double clearance, bool gapsCovered)
    {
        if (clearance <= 0 || prepared.Count == 0)
        {
            return;
        }

        var applied = prepared.Count(p => p.ClearanceApplied);
        Console.WriteLine(words.Format(TextKey.MsgClearanceApplied, applied, prepared.Count, clearance));

        var open = prepared.Count(p => p.ClearanceRefusedBecause == "open");
        var thin = prepared.Count(p => p.ClearanceRefusedBecause == "thin");

        if (open > 0)
        {
            Console.WriteLine("  " + words.Format(
                gapsCovered ? TextKey.MsgClearanceRefusedStillOpen : TextKey.MsgClearanceRefusedOpen,
                open));
        }

        if (thin > 0)
        {
            Console.WriteLine("  " + words.Format(TextKey.MsgClearanceRefusedThin, thin));
        }
    }
}

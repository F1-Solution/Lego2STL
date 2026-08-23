using System.CommandLine;
using System.Globalization;
using System.Text;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.LDraw;
using Lego2STL.Core.Text;

namespace Lego2STL.Cli.Commands;

/// <summary>
/// Writes the same small part several times at different clearances, so the right one can be
/// measured instead of guessed.
/// </summary>
/// <remarks>
/// <para>
/// Clearance cannot be worked out in advance. It depends on the printer, the nozzle, the
/// material, the temperature and how the machine happens to be behaving today, and the figure
/// that matters is smaller than the spread between two machines of the same model. So the tool
/// does not offer a default: it offers a way of finding yours.
/// </para>
/// <para>
/// The default pair is an axle and the bush that runs on it, because a fit is a property of two
/// parts and not of one. Printing both at each value and trying them together answers the
/// question directly - this one is loose, that one will not go on, so it is the one between.
/// </para>
/// </remarks>
internal static class CalibrationCommand
{
    /// <summary>
    /// An axle and the bush that goes on it: the smallest pair that actually tests a fit, and
    /// both come out closed, so a clearance can be applied to them without covering any gaps.
    /// </summary>
    private static readonly string[] DefaultParts = ["3705", "4265c"];

    private static readonly double[] DefaultSteps = [0.00, 0.05, 0.10, 0.15, 0.20, 0.25];

    public static Command Create()
    {
        var parts = new Option<string[]>("--part")
        {
            Description =
                "Which part to print the set from. Repeat for several. " +
                $"Defaults to {string.Join(" and ", DefaultParts)}, an axle and the bush that fits it.",
            AllowMultipleArgumentsPerToken = true,
        };

        var steps = new Option<string?>("--steps")
        {
            Description =
                "The clearances to try, in millimetres, separated by commas. " +
                $"Defaults to {string.Join(",", DefaultSteps.Select(s => s.ToString("0.00", CultureInfo.InvariantCulture)))}.",
        };

        var outputDirectory = new Option<DirectoryInfo?>("--output-dir")
        {
            Description = "Where to write the set. Defaults to a 'calibration' folder here.",
        };

        var ldrawDirectory = new Option<DirectoryInfo?>("--ldraw-dir")
        {
            Description = "An existing LDraw library to use instead of downloading anything.",
        };

        var offline = new Option<bool>("--offline")
        {
            Description = "Never use the network.",
        };

        var asText = new Option<bool>("--ascii")
        {
            Description = "Write the readable form of STL instead of the compact one.",
        };

        var command = new Command(
            "calibration",
            "Write one small part at several clearances, to find the one your printer needs.")
        {
            parts, steps, outputDirectory, ldrawDirectory, offline, asText,
        };

        command.SetAction(async (parseResult, cancellationToken) => await RunAsync(
            parseResult.GetValue(parts) is { Length: > 0 } chosen ? chosen : DefaultParts,
            ParseSteps(parseResult.GetValue(steps)),
            parseResult.GetValue(outputDirectory),
            parseResult.GetValue(ldrawDirectory),
            parseResult.GetValue(offline),
            parseResult.GetValue(asText),
            parseResult.GetValue(CommonOptions.Language),
            cancellationToken).ConfigureAwait(false));

        return command;
    }

    private static double[] ParseSteps(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return DefaultSteps;
        }

        var values = new List<double>();

        foreach (var piece in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!double.TryParse(piece, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException($"'{piece}' is not a number of millimetres.");
            }

            if (value < 0)
            {
                throw new FormatException($"A clearance cannot be negative; got {piece}.");
            }

            values.Add(value);
        }

        return values.Count > 0 ? [.. values.Distinct().Order()] : DefaultSteps;
    }

    private static async Task<int> RunAsync(
        IReadOnlyList<string> parts,
        IReadOnlyList<double> steps,
        DirectoryInfo? outputDirectory,
        DirectoryInfo? ldrawDirectory,
        bool offline,
        bool asText,
        DisplayLanguage language,
        CancellationToken cancellationToken)
    {
        var words = Strings.For(language);
        var directory = outputDirectory?.FullName ?? Path.Combine(Environment.CurrentDirectory, "calibration");

        Directory.CreateDirectory(directory);

        using var library = new EscalatingLDrawLibrary(
            new LDrawSourceOptions
            {
                LocalDirectory = ldrawDirectory?.FullName,
                Offline = offline,
            },
            message => Console.WriteLine("  " + message));

        var builder = new LDrawMeshBuilder(library);
        var written = 0;
        var rows = new List<string>();

        foreach (var partNumber in parts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PartMesh source;
            try
            {
                source = await builder.BuildAsync(partNumber, cancellationToken).ConfigureAwait(false);
            }
            catch (LDrawPartNotFoundException ex)
            {
                Console.Error.WriteLine($"{words[TextKey.MsgError]}: {ex.Message}");
                return Program.ExitFailure;
            }

            foreach (var step in steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var prepared = MeshPipeline.Prepare(source, new MeshPipelineOptions
                {
                    // Gaps are covered here whatever was asked for: a calibration piece that
                    // silently came out at true size would send the whole exercise wrong.
                    FillGaps = true,
                    ClearanceMillimetres = (float)step,
                });

                var name = string.Create(
                    CultureInfo.InvariantCulture, $"{partNumber}-{step:0.00}mm.stl");

                await StlWriter
                    .WriteFileAsync(Path.Combine(directory, name), prepared.Mesh, asText, name, cancellationToken)
                    .ConfigureAwait(false);

                written++;
                rows.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{name,-28}{step,8:0.00}{(prepared.ClearanceApplied ? string.Empty : "  not applied"),-14}  {prepared.DescribeSize()}"));

                Console.WriteLine($"  {rows[^1]}");
            }
        }

        await WriteInstructionsAsync(directory, parts, steps, rows, words, cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine(words.Format(TextKey.MsgCalibrationWritten, written, directory));

        return Program.ExitOk;
    }

    /// <summary>
    /// A note beside the files saying what to do with them. A folder of near-identical shapes
    /// is useless without one, and by the time they are printed the command line that made
    /// them is long gone.
    /// </summary>
    private static async Task WriteInstructionsAsync(
        string directory,
        IReadOnlyList<string> parts,
        IReadOnlyList<double> steps,
        IReadOnlyList<string> rows,
        Strings words,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        sb.AppendLine(words[TextKey.CalibrationTitle]);
        sb.AppendLine(new string('-', 70));
        sb.AppendLine();
        sb.AppendLine(words.Format(
            TextKey.CalibrationHow,
            string.Join(", ", parts),
            string.Join(", ", steps.Select(s => s.ToString("0.00", CultureInfo.InvariantCulture)))));
        sb.AppendLine();

        foreach (var row in rows)
        {
            sb.AppendLine(row);
        }

        sb.AppendLine();
        sb.AppendLine(words[TextKey.CalibrationThen]);

        await File.WriteAllTextAsync(
                Path.Combine(directory, "how-to-use-these.txt"),
                sb.ToString(),
                new UTF8Encoding(true),
                cancellationToken)
            .ConfigureAwait(false);
    }
}

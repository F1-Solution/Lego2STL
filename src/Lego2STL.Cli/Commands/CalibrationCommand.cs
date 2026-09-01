using System.CommandLine;
using System.Globalization;
using Lego2STL.Core.LDraw;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;

namespace Lego2STL.Cli.Commands;

/// <summary>
/// Writes a calibration plate: the same small parts several times at different clearances, so
/// the right one can be measured instead of guessed.
/// </summary>
/// <remarks>
/// <para>
/// Clearance cannot be worked out in advance. It depends on the printer, the nozzle, the
/// material, the temperature and how the machine happens to be behaving today, and the figure
/// that matters is smaller than the spread between two machines of the same model. So the tool
/// does not offer a default: it offers a way of finding yours.
/// </para>
/// <para>
/// Three mating pairs, because a fit is a property of two parts and not of one, plus one wide
/// plate that tests warping rather than clearance. The set is the set: substituting a part would
/// leave the sheet describing a plate that was not built.
/// </para>
/// </remarks>
internal static class CalibrationCommand
{
    public static Command Create(Strings words)
    {
        var steps = new Option<string?>("--steps")
        {
            Description = words[TextKey.HelpArgSteps] +
                $" Defaults to {string.Join(",", CalibrationSet.DefaultSteps.Select(s => s.ToString("0.00", CultureInfo.InvariantCulture)))}.",
        };

        var printer = new Option<string>("--printer")
        {
            Description = words.Format(TextKey.HelpOptPrinter, string.Join(", ", PrintBeds.Names)),
            DefaultValueFactory = _ => PrintBeds.Default.Name,
        };

        var outputDirectory = new Option<DirectoryInfo?>("--output-dir")
        {
            Description = words[TextKey.HelpOptBricksOutputDir],
        };

        var ldrawDirectory = new Option<DirectoryInfo?>("--ldraw-dir")
        {
            Description = words[TextKey.HelpOptLDrawDir],
        };

        var offline = new Option<bool>("--offline")
        {
            Description = words[TextKey.HelpOptOffline],
        };

        var save = new Option<double?>("--save")
        {
            Description = words[TextKey.HelpOptSave],
        };

        var name = new Option<string?>("--name")
        {
            Description = words[TextKey.HelpOptToleranceName],
        };

        var preferred = new Option<bool>("--preferred")
        {
            Description = words[TextKey.HelpOptPreferred],
        };

        var list = new Option<bool>("--list")
        {
            Description = words[TextKey.HelpOptListTolerances],
        };

        var prefer = new Option<string?>("--prefer")
        {
            Description = words[TextKey.HelpOptPrefer],
        };

        var forget = new Option<string?>("--forget")
        {
            Description = words[TextKey.HelpOptForget],
        };

        var command = new Command("calibration", words[TextKey.HelpCalibration])
        {
            steps, printer, outputDirectory, ldrawDirectory, offline,
            save, name, preferred, list, prefer, forget,
        };

        command.SetAction(async (parseResult, cancellationToken) => await RunAsync(
            ParseSteps(parseResult.GetValue(steps)),
            parseResult.GetValue(printer) ?? PrintBeds.Default.Name,
            parseResult.GetValue(outputDirectory),
            parseResult.GetValue(ldrawDirectory),
            parseResult.GetValue(offline),
            parseResult.GetValue(save),
            parseResult.GetValue(name),
            parseResult.GetValue(preferred),
            parseResult.GetValue(list),
            parseResult.GetValue(prefer),
            parseResult.GetValue(forget),
            parseResult.GetValue(CommonOptions.Language),
            cancellationToken).ConfigureAwait(false));

        return command;
    }

    private static double[] ParseSteps(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [.. CalibrationSet.DefaultSteps];
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

        return values.Count > 0 ? [.. values.Distinct().Order()] : [.. CalibrationSet.DefaultSteps];
    }

    private static async Task<int> RunAsync(
        IReadOnlyList<double> steps,
        string printer,
        DirectoryInfo? outputDirectory,
        DirectoryInfo? ldrawDirectory,
        bool offline,
        double? save,
        string? name,
        bool preferred,
        bool list,
        string? prefer,
        string? forget,
        DisplayLanguage language,
        CancellationToken cancellationToken)
    {
        var words = Strings.For(language);

        // The management flags record or report and stop. Placed before anything is created so
        // that asking what is saved never leaves a folder behind.
        if (list)
        {
            foreach (var preset in TolerancePresets.Load())
            {
                Console.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"  {(preset.Preferred ? "*" : " ")} {preset.Name,-40}{preset.Millimetres,8:0.00} mm"));
            }

            return Program.ExitOk;
        }

        if (prefer is { Length: > 0 })
        {
            TolerancePresets.Prefer(prefer);
            return Program.ExitOk;
        }

        if (forget is { Length: > 0 })
        {
            TolerancePresets.Forget(forget);
            return Program.ExitOk;
        }

        if (save is { } measured)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Console.Error.WriteLine($"{words[TextKey.MsgError]}: {words[TextKey.ErrToleranceNeedsAName]}");
                return Program.ExitFailure;
            }

            TolerancePresets.Remember(name, measured, preferred);
            Console.WriteLine(words.Format(TextKey.MsgToleranceSaved, name, measured));
            return Program.ExitOk;
        }

        var directory = outputDirectory?.FullName ?? Path.Combine(Environment.CurrentDirectory, "calibration");

        using var library = new EscalatingLDrawLibrary(
            new LDrawSourceOptions
            {
                LocalDirectory = ldrawDirectory?.FullName,
                Offline = offline,
            },
            message => Console.WriteLine("  " + message),
            words);

        var builder = new LDrawMeshBuilder(library);

        // A part the library has not got is left off rather than stopping a plate whose value is
        // mostly still there.
        var sources = new Dictionary<string, PartMesh>(StringComparer.OrdinalIgnoreCase);

        foreach (var partNumber in CalibrationSet.PartNumbers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                sources[partNumber] = await builder.BuildAsync(partNumber, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (LDrawPartNotFoundException)
            {
                // Named on the sheet by the run itself, from what it was not given.
            }
        }

        var result = await CalibrationRun
            .WriteAsync(sources, steps, printer, directory, words, cancellationToken)
            .ConfigureAwait(false);

        if (result.PieceCount == 0)
        {
            Console.Error.WriteLine(
                $"{words[TextKey.MsgError]}: {words[TextKey.ErrCalibrationNothingToBuild]}");
            return Program.ExitFailure;
        }

        Console.WriteLine();
        Console.WriteLine(words.Format(TextKey.MsgCalibrationWritten, result.PieceCount, directory));

        return Program.ExitOk;
    }
}

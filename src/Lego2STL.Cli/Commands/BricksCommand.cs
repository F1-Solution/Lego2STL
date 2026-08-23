using System.CommandLine;
using Lego2STL.Core.Bricks;
using Lego2STL.Core.Text;

namespace Lego2STL.Cli.Commands;

/// <summary>
/// Generates pieces by size, rather than looking real ones up.
/// </summary>
/// <remarks>
/// Separate from everything else on purpose. The shape comes from MachineBlocks, published
/// under a non-commercial share-alike licence, so it is neither included nor fetched: it has
/// to be your own copy, driven by your own OpenSCAD. This command writes a few lines saying
/// which piece is wanted and hands them over.
/// </remarks>
internal static class BricksCommand
{
    public static Command Create()
    {
        var sizes = new Argument<string[]>("size")
        {
            Description = "Sizes to make, e.g. 2x4 or 2x4x6 to say how many plates tall.",
            Arity = ArgumentArity.OneOrMore,
        };

        var kind = new Option<BrickKind>("--kind")
        {
            Description = "Brick (three plates), plate (one), or tile (one, smooth on top).",
            DefaultValueFactory = _ => BrickKind.Brick,
        };

        var noKnobs = new Option<bool>("--no-knobs")
        {
            Description = "Leave the top smooth. A tile is already smooth.",
        };

        var noStudHoles = new Option<bool>("--no-stud-holes")
        {
            Description = "Make it solid underneath. The hollow tubes are what make a piece grip.",
        };

        var outputDirectory = new Option<DirectoryInfo?>("--output-dir")
        {
            Description = "Where to write. Defaults to a 'bricks' folder here.",
        };

        var openScad = new Option<FileInfo?>("--openscad")
        {
            Description = "Where OpenSCAD is, when it is not on the path.",
        };

        var machineBlocks = new Option<DirectoryInfo?>("--machineblocks")
        {
            Description =
                "Your copy of the MachineBlocks library. Also read from MACHINEBLOCKS_DIR.",
        };

        var describeOnly = new Option<bool>("--scad-only")
        {
            Description = "Write the OpenSCAD description and stop, without generating a shape.",
        };

        var command = new Command(
            "bricks",
            "Generate a piece by size, using your own OpenSCAD and your own copy of MachineBlocks.")
        {
            sizes, kind, noKnobs, noStudHoles, outputDirectory, openScad, machineBlocks, describeOnly,
        };

        command.SetAction(async (parseResult, cancellationToken) => await RunAsync(
            parseResult.GetRequiredValue(sizes),
            parseResult.GetValue(kind),
            parseResult.GetValue(noKnobs),
            parseResult.GetValue(noStudHoles),
            parseResult.GetValue(outputDirectory),
            parseResult.GetValue(openScad),
            parseResult.GetValue(machineBlocks),
            parseResult.GetValue(describeOnly),
            parseResult.GetValue(CommonOptions.Language),
            cancellationToken).ConfigureAwait(false));

        return command;
    }

    private static async Task<int> RunAsync(
        string[] sizes,
        BrickKind kind,
        bool noKnobs,
        bool noStudHoles,
        DirectoryInfo? outputDirectory,
        FileInfo? openScadPath,
        DirectoryInfo? libraryPath,
        bool describeOnly,
        Core.Text.DisplayLanguage language,
        CancellationToken cancellationToken)
    {
        var words = Strings.For(language);
        var directory = outputDirectory?.FullName ?? Path.Combine(Environment.CurrentDirectory, "bricks");

        List<BrickSpec> specs;
        try
        {
            specs = [.. sizes.Select(s => BrickSpec.Parse(s, kind, !noKnobs, !noStudHoles))];
        }
        catch (FormatException ex)
        {
            Console.Error.WriteLine($"{words[TextKey.MsgError]}: {ex.Message}");
            return Program.ExitFailure;
        }

        OpenScadRunner runner;
        try
        {
            runner = OpenScadRunner.Create(openScadPath?.FullName, libraryPath?.FullName);
        }
        catch (OpenScadUnavailableException ex)
        {
            Console.Error.WriteLine($"{words[TextKey.MsgError]}: {ex.Message}");
            return Program.ExitFailure;
        }

        Console.WriteLine($"OpenSCAD:      {runner.OpenScadPath}");
        Console.WriteLine($"MachineBlocks: {runner.LibraryPath}");
        Console.WriteLine();

        var made = 0;

        foreach (var spec in specs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await runner
                    .GenerateAsync(spec, directory, describeOnly, cancellationToken)
                    .ConfigureAwait(false);

                Console.WriteLine($"  {spec}");
                Console.WriteLine($"    {result.ScadPath}");

                if (result.ShapePath is not null)
                {
                    Console.WriteLine($"    {result.ShapePath}");
                    made++;
                }
            }
            catch (OpenScadUnavailableException ex)
            {
                Console.Error.WriteLine($"  {spec}: {ex.Message}");
                return Program.ExitUnverified;
            }
        }

        Console.WriteLine();
        Console.WriteLine(describeOnly
            ? $"Wrote {specs.Count} description(s) to {directory}. Run OpenSCAD on them yourself."
            : $"Wrote {made} shape(s) to {directory}");

        Console.WriteLine();
        Console.WriteLine(
            "These shapes come from MachineBlocks, under CC BY-NC-SA: for your own use, not " +
            "for selling, and anything shared has to be shared on the same terms.");

        return Program.ExitOk;
    }
}

using System.CommandLine;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Rebrickable;

namespace Lego2STL.Cli.Commands;

/// <summary>
/// Regenerates the colour cross-reference from the Rebrickable API.
/// </summary>
/// <remarks>
/// Not part of a normal run. The generated file is committed into
/// <c>src/Lego2STL.Core/Data/</c> and embedded in the assembly, which is what lets an
/// ordinary run work with no API key and no network. Re-run this only when LEGO adds or
/// retires a colour.
/// </remarks>
internal static class RefreshColorsCommand
{
    public static Command Create()
    {
        var outputOption = new Option<FileInfo?>("--out", "-o")
        {
            Description = "Where to write the reference file. Defaults to the copy inside the source tree.",
        };

        var apiKeyOption = new Option<string?>("--api-key")
        {
            Description =
                "Rebrickable API key. Falls back to the REBRICKABLE_API_KEY environment variable, " +
                "then to %APPDATA%\\Lego2STL\\config.json.",
        };

        var dumpOption = new Option<DirectoryInfo?>("--rebrickable-dump")
        {
            Description =
                "Optional folder holding a Rebrickable CSV download. Only used to record per-colour " +
                "part counts, which act as a tie-break when two colours claim the same external code.",
        };

        var command = new Command("refresh-colors", "Regenerate the colour cross-reference from the Rebrickable API.")
        {
            outputOption,
            apiKeyOption,
            dumpOption,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var output = parseResult.GetValue(outputOption);
            var apiKey = parseResult.GetValue(apiKeyOption);
            var dump = parseResult.GetValue(dumpOption);

            return await RunAsync(output, apiKey, dump, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private static async Task<int> RunAsync(
        FileInfo? output,
        string? apiKey,
        DirectoryInfo? dump,
        CancellationToken cancellationToken)
    {
        var key = RebrickableApiKey.Require(apiKey);
        var target = output ?? new FileInfo(DefaultOutputPath());

        var partCounts = RebrickableDump.TryReadColorPartCounts(dump?.FullName);
        Console.WriteLine(partCounts.Count > 0
            ? $"Read part counts for {partCounts.Count} colours from the local dump."
            : "No local Rebrickable dump given; part counts will be recorded as 0.");

        Console.WriteLine("Fetching colours from the Rebrickable API...");
        using var client = new RebrickableClient(key);
        var result = await ColorTableBuilder.BuildAsync(client, partCounts, cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"Fetched {result.Colors.Count} colours.");
        foreach (var scheme in Enum.GetValues<ColorScheme>())
        {
            Console.WriteLine($"  {scheme,-12} {result.Table.CountIn(scheme),4} codes resolve");
        }

        if (result.Notes.Count > 0)
        {
            Console.WriteLine($"\n{result.Notes.Count} ambiguity/ies resolved:");
            foreach (var note in result.Notes)
            {
                Console.WriteLine("  - " + note);
            }
        }

        target.Directory?.Create();
        await File.WriteAllTextAsync(target.FullName, ColorTableCsv.Write(result.Colors, result.Mappings), cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine($"\nWrote {target.FullName}");
        Console.WriteLine("Rebuild so the new table is embedded in the assembly.");
        return 0;
    }

    /// <summary>
    /// Walks up from the executable looking for the source tree, so running this from
    /// bin/Debug/... still writes to the file that gets committed.
    /// </summary>
    private static string DefaultOutputPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Lego2STL.Core", "Data");
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "Lego2STL.Core")))
            {
                return Path.Combine(candidate, ColorReference.FileName);
            }

            dir = dir.Parent;
        }

        return Path.Combine(Environment.CurrentDirectory, ColorReference.FileName);
    }
}

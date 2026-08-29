using System.Globalization;

namespace Lego2STL.Core.Rebrickable;

/// <summary>
/// Reads the CSV files from a Rebrickable bulk download, when one is available locally.
/// </summary>
/// <remarks>
/// Entirely optional. The dump is large (its <c>inventory_parts.csv</c> alone is 132 MB),
/// is not ours to redistribute, and is therefore never committed. It is used only to
/// enrich generated data — colour part counts today — and every caller must work without it.
/// </remarks>
public static class RebrickableDump
{
    /// <summary>Reads <c>colors.csv</c> and returns Rebrickable colour id to part count.</summary>
    /// <returns>An empty map when the file is absent or unreadable.</returns>
    public static IReadOnlyDictionary<int, int> TryReadColorPartCounts(string? dumpDirectory)
    {
        var empty = new Dictionary<int, int>();
        if (string.IsNullOrWhiteSpace(dumpDirectory))
        {
            return empty;
        }

        var path = Path.Combine(dumpDirectory, "colors.csv");
        if (!File.Exists(path))
        {
            return empty;
        }

        try
        {
            var counts = new Dictionary<int, int>();
            var lines = File.ReadAllLines(path);
            if (lines.Length == 0)
            {
                return empty;
            }

            var header = SplitCsvLine(lines[0]);
            var idIndex = Array.FindIndex(header, h => h.Equals("id", StringComparison.OrdinalIgnoreCase));
            var countIndex = Array.FindIndex(header, h => h.Equals("num_parts", StringComparison.OrdinalIgnoreCase));
            if (idIndex < 0 || countIndex < 0)
            {
                return empty;
            }

            foreach (var line in lines.Skip(1))
            {
                if (line.Length == 0)
                {
                    continue;
                }

                var f = SplitCsvLine(line);
                if (f.Length <= Math.Max(idIndex, countIndex))
                {
                    continue;
                }

                if (int.TryParse(f[idIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) &&
                    int.TryParse(f[countIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                {
                    counts[id] = n;
                }
            }

            return counts;
        }
        catch (IOException)
        {
            return empty;   // optional input: never fail the run because of it
        }
    }

    /// <summary>The file in a dump that maps element numbers to parts and colours.</summary>
    public const string ElementsFileName = "elements.csv";

    /// <summary>
    /// Reads <c>elements.csv</c> and returns element number to part number and Rebrickable
    /// colour id.
    /// </summary>
    /// <param name="path">
    /// The file itself, or a folder holding it. A folder is searched one level deep as well,
    /// so pointing at a project folder that contains an unpacked dump beside it works.
    /// </param>
    /// <returns>An empty map when there is no such file, or it cannot be read.</returns>
    public static IReadOnlyDictionary<string, (string PartNumber, int ColorId)> TryReadElements(string? path)
    {
        var empty = new Dictionary<string, (string, int)>(StringComparer.OrdinalIgnoreCase);

        if (TryFindElementsFile(path) is not { } file)
        {
            return empty;
        }

        try
        {
            var elements = new Dictionary<string, (string, int)>(
                capacity: 100_000, StringComparer.OrdinalIgnoreCase);

            using var reader = new StreamReader(file);

            if (reader.ReadLine() is not { } headerLine)
            {
                return empty;
            }

            var header = SplitCsvLine(headerLine);
            var idIndex = IndexOf(header, "element_id");
            var partIndex = IndexOf(header, "part_num");
            var colorIndex = IndexOf(header, "color_id");

            if (idIndex < 0 || partIndex < 0 || colorIndex < 0)
            {
                return empty;
            }

            var widest = Math.Max(idIndex, Math.Max(partIndex, colorIndex));

            while (reader.ReadLine() is { } line)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                var f = SplitCsvLine(line);
                if (f.Length <= widest || f[idIndex].Length == 0 || f[partIndex].Length == 0)
                {
                    continue;
                }

                if (int.TryParse(f[colorIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var color))
                {
                    // First entry wins: the file lists an element once, and a duplicate would
                    // be a damaged download rather than a choice to make.
                    elements.TryAdd(f[idIndex], (f[partIndex], color));
                }
            }

            return elements;
        }
        catch (IOException)
        {
            return empty;   // optional input: never fail the run because of it
        }
    }

    /// <summary>
    /// The <c>elements.csv</c> a path points at, whether it names the file, the folder holding
    /// it, or a folder holding the folder.
    /// </summary>
    public static string? TryFindElementsFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (File.Exists(path))
        {
            return path;
        }

        if (!Directory.Exists(path))
        {
            return null;
        }

        var here = Path.Combine(path, ElementsFileName);
        if (File.Exists(here))
        {
            return here;
        }

        try
        {
            // One level down, so that a folder holding an unpacked dump next to the documents
            // is found without anyone having to name it.
            return Directory
                .EnumerateDirectories(path)
                .Select(d => Path.Combine(d, ElementsFileName))
                .FirstOrDefault(File.Exists);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static int IndexOf(string[] header, string name) =>
        Array.FindIndex(header, h => h.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Minimal RFC 4180 split: comma separated, double quotes, doubled quotes escape.</summary>
    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return [.. fields];
    }
}

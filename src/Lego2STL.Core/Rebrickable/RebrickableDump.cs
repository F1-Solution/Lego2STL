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

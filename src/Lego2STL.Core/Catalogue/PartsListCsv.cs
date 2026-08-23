using System.Globalization;
using System.Text;
using Lego2STL.Core.Colors;

namespace Lego2STL.Core.Catalogue;

/// <summary>
/// Reads and writes the parts list file.
/// </summary>
/// <remarks>
/// <para>
/// Written with semicolons and a byte-order mark, so that double-clicking it opens correctly
/// in a spreadsheet configured for a locale where the semicolon is the list separator, with
/// accented characters intact. On reading, the separator is detected rather than assumed, so
/// a file edited and re-saved by a spreadsheet still loads whichever separator it chose.
/// </para>
/// <para>
/// Column headings are fixed rather than translated, because this file is also an input:
/// changing the headings with a display language would make yesterday's file unreadable.
/// </para>
/// </remarks>
public static class PartsListCsv
{
    public const char DefaultDelimiter = ';';

    /// <summary>Separators tried when detecting the format of a file being read.</summary>
    private static readonly char[] CandidateDelimiters = [';', ',', '\t'];

    private static readonly string[] Headings =
    [
        "ID",
        "Codice Lego",
        "Codice BrickLink",
        "Nome colore",
        "Codice RGB",
        "Quantita",
    ];

    public static string Write(PartsList list, char delimiter = DefaultDelimiter)
    {
        ArgumentNullException.ThrowIfNull(list);
        return Write(list.Entries, delimiter);
    }

    public static string Write(IEnumerable<PartEntry> entries, char delimiter = DefaultDelimiter)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var sb = new StringBuilder();
        sb.Append(string.Join(delimiter, Headings)).Append("\r\n");

        foreach (var e in entries)
        {
            var fields = new[]
            {
                e.Id.ToString(CultureInfo.InvariantCulture),
                e.PartNumber,
                e.BrickLinkColorCode.ToString(CultureInfo.InvariantCulture),
                e.ColorName,
                e.Rgb.ToString(),
                e.Quantity.ToString(CultureInfo.InvariantCulture),
            };

            sb.Append(string.Join(delimiter, fields.Select(f => Quote(f, delimiter)))).Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>Writes the file, with the byte-order mark a spreadsheet needs to read it as UTF-8.</summary>
    public static async Task WriteFileAsync(
        string path,
        PartsList list,
        char delimiter = DefaultDelimiter,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        await File.WriteAllTextAsync(path, Write(list, delimiter), new UTF8Encoding(true), cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<PartsList> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No such parts list: {path}", path);
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Read(text);
    }

    public static PartsList Read(string content, char? delimiter = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        var lines = content.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0)
            .ToList();

        if (lines.Count == 0)
        {
            throw new FormatException("The parts list is empty.");
        }

        // Strip a byte-order mark if the file has one.
        lines[0] = lines[0].TrimStart('﻿');

        var separator = delimiter ?? DetectDelimiter(lines[0]);
        var startIndex = LooksLikeHeader(lines[0], separator) ? 1 : 0;

        var entries = new List<PartEntry>();
        var notes = new List<string>();

        if (startIndex == 0)
        {
            notes.Add("The file has no heading row; reading it as data.");
        }

        for (var i = startIndex; i < lines.Count; i++)
        {
            entries.Add(ParseRow(SplitRow(lines[i], separator), i + 1, lines[i]));
        }

        if (entries.Count == 0)
        {
            throw new FormatException("The parts list has a heading row but no entries.");
        }

        notes.Add($"Read {entries.Count} entries, separated by '{separator}'.");
        return new PartsList(entries, notes);
    }

    /// <summary>Picks the separator that splits the heading row into the expected number of fields.</summary>
    private static char DetectDelimiter(string headerLine)
    {
        var best = DefaultDelimiter;
        var bestCount = -1;

        foreach (var candidate in CandidateDelimiters)
        {
            var count = SplitRow(headerLine, candidate).Count;
            if (count == Headings.Length)
            {
                return candidate;
            }

            if (count > bestCount)
            {
                bestCount = count;
                best = candidate;
            }
        }

        return best;
    }

    private static bool LooksLikeHeader(string line, char delimiter)
    {
        var fields = SplitRow(line, delimiter);
        return fields.Count > 0 &&
               !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static PartEntry ParseRow(List<string> f, int lineNumber, string line)
    {
        if (f.Count < Headings.Length)
        {
            throw new FormatException(
                $"Line {lineNumber} has {f.Count} fields, expected {Headings.Length}: '{line}'");
        }

        return new PartEntry(
            Id: ParseInt(f[0], lineNumber, "ID"),
            PartNumber: RequireText(f[1], lineNumber, "Codice Lego"),
            BrickLinkColorCode: ParseInt(f[2], lineNumber, "Codice BrickLink"),
            ColorName: f[3],
            Rgb: ParseRgb(f[4], lineNumber),
            Quantity: ParseQuantity(f[5], lineNumber));
    }

    private static Rgb24 ParseRgb(string field, int lineNumber) =>
        Rgb24.TryParse(field, out var rgb)
            ? rgb
            : throw new FormatException($"Line {lineNumber}: '{field}' is not a colour value like #05131D.");

    private static int ParseQuantity(string field, int lineNumber)
    {
        var value = ParseInt(field, lineNumber, "Quantita");
        return value > 0
            ? value
            : throw new FormatException($"Line {lineNumber}: a quantity must be greater than zero, found {value}.");
    }

    private static int ParseInt(string field, int lineNumber, string column) =>
        int.TryParse(field.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException($"Line {lineNumber}: '{field}' is not a number in column {column}.");

    private static string RequireText(string field, int lineNumber, string column)
    {
        var text = field.Trim();
        return text.Length > 0
            ? text
            : throw new FormatException($"Line {lineNumber}: column {column} is empty.");
    }

    /// <summary>Splits one row, honouring quoted fields and doubled quotes.</summary>
    private static List<string> SplitRow(string line, char delimiter)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
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
            else if (c == '"' && current.Length == 0)
            {
                inQuotes = true;
            }
            else if (c == delimiter)
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
        return fields;
    }

    private static string Quote(string field, char delimiter)
    {
        var needsQuotes = field.Contains(delimiter) ||
                          field.Contains('"') ||
                          field.Contains('\n') ||
                          field.Contains('\r');

        return needsQuotes ? '"' + field.Replace("\"", "\"\"") + '"' : field;
    }
}

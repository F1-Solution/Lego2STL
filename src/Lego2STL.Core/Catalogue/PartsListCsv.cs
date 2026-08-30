using System.Globalization;
using System.Text;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Text;

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
/// Column headings and colour names follow the display language, so the file reads naturally
/// to whoever opens it. Reading does not depend on that: every wording the tool has ever
/// written is recognised, in any language, and the columns are positional anyway, so a file
/// written in one language loads perfectly well in another.
/// </para>
/// </remarks>
public static class PartsListCsv
{
    public const char DefaultDelimiter = ';';

    /// <summary>The column order, once and for all. Only the wording changes with language.</summary>
    private static readonly TextKey[] Columns =
    [
        TextKey.CsvId,
        TextKey.CsvLegoCode,
        TextKey.CsvBrickLinkCode,
        TextKey.CsvColourName,
        TextKey.CsvRgb,
        TextKey.CsvQuantity,
    ];

    public static int ColumnCount => Columns.Length;

    /// <summary>Separators tried when detecting the format of a file being read.</summary>
    private static readonly char[] CandidateDelimiters = [';', ',', '\t'];

    /// <summary>
    /// Heading rows recognised when reading: the columns as worded in every language the tool
    /// speaks. A parts list is an input as well as an output, so a file written on an Italian
    /// machine has to load on an English one and the other way round.
    /// </summary>
    private static readonly string[][] KnownHeadings =
        [.. DisplayLanguages.All.Select(HeadingsFor)];

    /// <summary>The column names in one language.</summary>
    public static string[] HeadingsFor(DisplayLanguage language)
    {
        var words = Strings.For(language);
        return [.. Columns.Select(c => words[c])];
    }

    public static string Write(
        PartsList list,
        char delimiter = DefaultDelimiter,
        DisplayLanguage language = DisplayLanguages.Fallback)
    {
        ArgumentNullException.ThrowIfNull(list);
        return Write(list.Entries, delimiter, language);
    }

    public static string Write(
        IEnumerable<PartEntry> entries,
        char delimiter = DefaultDelimiter,
        DisplayLanguage language = DisplayLanguages.Fallback)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var sb = new StringBuilder();
        sb.Append(string.Join(delimiter, HeadingsFor(language))).Append("\r\n");

        foreach (var e in entries)
        {
            var fields = new[]
            {
                e.Id.ToString(CultureInfo.InvariantCulture),
                e.PartNumber,
                e.BrickLinkColorCode.ToString(CultureInfo.InvariantCulture),
                ColorNames.For(language, e.ColorName),
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
        DisplayLanguage language = DisplayLanguages.Fallback,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        await File.WriteAllTextAsync(
                path,
                Write(list, delimiter, language),
                new UTF8Encoding(true),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>The same file, written where waiting is not on offer - inside a button.</summary>
    public static void WriteFile(
        string path,
        PartsList list,
        char delimiter = DefaultDelimiter,
        DisplayLanguage language = DisplayLanguages.Fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, Write(list, delimiter, language), new UTF8Encoding(true));
    }

    public static async Task<PartsList> ReadFileAsync(
        string path,
        CancellationToken cancellationToken = default,
        DisplayLanguage language = DisplayLanguages.Fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No such parts list: {path}", path);
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Read(text, null, language);
    }

    public static PartsList Read(
        string content,
        char? delimiter = null,
        DisplayLanguage language = DisplayLanguages.Fallback)
    {
        ArgumentNullException.ThrowIfNull(content);

        var words = Strings.For(language);

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
            notes.Add(words[TextKey.NoteNoHeadingRow]);
        }

        for (var i = startIndex; i < lines.Count; i++)
        {
            entries.Add(ParseRow(SplitRow(lines[i], separator), i + 1, lines[i]));
        }

        if (entries.Count == 0)
        {
            throw new FormatException("The parts list has a heading row but no entries.");
        }

        notes.Add(words.Format(TextKey.NoteRead, entries.Count, separator));
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
            if (count == ColumnCount)
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

    /// <summary>
    /// True when the first line names the columns rather than holding data. A heading row in
    /// any language the tool speaks settles it outright; otherwise anything that does not start
    /// with a number is a heading, which covers a file whose headings a spreadsheet has renamed.
    /// </summary>
    private static bool LooksLikeHeader(string line, char delimiter)
    {
        var fields = SplitRow(line, delimiter);

        if (fields.Count == 0)
        {
            return false;
        }

        if (KnownHeadings.Any(known => known.SequenceEqual(fields.Select(f => f.Trim()), StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        return !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static PartEntry ParseRow(List<string> f, int lineNumber, string line)
    {
        if (f.Count < ColumnCount)
        {
            throw new FormatException(
                $"Line {lineNumber} has {f.Count} fields, expected {ColumnCount}: '{line}'");
        }

        return new PartEntry(
            Id: ParseInt(f[0], lineNumber, "ID"),
            PartNumber: RequireText(f[1], lineNumber, Strings.English[TextKey.CsvLegoCode]),
            BrickLinkColorCode: ParseInt(f[2], lineNumber, Strings.English[TextKey.CsvBrickLinkCode]),
            // Back to the name the tool stores, whichever language the file was written in.
            ColorName: ColorNames.ToCanonical(f[3]),
            Rgb: ParseRgb(f[4], lineNumber),
            Quantity: ParseQuantity(f[5], lineNumber));
    }

    private static Rgb24 ParseRgb(string field, int lineNumber) =>
        Rgb24.TryParse(field, out var rgb)
            ? rgb
            : throw new FormatException($"Line {lineNumber}: '{field}' is not a colour value like #05131D.");

    private static int ParseQuantity(string field, int lineNumber)
    {
        var value = ParseInt(field, lineNumber, Strings.English[TextKey.CsvQuantity]);
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

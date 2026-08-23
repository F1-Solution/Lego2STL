using System.Globalization;
using System.Text;

namespace Lego2STL.Core.Colors;

/// <summary>
/// Reads and writes the vendored colour cross-reference file.
/// </summary>
/// <remarks>
/// <para>
/// Generated once from the Rebrickable API (see <see cref="ColorTableBuilder"/>) and shipped
/// inside the tool, so a normal run needs no API key, no network and no HTML scraping, and
/// gives byte-identical results every time. LEGO retires or adds a colour at most once a
/// year, and an unknown code is reported rather than guessed, so a slightly stale table
/// cannot produce a wrong answer.
/// </para>
/// <para>
/// Two record types share the file: <c>C</c> lines describe a colour, <c>M</c> lines are the
/// reverse map. Keeping the reverse map explicit means the runtime never has to re-derive
/// which of two colours a contested code belongs to.
/// </para>
/// </remarks>
public static class ColorTableCsv
{
    private const char Delimiter = ';';

    private const string ColorHeader =
        "C;rebrickable_id;name;rgb;is_trans;bricklink_id;lego_id;ldraw_id;part_count";

    private const int ColorFieldCount = 9;
    private const int MappingFieldCount = 4;

    public static string Write(IEnumerable<LegoColor> colors, IEnumerable<ColorCodeMapping> mappings)
    {
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(mappings);

        var sb = new StringBuilder();
        sb.Append("# Lego2STL colour cross-reference. Generated from the Rebrickable API; do not hand-edit.\n");
        sb.Append("# Regenerate with: lego2stl refresh-colors\n");
        sb.Append("# C lines are colours. M lines are the reverse map: scheme;code;rebrickable_id.\n");
        sb.Append(ColorHeader).Append('\n');

        foreach (var c in colors.OrderBy(c => c.RebrickableId))
        {
            if (c.Name.Contains(Delimiter))
            {
                throw new InvalidOperationException(
                    $"Colour name '{c.Name}' contains the '{Delimiter}' delimiter; the reference format cannot hold it.");
            }

            sb.Append("C").Append(Delimiter)
              .Append(Int(c.RebrickableId)).Append(Delimiter)
              .Append(c.Name).Append(Delimiter)
              .Append(c.Rgb.ToString()).Append(Delimiter)
              .Append(c.IsTranslucent ? '1' : '0').Append(Delimiter)
              .Append(Optional(c.BrickLinkId)).Append(Delimiter)
              .Append(Optional(c.LegoId)).Append(Delimiter)
              .Append(Optional(c.LDrawId)).Append(Delimiter)
              .Append(Int(c.PartCount))
              .Append('\n');
        }

        foreach (var m in mappings
                     .OrderBy(m => m.Scheme)
                     .ThenBy(m => m.Code))
        {
            sb.Append("M").Append(Delimiter)
              .Append(m.Scheme.ToString()).Append(Delimiter)
              .Append(Int(m.Code)).Append(Delimiter)
              .Append(Int(m.RebrickableId))
              .Append('\n');
        }

        return sb.ToString();
    }

    public static ColorTable Read(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var colors = new List<LegoColor>();
        var mappings = new List<ColorCodeMapping>();
        var lines = content.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var lineNumber = i + 1;

            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("C;rebrickable_id", StringComparison.Ordinal))
            {
                continue;
            }

            var f = line.Split(Delimiter);
            switch (f[0])
            {
                case "C":
                    colors.Add(ReadColor(f, line, lineNumber));
                    break;

                case "M":
                    mappings.Add(ReadMapping(f, line, lineNumber));
                    break;

                default:
                    throw new FormatException(
                        $"Colour reference line {lineNumber} starts with '{f[0]}'; expected 'C' or 'M': '{line}'");
            }
        }

        if (colors.Count == 0)
        {
            throw new FormatException("Colour reference contains no colours.");
        }

        return ColorTable.Create(colors, mappings);
    }

    private static LegoColor ReadColor(string[] f, string line, int lineNumber)
    {
        if (f.Length != ColorFieldCount)
        {
            throw new FormatException(
                $"Colour reference line {lineNumber} has {f.Length} fields, expected {ColorFieldCount}: '{line}'");
        }

        return new LegoColor(
            RebrickableId: ParseInt(f[1], lineNumber, "rebrickable_id"),
            Name: f[2],
            Rgb: Rgb24.Parse(f[3]),
            IsTranslucent: f[4] == "1",
            BrickLinkId: ParseOptional(f[5], lineNumber, "bricklink_id"),
            LegoId: ParseOptional(f[6], lineNumber, "lego_id"),
            LDrawId: ParseOptional(f[7], lineNumber, "ldraw_id"),
            PartCount: ParseInt(f[8], lineNumber, "part_count"));
    }

    private static ColorCodeMapping ReadMapping(string[] f, string line, int lineNumber)
    {
        if (f.Length != MappingFieldCount)
        {
            throw new FormatException(
                $"Colour reference line {lineNumber} has {f.Length} fields, expected {MappingFieldCount}: '{line}'");
        }

        if (!Enum.TryParse<ColorScheme>(f[1], ignoreCase: true, out var scheme))
        {
            throw new FormatException($"Colour reference line {lineNumber}: '{f[1]}' is not a known colour scheme.");
        }

        return new ColorCodeMapping(
            scheme,
            ParseInt(f[2], lineNumber, "code"),
            ParseInt(f[3], lineNumber, "rebrickable_id"));
    }

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Optional(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static int ParseInt(string s, int line, string field) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : throw new FormatException($"Colour reference line {line}: '{s}' is not a valid {field}.");

    private static int? ParseOptional(string s, int line, string field) =>
        s.Length == 0 ? null : ParseInt(s, line, field);
}

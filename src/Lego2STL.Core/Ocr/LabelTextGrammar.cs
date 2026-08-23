using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Lego2STL.Core.Ocr;

/// <summary>
/// Turns the recogniser's output for one line into structured values, repairing the
/// confusions it actually makes.
/// </summary>
/// <remarks>
/// <para>
/// A catalogue entry's two lines have rigid shapes - a count followed by "x", and a part
/// number followed by a comma and a colour number - so each line can be read against the
/// shape it is required to have instead of accepted as free text. That is what makes the
/// repairs safe: a substitution is only applied where the shape demands a digit.
/// </para>
/// <para>
/// The substitutions are not guesses. Reading every label of the reference document
/// produced exactly one recurring class of error: the digit one coming back as a letter,
/// giving "Ix", "lx", "IOX" and "1 ox" in place of "1x" and "10x". Nothing else was
/// misread once the crops were given a white border.
/// </para>
/// </remarks>
public static class LabelTextGrammar
{
    /// <summary>
    /// Characters the recogniser returns in place of a digit, and what they should be.
    /// Applied only where the line's shape requires a digit.
    /// </summary>
    private static readonly Dictionary<char, char> DigitConfusions = new()
    {
        ['I'] = '1',
        ['i'] = '1',
        ['l'] = '1',
        ['|'] = '1',
        ['!'] = '1',
        ['O'] = '0',
        ['o'] = '0',
        ['Q'] = '0',
        ['D'] = '0',
        ['S'] = '5',
        ['s'] = '5',
        ['B'] = '8',
        ['Z'] = '2',
        ['z'] = '2',
    };

    /// <summary>Characters that can stand in for a digit, as a regex character class.</summary>
    private const string DigitLike = @"[0-9IilOoQDSsBZz|!]";

    /// <summary>A quantity: digit-like characters followed by an x.</summary>
    private static readonly Regex QuantityPattern =
        new(DigitLike + @"{1,4}\s*[xX]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>A part number and colour: digit-like characters, an optional letter suffix, a comma, then a colour.</summary>
    private static readonly Regex PartAndColorPattern =
        new(DigitLike + @"{2,8}[A-Za-z]{0,3}\s*,\s*" + DigitLike + @"{1,3}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Finds the quantity and the part-and-colour anywhere in a block of recognised text.
    /// </summary>
    /// <remarks>
    /// Scanning rather than splitting into lines, because how the recogniser divides its
    /// output is not something to depend on: the same entry can come back as two lines, as
    /// one line joined by a space, or with the pieces separated differently again. Searching
    /// for the two shapes that a catalogue entry must contain works whichever way it arrives.
    /// </remarks>
    public static (int? Quantity, string? PartNumber, int? ColorCode) Scan(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (null, null, null);
        }

        string? partNumber = null;
        int? colorCode = null;

        // The part line first: its match would otherwise swallow the quantity's digits.
        foreach (var match in PartAndColorPattern.Matches(text).Cast<Match>())
        {
            if (TryReadPartAndColor(match.Value, out var p, out var c, out _))
            {
                partNumber = p;
                colorCode = c;
                break;
            }
        }

        int? quantity = null;
        foreach (var match in QuantityPattern.Matches(text).Cast<Match>())
        {
            // Skip a match that is really part of the part-and-colour text.
            if (partNumber is not null && match.Index > 0 && text[match.Index - 1] == ',')
            {
                continue;
            }

            if (TryReadQuantity(match.Value, out var q, out _))
            {
                quantity = q;
                break;
            }
        }

        return (quantity, partNumber, colorCode);
    }

    /// <summary>
    /// Reads a quantity line: some digits then an "x", e.g. "1x", "15x", "38x".
    /// </summary>
    public static bool TryReadQuantity(string? raw, out int quantity, out string repaired)
    {
        quantity = 0;
        repaired = "";

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        // The recogniser sometimes splits "10x" as "1 ox", so spaces carry no meaning here.
        var compact = Compact(raw);

        if (compact.Length < 2)
        {
            return false;
        }

        // The trailing marker must be an x. It is the one letter the shape allows, so a
        // digit confusion cannot be applied to it.
        var last = compact[^1];
        if (last is not ('x' or 'X'))
        {
            return false;
        }

        var digits = RepairDigits(compact[..^1]);
        if (digits is null)
        {
            return false;
        }

        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out quantity) || quantity <= 0)
        {
            return false;
        }

        repaired = digits + "x";
        return true;
    }

    /// <summary>
    /// Reads a part line: a part number, a comma, then a colour number, e.g. "32525, 11"
    /// or "4265c, 9".
    /// </summary>
    /// <remarks>
    /// Part numbers are digits with an optional short letter suffix ("4265c", "3068b",
    /// "32123a"), so digit repair is applied to the numeric part and the suffix is left
    /// alone. Doing otherwise would turn a real suffix into a digit.
    /// </remarks>
    public static bool TryReadPartAndColor(
        string? raw,
        out string partNumber,
        out int colorCode,
        out string repaired)
    {
        partNumber = "";
        colorCode = 0;
        repaired = "";

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var compact = Compact(raw);

        var comma = compact.LastIndexOf(',');
        if (comma <= 0 || comma == compact.Length - 1)
        {
            return false;
        }

        var partText = compact[..comma];
        var colorText = RepairDigits(compact[(comma + 1)..]);

        if (colorText is null ||
            !int.TryParse(colorText, NumberStyles.None, CultureInfo.InvariantCulture, out colorCode) ||
            colorCode < 0)
        {
            return false;
        }

        if (!TryReadPartNumber(partText, out partNumber))
        {
            return false;
        }

        repaired = $"{partNumber}, {colorCode.ToString(CultureInfo.InvariantCulture)}";
        return true;
    }

    /// <summary>
    /// Reads a part number: digits, optionally followed by up to three letters.
    /// </summary>
    public static bool TryReadPartNumber(string? raw, out string partNumber)
    {
        partNumber = "";

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var compact = Compact(raw);

        // Split into a leading numeric run and a trailing letter suffix.
        var split = compact.Length;
        for (var i = 0; i < compact.Length; i++)
        {
            if (char.IsAsciiLetter(compact[i]) && IsSuffixOnly(compact, i))
            {
                split = i;
                break;
            }
        }

        var digits = RepairDigits(compact[..split]);
        var suffix = compact[split..];

        if (digits is null || digits.Length == 0)
        {
            return false;
        }

        if (suffix.Length > 3 || !suffix.All(char.IsAsciiLetter))
        {
            return false;
        }

        partNumber = digits + suffix.ToLowerInvariant();
        return true;
    }

    /// <summary>
    /// True when everything from <paramref name="index"/> onwards is letters, i.e. this is
    /// where the suffix starts rather than a letter standing in for a digit.
    /// </summary>
    private static bool IsSuffixOnly(string text, int index)
    {
        for (var i = index; i < text.Length; i++)
        {
            if (!char.IsAsciiLetter(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Applies the digit substitutions, returning null when a character cannot be a digit
    /// at all - which is how a genuinely unreadable line is reported rather than mangled.
    /// </summary>
    private static string? RepairDigits(string text)
    {
        if (text.Length == 0)
        {
            return null;
        }

        var sb = new StringBuilder(text.Length);

        foreach (var c in text)
        {
            if (char.IsAsciiDigit(c))
            {
                sb.Append(c);
            }
            else if (DigitConfusions.TryGetValue(c, out var digit))
            {
                sb.Append(digit);
            }
            else
            {
                return null;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Removes whitespace and the punctuation the recogniser sprinkles in. Commas are kept,
    /// because a comma is part of the part line's shape.
    /// </summary>
    private static string Compact(string raw)
    {
        var sb = new StringBuilder(raw.Length);

        foreach (var c in raw)
        {
            if (char.IsWhiteSpace(c) || c is '.' or '\'' or '"' or '`' or '_')
            {
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }
}

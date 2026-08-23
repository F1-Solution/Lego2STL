using System.Globalization;

namespace Lego2STL.Core.Pdf;

/// <summary>
/// Parses a page range such as <c>2-5</c>, <c>2-5,8,11-13</c> or <c>3</c>.
/// </summary>
/// <remarks>
/// Page numbers are 1-based positions in the file, inclusive at both ends. There is no
/// other sensible reading: instruction PDFs of this kind carry no printed page numbers.
/// Overlapping and repeated pages are collapsed, so <c>2-5,3</c> costs no extra work, and
/// a page outside the document is an error naming the real page count rather than a
/// silent clamp.
/// </remarks>
public static class PageRange
{
    /// <summary>Parses a range, returning ascending distinct 1-based page numbers.</summary>
    /// <param name="text">The range expression.</param>
    /// <param name="pageCount">
    /// The document's page count, used to reject out-of-range pages. Pass null to skip that
    /// check when the document is not open yet.
    /// </param>
    public static IReadOnlyList<int> Parse(string text, int? pageCount = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new FormatException(
                "The page range is empty. Give something like '2-5', or '2-5,8,11-13'.");
        }

        var pages = new SortedSet<int>();

        foreach (var rawPart in text.Split(',', StringSplitOptions.TrimEntries))
        {
            if (rawPart.Length == 0)
            {
                throw new FormatException(
                    $"'{text}' has an empty section. Separate ranges with a single comma, e.g. '2-5,8'.");
            }

            var (first, last) = ParsePart(rawPart, text);

            for (var page = first; page <= last; page++)
            {
                pages.Add(page);
            }
        }

        if (pageCount is { } count)
        {
            var outside = pages.Where(p => p > count).ToList();
            if (outside.Count > 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(text),
                    $"The range asks for page {string.Join(", ", outside.Take(5))}" +
                    (outside.Count > 5 ? ", ..." : "") +
                    $", but the document has only {count} page{(count == 1 ? "" : "s")}.");
            }
        }

        return [.. pages];
    }

    private static (int First, int Last) ParsePart(string part, string whole)
    {
        var dash = part.IndexOf('-', StringComparison.Ordinal);

        if (dash < 0)
        {
            var single = ParsePageNumber(part, whole);
            return (single, single);
        }

        // A dash at either end means a missing bound: "-5" or "2-".
        if (dash == 0 || dash == part.Length - 1)
        {
            throw new FormatException(
                $"'{part}' in '{whole}' is missing one end of the range. " +
                "Write both ends, e.g. '2-5'. Open-ended ranges are not supported.");
        }

        var first = ParsePageNumber(part[..dash].Trim(), whole);
        var last = ParsePageNumber(part[(dash + 1)..].Trim(), whole);

        if (last < first)
        {
            throw new FormatException(
                $"'{part}' in '{whole}' runs backwards: {first} to {last}. Write it as '{last}-{first}'.");
        }

        return (first, last);
    }

    private static int ParsePageNumber(string token, string whole)
    {
        if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var page))
        {
            throw new FormatException(
                $"'{token}' in '{whole}' is not a page number. Use digits, e.g. '2-5,8'.");
        }

        if (page < 1)
        {
            throw new FormatException(
                $"Page numbers start at 1, so '{token}' in '{whole}' is not valid.");
        }

        return page;
    }

    /// <summary>
    /// Renders pages back into the shortest equivalent expression, collapsing runs.
    /// Used when reporting what was actually read, and when echoing a detected range.
    /// </summary>
    public static string Format(IEnumerable<int> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        var sorted = new SortedSet<int>(pages).ToList();
        if (sorted.Count == 0)
        {
            return "";
        }

        var parts = new List<string>();
        var runStart = sorted[0];
        var previous = sorted[0];

        foreach (var page in sorted.Skip(1))
        {
            if (page == previous + 1)
            {
                previous = page;
                continue;
            }

            parts.Add(FormatRun(runStart, previous));
            runStart = page;
            previous = page;
        }

        parts.Add(FormatRun(runStart, previous));
        return string.Join(',', parts);
    }

    private static string FormatRun(int first, int last) =>
        first == last
            ? first.ToString(CultureInfo.InvariantCulture)
            : $"{first}-{last}";
}

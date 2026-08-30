using System.Globalization;
using System.Text;

namespace Lego2STL.Core.Run;

/// <summary>
/// What a person said about a region the reader could not make out.
/// </summary>
/// <param name="Bounds">The region on the page, worded as the record words it.</param>
/// <param name="NotAnEntry">
/// True when the region was never a label at all, which is an answer about the page rather
/// than about a part: nothing is added to the list, and the question is never asked again.
/// </param>
public sealed record ReviewAnswer(
    int Page,
    string Bounds,
    string? PartNumber,
    int? ColorCode,
    int? Quantity,
    bool NotAnEntry);

/// <summary>
/// The answers file kept in a run's own folder.
/// </summary>
/// <remarks>
/// <para>
/// Appended to rather than rewritten, so an answer is on the disk the moment it is given and a
/// window closed mid-review loses nothing. Reading keeps the last answer for each region, which
/// is what makes answering twice a correction instead of a duplicate.
/// </para>
/// <para>
/// Reading never throws. A file someone has edited by hand into nonsense costs the answers it
/// held and nothing else; the run beside it is untouched, and the questions are simply asked
/// again.
/// </para>
/// </remarks>
public static class ReviewAnswers
{
    /// <summary>Semicolons, as everywhere else here, so a region's commas need no quoting.</summary>
    private const char Delimiter = ';';

    private const string Header = "page;bounds;part;color;quantity;notAnEntry";

    private const int ColumnCount = 6;

    /// <summary>What makes two answers answers about the same question.</summary>
    public static string Key(int page, string bounds) =>
        FormattableString.Invariant($"{page}|{bounds}");

    /// <summary>The answers a folder holds, the last one given about each region.</summary>
    public static IReadOnlyList<ReviewAnswer> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string[] lines;

        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            lines = File.ReadAllLines(path);
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or NotSupportedException)
        {
            return [];
        }

        var byKey = new Dictionary<string, int>(StringComparer.Ordinal);
        var kept = new List<ReviewAnswer>();

        foreach (var line in lines)
        {
            if (Parse(line) is not { } answer)
            {
                continue;
            }

            var key = Key(answer.Page, answer.Bounds);

            if (byKey.TryGetValue(key, out var at))
            {
                kept[at] = answer;
            }
            else
            {
                byKey[key] = kept.Count;
                kept.Add(answer);
            }
        }

        return kept;
    }

    /// <summary>Adds one answer, making the file if it is not there yet.</summary>
    public static void Append(string path, ReviewAnswer answer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(answer);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        var text = new StringBuilder();

        if (!File.Exists(path))
        {
            text.Append(Header).Append("\r\n");
        }

        text.Append(Row(answer)).Append("\r\n");

        File.AppendAllText(path, text.ToString(), new UTF8Encoding(true));
    }

    private static string Row(ReviewAnswer answer) => string.Join(
        Delimiter,
        [
            answer.Page.ToString(CultureInfo.InvariantCulture),
            answer.Bounds,
            answer.PartNumber ?? string.Empty,
            Number(answer.ColorCode),
            Number(answer.Quantity),
            answer.NotAnEntry ? "true" : "false",
        ]);

    private static string Number(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>One row, or null for the heading and for anything that is not a row.</summary>
    private static ReviewAnswer? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var fields = line.Split(Delimiter);

        if (fields.Length != ColumnCount
            || !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var page)
            || string.IsNullOrWhiteSpace(fields[1]))
        {
            return null;
        }

        return new ReviewAnswer(
            page,
            fields[1].Trim(),
            fields[2].Length == 0 ? null : fields[2].Trim(),
            Optional(fields[3]),
            Optional(fields[4]),
            bool.TryParse(fields[5].Trim(), out var notAnEntry) && notAnEntry);
    }

    private static int? Optional(string field) =>
        int.TryParse(field, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}

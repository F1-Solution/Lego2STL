using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lego2STL.Core.Extraction;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace Lego2STL.Core.Pdf;

/// <summary>One catalogue entry exactly as the document prints it.</summary>
/// <param name="Quantity">How many, from the line reading "7x".</param>
/// <param name="ElementId">
/// The number under it. Official instructions print an element number - a moulding and a
/// colour in one, e.g. 6177114 - rather than a design number and a colour code, so this is
/// not a part number yet. <see cref="Catalogue.ElementLookup"/> turns it into one.
/// </param>
/// <param name="Bounds">Where it sits on the page, for naming an entry that could not be resolved.</param>
public sealed record PrintedEntry(int Quantity, string ElementId, PixelBounds Bounds)
{
    public override string ToString() => $"{Quantity}x {ElementId}";
}

/// <summary>What a page's text layer holds.</summary>
/// <param name="HasText">
/// Whether the page carries any text at all. A document whose pages all say no is a scan or
/// an image-only export, and only its pixels can be read; a document whose pages say yes has
/// already answered where its catalogue is, including when the answer is nowhere.
/// </param>
/// <param name="Entries">The catalogue entries printed on it, in reading order.</param>
public sealed record PageText(bool HasText, IReadOnlyList<PrintedEntry> Entries);

/// <summary>
/// Reads a parts catalogue out of a page's text, without looking at the pixels at all.
/// </summary>
/// <remarks>
/// <para>
/// Official LEGO building instructions carry a real text layer, and their catalogue pages are
/// laid out to a rule that never varies: a count ending in "x" with the element number printed
/// directly beneath it, left edges aligned to the same point and in the same size. Two words
/// standing in that relationship are an entry; nothing else on the page is. That is exact
/// where recognising the shapes of the characters is merely accurate, it costs no rasterising,
/// and it works on a build that has no text recogniser at all.
/// </para>
/// <para>
/// The same rule is what tells a catalogue page from a building step. Steps print counts too -
/// "2x" beside the pieces that step adds - but never a number beneath one, so a page's entry
/// count is a reliable answer to "is this the catalogue". Measured on a 372-page instruction
/// book: the two real catalogue pages yield 114 and 109 entries, and all 370 other pages yield
/// none, including the ones carrying a dozen step counts.
/// </para>
/// </remarks>
internal static partial class PrintedCatalogue
{
    /// <summary>A count: up to four digits and an "x".</summary>
    [GeneratedRegex(@"^([0-9]{1,4})[xX]$", RegexOptions.CultureInvariant)]
    private static partial Regex QuantityPattern { get; }

    /// <summary>
    /// An element number: five to eight digits, with the optional letter suffix Rebrickable
    /// uses for mould variants.
    /// </summary>
    /// <remarks>
    /// Five at the low end excludes the small circled numbers printed beside beams and axles,
    /// which give a length in studs and run to two digits. Element numbers have been six
    /// digits since the sixties and seven since the two-thousands.
    /// </remarks>
    [GeneratedRegex(@"^[0-9]{5,8}[a-z]?$", RegexOptions.CultureInvariant)]
    private static partial Regex ElementPattern { get; }

    /// <summary>
    /// How far the two lines' left edges may differ. Measured: they agree to within a
    /// thousandth of a point, being set from the same origin, so this is slack rather than
    /// tolerance.
    /// </summary>
    private const double LeftEdgeTolerance = 1.5;

    /// <summary>
    /// The vertical gap allowed between the count and the number under it, as a fraction of
    /// the type size. Measured: consistently half the type size, and the next entry up the
    /// column is more than three times it away.
    /// </summary>
    private const double MaxLineGapInEms = 1.2;

    /// <summary>Slack for a gap that measures very slightly negative because the glyphs touch.</summary>
    private const double MinLineGap = -1.0;

    /// <summary>Type sizes further apart than this are not two lines of one entry.</summary>
    private const double SizeTolerance = 0.5;

    /// <summary>
    /// Every entry on one page, in reading order.
    /// </summary>
    public static IReadOnlyList<PrintedEntry> Read(Page page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var words = page.GetWords()
            .Select(word => (Word: word, Text: WithoutOverprint(word)))
            .ToList();

        var counts = words.Where(w => QuantityPattern.IsMatch(w.Text)).ToList();
        var numbers = words.Where(w => ElementPattern.IsMatch(w.Text)).ToList();

        var entries = new List<PrintedEntry>();

        foreach (var count in counts)
        {
            if (NumberUnder(count, numbers) is not { } number)
            {
                continue;
            }

            var quantity = int.Parse(
                QuantityPattern.Match(count.Text).Groups[1].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture);

            if (quantity <= 0)
            {
                continue;
            }

            entries.Add(new PrintedEntry(
                quantity,
                number.Text,
                Bounds(count.Word.BoundingBox, number.Word.BoundingBox, page.Height)));
        }

        // Reading order: down each column, then across. Top-to-bottom within a column is what
        // makes the parts list come out in the order the page presents it.
        return
        [
            .. entries
                .OrderBy(e => e.Bounds.Left)
                .ThenBy(e => e.Bounds.Top),
        ];
    }

    /// <summary>How many entries a page holds, which is what makes it a catalogue page or not.</summary>
    public static int Count(Page page) => Read(page).Count;

    /// <summary>
    /// The element number belonging to a count: the nearest one directly beneath it, in the
    /// same size and starting at the same left edge.
    /// </summary>
    private static (Word Word, string Text)? NumberUnder(
        (Word Word, string Text) count,
        List<(Word Word, string Text)> numbers)
    {
        var size = count.Word.Letters[0].PointSize;

        (Word Word, string Text)? best = null;
        var bestGap = double.MaxValue;

        foreach (var number in numbers)
        {
            if (Math.Abs(number.Word.Letters[0].PointSize - size) > SizeTolerance)
            {
                continue;
            }

            if (Math.Abs(number.Word.BoundingBox.Left - count.Word.BoundingBox.Left) > LeftEdgeTolerance)
            {
                continue;
            }

            // PDF coordinates run up the page, so "beneath" is the count's bottom above the
            // number's top.
            var gap = count.Word.BoundingBox.Bottom - number.Word.BoundingBox.Top;

            if (gap < MinLineGap || gap > size * MaxLineGapInEms || gap >= bestGap)
            {
                continue;
            }

            best = number;
            bestGap = gap;
        }

        return best;
    }

    /// <summary>
    /// A word's text with overprinted characters removed.
    /// </summary>
    /// <remarks>
    /// These documents draw some entries twice, one impression exactly on top of the other, so
    /// the text layer hands back "66221188220099" for 6218209 and "55xx" for 5x. Dropping a
    /// character that repeats one already placed at the same position is what puts those back
    /// together, and it cannot damage a genuine repeat: two characters of one word never
    /// occupy the same place. Nine of the reference document's 223 entries need this, and
    /// without it they are silently lost.
    /// </remarks>
    private static string WithoutOverprint(Word word)
    {
        var placed = new List<(string Value, double Left, double Right)>(word.Letters.Count);
        var text = new StringBuilder(word.Text.Length);

        foreach (var letter in word.Letters)
        {
            var box = letter.BoundingBox;

            if (placed.Any(p => p.Value == letter.Value
                                && Math.Abs(p.Left - box.Left) < OverprintTolerance
                                && Math.Abs(p.Right - box.Right) < OverprintTolerance))
            {
                continue;
            }

            text.Append(letter.Value);
            placed.Add((letter.Value, box.Left, box.Right));
        }

        return text.ToString();
    }

    /// <summary>How close two impressions of one character sit. Measured: identical to 0.01 pt.</summary>
    private const double OverprintTolerance = 0.2;

    /// <summary>
    /// Where the entry is, in the pixels a rendered page would have.
    /// </summary>
    /// <remarks>
    /// Reported in the same units as the pixel-reading path so that "page 370 at (444,530)"
    /// means one thing wherever it came from. PDF points run up the page from the bottom left
    /// and pixels run down from the top left, hence the flip.
    /// </remarks>
    private static PixelBounds Bounds(PdfRectangle count, PdfRectangle number, double pageHeight)
    {
        const double scale = PdfPageImageSource.FallbackRenderDpi / 72.0;

        var left = Math.Min(count.Left, number.Left);
        var right = Math.Max(count.Right, number.Right);
        var topPoints = Math.Max(count.Top, number.Top);
        var bottomPoints = Math.Min(count.Bottom, number.Bottom);

        return new PixelBounds(
            (int)Math.Floor(left * scale),
            (int)Math.Floor((pageHeight - topPoints) * scale),
            (int)Math.Ceiling(right * scale),
            (int)Math.Ceiling((pageHeight - bottomPoints) * scale));
    }
}

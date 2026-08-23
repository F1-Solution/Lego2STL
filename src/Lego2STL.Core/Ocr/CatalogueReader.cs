using Lego2STL.Core.Extraction;
using Lego2STL.Core.Pdf;
using Lego2STL.Core.Text;

namespace Lego2STL.Core.Ocr;

/// <summary>How a value was arrived at, so the report can say where each number came from.</summary>
public enum ReadingSource
{
    /// <summary>The system text recogniser read it directly.</summary>
    TextRecogniser,

    /// <summary>The character shapes learned from this document read it.</summary>
    LearnedShapes,
}

/// <summary>One catalogue entry, read.</summary>
public sealed record CatalogueReading(
    int Page,
    PixelBounds Bounds,
    int Quantity,
    string PartNumber,
    int ColorCode,
    ReadingSource QuantitySource,
    ReadingSource PartSource)
{
    public override string ToString() => $"p{Page} {Quantity}x {PartNumber},{ColorCode}";
}

/// <summary>An entry that could not be read, with enough context to ask the user about it.</summary>
public sealed record UnresolvedReading(
    int Page,
    PixelBounds Bounds,
    string RawText,
    int? Quantity,
    string? PartNumber,
    int? ColorCode,
    string Reason);

/// <summary>Everything one run of the reader produced.</summary>
public sealed record CatalogueReadResult(
    IReadOnlyList<CatalogueReading> Entries,
    IReadOnlyList<UnresolvedReading> Unresolved,
    IReadOnlyList<string> Notes)
{
    public bool IsComplete => Unresolved.Count == 0;
}

/// <summary>
/// Reads a whole catalogue: locate the entries, read what can be read, then use the
/// document's own lettering to fill in what the recogniser missed.
/// </summary>
/// <remarks>
/// Two passes, because the second depends on the first. The first pass reads every entry
/// and collects the shape of every character. Part lines come back reliably, and each one
/// is a set of labelled examples of that document's digits. The second pass uses those
/// examples to read the short quantity lines the recogniser declines to attempt, and to
/// check the part lines it did read.
/// </remarks>
public sealed class CatalogueReader
{
    private const string Digits = "0123456789";

    private readonly IOcrEngine _engine;
    private readonly LabelLocator _locator;
    private readonly LabelLocatorOptions _locatorOptions;

    private readonly Strings _words;

    public CatalogueReader(
        IOcrEngine engine,
        LabelLocatorOptions? locatorOptions = null,
        Strings? words = null)
    {
        _words = words ?? Strings.English;
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _locatorOptions = locatorOptions ?? new LabelLocatorOptions();
        _locator = new LabelLocator(_locatorOptions);
    }

    public async Task<CatalogueReadResult> ReadAsync(
        PdfPageImageSource source,
        IReadOnlyList<int> pages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(pages);

        var samples = new List<(int Page, LabelSample Sample)>();
        var notes = new List<string>();

        foreach (var pageNumber in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var page = source.GetPage(pageNumber);
            var ink = InkMask.FromBitmap(page.Bitmap, _locatorOptions.InkThreshold);
            var labels = _locator.Locate(page);

            var reader = new LabelReader(_engine);
            foreach (var label in labels)
            {
                samples.Add((pageNumber, await reader.ReadAsync(page, ink, label, cancellationToken).ConfigureAwait(false)));
            }

            notes.Add(_words.Format(TextKey.NoteEntriesFound, pageNumber, labels.Count));
        }

        var templates = LearnTemplates(samples.Select(s => s.Sample), notes);

        return Resolve(samples, templates, notes);
    }

    /// <summary>
    /// Builds the character shapes from the part lines that were read, by pairing the text
    /// with the characters found in that line.
    /// </summary>
    private GlyphTemplateSet LearnTemplates(IEnumerable<LabelSample> samples, List<string> notes)
    {
        var templates = new GlyphTemplateSet();
        var used = 0;
        var skipped = 0;

        foreach (var sample in samples)
        {
            if (sample is not { PartNumber: { } part, ColorCode: { } color })
            {
                continue;
            }

            // The text as it is printed, so that its characters line up one for one with the
            // shapes found on that line.
            var printed = $"{part},{color}";

            if (templates.LearnFromLine(sample.PartGlyphs, printed))
            {
                used++;
            }
            else
            {
                skipped++;
            }
        }

        notes.Add(_words.Format(
            TextKey.NoteLearnedLettering,
            used,
            skipped > 0 ? _words.Format(TextKey.NoteLearnedSkipped, skipped) : string.Empty,
            string.Join("", templates.KnownCharacters.OrderBy(c => c))));

        return templates;
    }

    private CatalogueReadResult Resolve(
        List<(int Page, LabelSample Sample)> samples,
        GlyphTemplateSet templates,
        List<string> notes)
    {
        var entries = new List<CatalogueReading>();
        var unresolved = new List<UnresolvedReading>();
        var recovered = 0;

        foreach (var (page, sample) in samples)
        {
            var quantity = sample.Quantity;
            var quantitySource = ReadingSource.TextRecogniser;

            if (quantity is null)
            {
                var fromShapes = ReadQuantityFromShapes(sample, templates);
                if (fromShapes is not null)
                {
                    quantity = fromShapes;
                    quantitySource = ReadingSource.LearnedShapes;
                    recovered++;
                }
            }

            if (quantity is null || sample is not { PartNumber: { } part, ColorCode: { } color })
            {
                unresolved.Add(new UnresolvedReading(
                    page,
                    sample.Label.Bounds,
                    sample.RawText,
                    quantity,
                    sample.PartNumber,
                    sample.ColorCode,
                    Describe(quantity, sample)));
                continue;
            }

            entries.Add(new CatalogueReading(
                page, sample.Label.Bounds, quantity.Value, part, color,
                quantitySource, ReadingSource.TextRecogniser));
        }

        if (recovered > 0)
        {
            notes.Add(_words.Format(TextKey.NoteRecoveredQuantities, recovered));
        }

        return new CatalogueReadResult(entries, unresolved, notes);
    }

    /// <summary>
    /// Reads a quantity line from its character shapes. The line is digits followed by an
    /// "x", so the final shape is known by position and only the digits need classifying -
    /// which also means no letter can be mistaken for a digit.
    /// </summary>
    private static int? ReadQuantityFromShapes(LabelSample sample, GlyphTemplateSet templates)
    {
        var glyphs = sample.QuantityGlyphs;
        if (glyphs.Count < 2)
        {
            return null;
        }

        var digitGlyphs = glyphs.Take(glyphs.Count - 1).ToList();
        var text = templates.ReadLine(digitGlyphs, Digits);

        return text is not null && int.TryParse(text, out var value) && value > 0 ? value : null;
    }

    private static string Describe(int? quantity, LabelSample sample)
    {
        var missing = new List<string>(2);

        if (quantity is null)
        {
            missing.Add("quantity");
        }

        if (sample.PartNumber is null || sample.ColorCode is null)
        {
            missing.Add("part number and colour");
        }

        return $"could not read the {string.Join(" or ", missing)}";
    }
}

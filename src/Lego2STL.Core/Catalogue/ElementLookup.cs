using Lego2STL.Core.Colors;
using Lego2STL.Core.Rebrickable;
using Lego2STL.Core.Text;

namespace Lego2STL.Core.Catalogue;

/// <summary>Which of the three routes answered for an element number.</summary>
public enum ElementSource
{
    /// <summary>A local Rebrickable dump's <c>elements.csv</c>.</summary>
    LocalTable,

    /// <summary>Rebrickable's API, one call per number, cached for the run.</summary>
    RebrickableApi,

    /// <summary>The digits of a classic element number. See <see cref="ElementNumber"/>.</summary>
    ClassicNumber,
}

/// <summary>An element number taken apart: the moulding it names and the colour it is in.</summary>
public sealed record ResolvedElement(
    string PartNumber,
    int ColorCode,
    ColorScheme Scheme,
    ElementSource Source);

/// <summary>
/// Turns the element numbers an official instruction book prints into parts and colours.
/// </summary>
/// <remarks>
/// <para>
/// Three routes, tried in that order, because they differ in what they cost and in how right
/// they are. A local <c>elements.csv</c> is instant, needs no network and no key, and is
/// exact; the API is exact too but costs a throttled call per number and a key; the digits of
/// a classic number cost nothing and are exact about the colour but only usually right about
/// the design. Whichever answered is recorded, so a run can say which numbers it inferred
/// rather than looked up.
/// </para>
/// <para>
/// The table is not shipped with the tool. It is Rebrickable's to distribute, it changes as
/// new elements are issued, and it would be stale in any copy pinned into an assembly - the
/// same reasons the colour cross-reference is regenerated rather than guessed at.
/// </para>
/// </remarks>
public sealed class ElementLookup : IDisposable
{
    private readonly IReadOnlyDictionary<string, (string PartNumber, int ColorId)> _table;
    private readonly RebrickableClient? _client;
    private readonly ColorTable _colors;
    private readonly Strings _words;

    private readonly Dictionary<string, ResolvedElement?> _answered =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<ElementSource, int> _counts = new();

    private ElementLookup(
        IReadOnlyDictionary<string, (string, int)> table,
        string? tablePath,
        RebrickableClient? client,
        ColorTable colors,
        Strings words)
    {
        _table = table;
        _client = client;
        _colors = colors;
        _words = words;
        TablePath = tablePath;
    }

    /// <summary>The <c>elements.csv</c> in use, or null when there is none.</summary>
    public string? TablePath { get; }

    /// <summary>How many numbers the table holds.</summary>
    public int TableSize => _table.Count;

    /// <summary>True when a number this run cannot answer could be answered online.</summary>
    public bool CanAskRebrickable => _client is not null;

    /// <summary>
    /// Opens whichever routes are available.
    /// </summary>
    /// <param name="mapPath">Where the caller said the table is, if anywhere.</param>
    /// <param name="alsoLook">
    /// Folders to look in when no table was named - normally the document's folder and the
    /// working folder. Each is searched for <c>elements.csv</c>, one level deep.
    /// </param>
    /// <param name="apiKey">A Rebrickable key, enabling the second route.</param>
    public static ElementLookup Open(
        string? mapPath,
        IEnumerable<string?> alsoLook,
        string? apiKey,
        ColorTable colors,
        Strings? words = null)
    {
        ArgumentNullException.ThrowIfNull(colors);

        var spoken = words ?? Strings.English;

        var found = RebrickableDump.TryFindElementsFile(mapPath);

        if (found is null)
        {
            foreach (var folder in alsoLook)
            {
                found = RebrickableDump.TryFindElementsFile(folder);
                if (found is not null)
                {
                    break;
                }
            }
        }

        var table = found is null
            ? new Dictionary<string, (string, int)>(StringComparer.OrdinalIgnoreCase)
            : RebrickableDump.TryReadElements(found);

        var client = string.IsNullOrWhiteSpace(apiKey) ? null : new RebrickableClient(apiKey);

        return new ElementLookup(table, table.Count > 0 ? found : null, client, colors, spoken);
    }

    /// <summary>
    /// The part and colour behind one element number, or null when no route could say.
    /// </summary>
    public async Task<ResolvedElement?> ResolveAsync(string elementId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);

        if (_answered.TryGetValue(elementId, out var remembered))
        {
            return remembered;
        }

        var answer = await LookUpAsync(elementId, cancellationToken).ConfigureAwait(false);

        _answered[elementId] = answer;

        if (answer is not null)
        {
            _counts[answer.Source] = _counts.GetValueOrDefault(answer.Source) + 1;
        }

        return answer;
    }

    private async Task<ResolvedElement?> LookUpAsync(string elementId, CancellationToken cancellationToken)
    {
        if (_table.TryGetValue(elementId, out var row))
        {
            return new ResolvedElement(
                row.PartNumber, row.ColorId, ColorScheme.Rebrickable, ElementSource.LocalTable);
        }

        if (_client is not null)
        {
            // A number the API does not know is an answer, not a failure; anything else is
            // worth stopping for, because silently falling through would turn a wrong key or a
            // dropped connection into a catalogue full of inferred design numbers.
            var element = await _client.GetElementAsync(elementId, cancellationToken).ConfigureAwait(false);

            if (element is { Part.PartNum.Length: > 0, Color: { } color })
            {
                return new ResolvedElement(
                    element.Part.PartNum, color.Id, ColorScheme.Rebrickable, ElementSource.RebrickableApi);
            }
        }

        if (ElementNumber.TrySplitClassic(elementId, out var design, out var legoColor) &&
            _colors.TryGet(ColorScheme.Lego, legoColor, out _))
        {
            return new ResolvedElement(design, legoColor, ColorScheme.Lego, ElementSource.ClassicNumber);
        }

        return null;
    }

    /// <summary>
    /// What the lookup did, for the run's notes: where the table came from, and how the
    /// numbers divided between the three routes.
    /// </summary>
    public IReadOnlyList<string> Notes()
    {
        var notes = new List<string>();

        notes.Add(TablePath is null
            ? _words[TextKey.NoteNoElementTable]
            : _words.Format(TextKey.NoteElementTable, TableSize, TablePath));

        var parts = new List<string>();

        foreach (var (source, key) in new[]
        {
            (ElementSource.LocalTable, TextKey.NoteElementsFromTable),
            (ElementSource.RebrickableApi, TextKey.NoteElementsFromRebrickable),
            (ElementSource.ClassicNumber, TextKey.NoteElementsFromNumber),
        })
        {
            if (_counts.GetValueOrDefault(source) is var n and > 0)
            {
                parts.Add(_words.Format(key, n));
            }
        }

        if (parts.Count > 0)
        {
            notes.Add(_words.Format(TextKey.NoteElementsResolved, string.Join(", ", parts)));
        }

        return notes;
    }

    public void Dispose() => _client?.Dispose();
}

namespace Lego2STL.Core.Colors;

/// <summary>
/// The loaded colour cross-reference: look a colour up by any catalogue's code,
/// or find the closest colours to a sampled pixel.
/// </summary>
public sealed class ColorTable
{
    private readonly Dictionary<int, LegoColor> _byRebrickableId;
    private readonly Dictionary<ColorScheme, Dictionary<int, LegoColor>> _reverse;

    private ColorTable(
        IReadOnlyList<LegoColor> colors,
        Dictionary<int, LegoColor> byRebrickableId,
        Dictionary<ColorScheme, Dictionary<int, LegoColor>> reverse)
    {
        Colors = colors;
        _byRebrickableId = byRebrickableId;
        _reverse = reverse;
    }

    public IReadOnlyList<LegoColor> Colors { get; }

    public int Count => Colors.Count;

    /// <summary>
    /// Builds a table from colours plus an explicit reverse map. The reverse map is
    /// generated once (see <see cref="ColorTableBuilder"/>) rather than inferred here,
    /// because several external codes are claimed by more than one colour and alias codes
    /// have to be accounted for too.
    /// </summary>
    public static ColorTable Create(
        IEnumerable<LegoColor> colors,
        IEnumerable<ColorCodeMapping> mappings)
    {
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(mappings);

        var list = colors.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("A colour table needs at least one colour.", nameof(colors));
        }

        var byId = new Dictionary<int, LegoColor>(list.Count);
        foreach (var color in list)
        {
            if (!byId.TryAdd(color.RebrickableId, color))
            {
                throw new InvalidOperationException(
                    $"Colour table lists Rebrickable id {color.RebrickableId} twice.");
            }
        }

        var reverse = Enum.GetValues<ColorScheme>()
            .ToDictionary(s => s, _ => new Dictionary<int, LegoColor>());

        foreach (var m in mappings)
        {
            if (!byId.TryGetValue(m.RebrickableId, out var color))
            {
                throw new InvalidOperationException(
                    $"Reverse map sends {m.Scheme} {m.Code} to Rebrickable colour {m.RebrickableId}, " +
                    "which is not in the table. Regenerate it with 'lego2stl refresh-colors'.");
            }

            if (!reverse[m.Scheme].TryAdd(m.Code, color))
            {
                throw new InvalidOperationException(
                    $"Reverse map has two entries for {m.Scheme} {m.Code}. " +
                    "Regenerate it with 'lego2stl refresh-colors'.");
            }
        }

        return new ColorTable(list, byId, reverse);
    }

    /// <summary>Looks a colour up by its code in the given scheme.</summary>
    public bool TryGet(ColorScheme scheme, int code, out LegoColor color) =>
        _reverse[scheme].TryGetValue(code, out color!);

    /// <summary>
    /// Looks a colour up, or throws naming the scheme and code, so an unmappable code is
    /// reported rather than silently guessed.
    /// </summary>
    public LegoColor Get(ColorScheme scheme, int code) =>
        TryGet(scheme, code, out var color)
            ? color
            : throw new KeyNotFoundException(
                $"No colour has {scheme} code {code}. " +
                (scheme == ColorScheme.BrickLink
                    ? "Check --color-scheme: is the input really using BrickLink codes?"
                    : $"Check --color-scheme {scheme}."));

    /// <summary>Looks a colour up by Rebrickable id, the primary key.</summary>
    public bool TryGetByRebrickableId(int id, out LegoColor color) =>
        _byRebrickableId.TryGetValue(id, out color!);

    /// <summary>How many codes resolve in the given scheme.</summary>
    public int CountIn(ColorScheme scheme) => _reverse[scheme].Count;

    /// <summary>
    /// Colours ranked by perceptual distance from a sampled pixel, closest first.
    /// Translucent colours are excluded: a render of a translucent part shows whatever is
    /// behind it, so the sample says nothing about the part's own colour.
    /// </summary>
    public IReadOnlyList<ColorMatch> RankByDistance(Rgb24 sampled) =>
        Colors
            .Where(c => !c.IsUnknown && !c.IsTranslucent)
            .Select(c => new ColorMatch(c, c.Rgb.DeltaE(sampled)))
            .OrderBy(m => m.DeltaE)
            .ToList();
}

/// <summary>A candidate colour and how far it sits from a sampled pixel.</summary>
public sealed record ColorMatch(LegoColor Color, double DeltaE);

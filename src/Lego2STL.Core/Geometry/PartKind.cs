namespace Lego2STL.Core.Geometry;

/// <summary>The kinds of part this tool has a rule for, and one for everything else.</summary>
public enum PartKind
{
    Unknown,
    Brick,
    Plate,
    Tile,
    Beam,
    Axle,
    Pin,
}

/// <summary>
/// Reads what a part is out of the description its LDraw file carries.
/// </summary>
/// <remarks>
/// <para>
/// Not from the parts database. That reads from a Rebrickable bulk download whose
/// <c>inventory_parts.csv</c> alone is 132 MB, which is never committed, so most runs have no
/// category for anything. The description is already in hand for every part that produced a shape.
/// </para>
/// <para>
/// Deliberately incomplete. Measured over run 6324712, these kinds cover about three fifths of a
/// real set and the rest comes back unknown, which is a verdict rather than a failure: a part with
/// no rule is left exactly as the pipeline already leaves it.
/// </para>
/// </remarks>
public static class PartKinds
{
    /// <summary>
    /// Ordered, because a title can read as two kinds and the first match wins.
    /// </summary>
    /// <remarks>
    /// Pin before axle: an "Axle Pin" is a pin with an axle on the end, and it is the pin that
    /// decides how it lies. Tile and plate before brick for the same reason - "Tile" and "Plate"
    /// are their own kinds and neither is a low brick as far as printing is concerned.
    /// </remarks>
    private static readonly (string Word, PartKind Kind)[] Words =
    [
        ("pin", PartKind.Pin),
        ("axle", PartKind.Axle),
        ("beam", PartKind.Beam),
        ("tile", PartKind.Tile),
        ("plate", PartKind.Plate),
        ("brick", PartKind.Brick),
    ];

    /// <summary>The family word every second Technic part leads with, which is not a kind.</summary>
    private const string Family = "Technic";

    /// <summary>What the part is, or unknown when its description does not say.</summary>
    public static PartKind FromTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.StartsWith('~'))
        {
            return PartKind.Unknown;
        }

        var leading = LeadingKindWords(title);

        foreach (var (word, kind) in Words)
        {
            if (leading.Any(w => string.Equals(w, word, StringComparison.OrdinalIgnoreCase)))
            {
                return kind;
            }
        }

        return PartKind.Unknown;
    }

    /// <summary>
    /// The kind words a description leads with, once the family word is set aside.
    /// </summary>
    /// <remarks>
    /// A description is only about a kind when it leads with that kind. "Slope Brick 45" leads
    /// with Slope and is therefore not a brick as far as printing goes, while "Technic Brick" is
    /// one; anything reached only after some other word - a shape, a mechanism, a fitting - was
    /// qualified by that word and is not the plain kind. Reading is over at the first measurement,
    /// so a "Plate" late in a long description cannot reach back and rename the part.
    /// </remarks>
    private static List<string> LeadingKindWords(string title)
    {
        var leading = new List<string>();

        foreach (var word in title.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!char.IsLetter(word[0]))
            {
                break;
            }

            if (leading.Count == 0 && string.Equals(word, Family, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Words.Any(w => string.Equals(w.Word, word, StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }

            leading.Add(word);
        }

        return leading;
    }
}

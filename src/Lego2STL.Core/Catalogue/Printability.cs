using Lego2STL.Core.Rebrickable;

namespace Lego2STL.Core.Catalogue;

/// <summary>Whether a part is printed at all, and when it is not, why not.</summary>
public enum Printable
{
    /// <summary>Nothing known about it says otherwise.</summary>
    Yes,

    /// <summary>Made of something no printer can lay down.</summary>
    NotItsMaterial,

    /// <summary>A kind of thing that is bought rather than made: electronics, stickers.</summary>
    NotItsKind,

    /// <summary>Nothing is known about it, which is not a reason to leave it out.</summary>
    Unknown,
}

/// <summary>
/// Decides which parts a printer is asked to make.
/// </summary>
/// <remarks>
/// The kind has to be consulted as well as the material because every one of the parts database's
/// 615 electronic parts is plastic, battery boxes included - so a run reading the material alone
/// prints hollow shells of things that have to be bought.
/// </remarks>
public static class Printability
{
    /// <summary>Materials no printer can lay down.</summary>
    public static IReadOnlySet<string> UnprintableMaterials { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Rubber", "Cloth", "Cardboard/Paper", "Foam", "Flexible Plastic", "Metal",
        };

    /// <summary>Kinds of part that are bought rather than made, whatever they are made of.</summary>
    public static IReadOnlySet<string> UnprintableCategories { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Electronics", "Stickers" };

    /// <summary>The material is answered first, being the more specific of the two.</summary>
    public static Printable Of(PartFact? fact) =>
        fact is null ? Printable.Unknown
        : UnprintableMaterials.Contains(fact.Material) ? Printable.NotItsMaterial
        : UnprintableCategories.Contains(fact.Category) ? Printable.NotItsKind
        : Printable.Yes;

    /// <summary>True when the run should build it; an unknown part is built like any other.</summary>
    public static bool IsPrinted(this Printable verdict) =>
        verdict is Printable.Yes or Printable.Unknown;

    /// <summary>The word a run's record keeps, so the wording can be chosen when it is read.</summary>
    public static string Token(this Printable verdict) => verdict switch
    {
        Printable.NotItsMaterial => "material",
        Printable.NotItsKind => "kind",
        Printable.Unknown => "unknown",
        _ => "yes",
    };

    /// <summary>Reads that word back. Anything unrecognised, including nothing, is unknown.</summary>
    public static Printable FromToken(string? token) => token switch
    {
        "material" => Printable.NotItsMaterial,
        "kind" => Printable.NotItsKind,
        "yes" => Printable.Yes,
        _ => Printable.Unknown,
    };

    /// <summary>
    /// Splits a list into the parts to build and the parts to leave, keeping their order.
    /// </summary>
    /// <param name="printEverything">
    /// Build them all regardless, for anyone who wants the shell of an electronic part.
    /// </param>
    public static (IReadOnlyList<string> Build, IReadOnlyList<string> Leave) Choose(
        IReadOnlyList<string> parts,
        IReadOnlyDictionary<string, PartFact> facts,
        bool printEverything)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(facts);

        if (printEverything)
        {
            return (parts, []);
        }

        var build = new List<string>(parts.Count);
        var leave = new List<string>();

        foreach (var part in parts)
        {
            if (Of(facts.GetValueOrDefault(part)).IsPrinted())
            {
                build.Add(part);
            }
            else
            {
                leave.Add(part);
            }
        }

        return (build, leave);
    }
}

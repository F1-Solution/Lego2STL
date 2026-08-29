using Lego2STL.Core.Colors;

namespace Lego2STL.Core.Catalogue;

/// <summary>
/// One row of the parts list: a part in a colour, and how many are needed.
/// </summary>
/// <param name="Id">Sequential number, 1..N, in the order the entries appear in the source.</param>
/// <param name="PartNumber">The part number as printed, e.g. "32525" or "4265c".</param>
/// <param name="BrickLinkColorCode">
/// The BrickLink colour number. Always BrickLink's, whatever numbering the input used, so
/// that lists from different sources can be compared and merged.
/// </param>
/// <param name="ColorName">The colour's name, so the file can be read by a human.</param>
/// <param name="Rgb">The colour's value, for previews and for colouring plates.</param>
/// <param name="Quantity">How many of this part in this colour.</param>
/// <param name="ElementId">
/// The LEGO element number the entry was read from, when it was read from one. Null for a list
/// that came from a CSV or from a set, which name a part and a colour rather than an element.
/// </param>
public sealed record PartEntry(
    int Id,
    string PartNumber,
    int BrickLinkColorCode,
    string ColorName,
    Rgb24 Rgb,
    int Quantity,
    string? ElementId = null)
{
    /// <summary>
    /// What makes two rows the same row. Colour is part of the identity because the same
    /// part in two colours is two separate things to buy; it is not part of the geometry,
    /// which is why the shapes produced later are counted per part number instead.
    /// </summary>
    public (string PartNumber, int Color) Key => (PartNumber.ToLowerInvariant(), BrickLinkColorCode);

    public override string ToString() =>
        $"{Id}: {Quantity}x {PartNumber} {ColorName} ({BrickLinkColorCode})";
}

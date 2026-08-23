namespace Lego2STL.Core.Colors;

/// <summary>
/// One LEGO colour with its cross-references and its RGB value.
/// Immutable; built once when the vendored reference table is loaded.
/// </summary>
/// <param name="RebrickableId">Rebrickable's id. Always present; this is the primary key.</param>
/// <param name="Name">Rebrickable's colour name, e.g. "Light Bluish Gray".</param>
/// <param name="Rgb">
/// Rebrickable's RGB. Chosen deliberately: measured against the pixels of a real
/// instruction PDF it is markedly closer than LDraw's or BrickLink's values
/// (black #010713 in the PDF vs Rebrickable #05131D, LDraw #1B2A34, BrickLink #2E2E2E).
/// </param>
/// <param name="IsTranslucent">Rebrickable's is_trans flag.</param>
/// <param name="BrickLinkId">
/// The colour's primary BrickLink id, used when writing output. 216 of 275 colours have one.
/// This is the forward direction only; going the other way is <see cref="ColorTable.TryGet"/>,
/// because several BrickLink ids are claimed by more than one colour.
/// </param>
/// <param name="LegoId">Primary LEGO id, when known. 191 of 275 colours have one.</param>
/// <param name="LDrawId">Primary LDraw code, when known. 169 of 275 colours have one.</param>
/// <param name="PartCount">
/// How many parts Rebrickable knows in this colour. Used as a last-resort tie-break when
/// generating the reverse map, and kept for diagnostics.
/// </param>
public sealed record LegoColor(
    int RebrickableId,
    string Name,
    Rgb24 Rgb,
    bool IsTranslucent,
    int? BrickLinkId,
    int? LegoId,
    int? LDrawId,
    int PartCount)
{
    /// <summary>
    /// Rebrickable's sentinel for "no colour information". It claims 17 LDraw codes,
    /// including LDraw's meta-colours 16 (inherit) and 24 (edge), so it must never
    /// be the target of a reverse lookup.
    /// </summary>
    public const int UnknownRebrickableId = -1;

    public bool IsUnknown => RebrickableId == UnknownRebrickableId;

    /// <summary>The primary code this colour carries under the given scheme, if any.</summary>
    public int? CodeIn(ColorScheme scheme) => scheme switch
    {
        ColorScheme.BrickLink => BrickLinkId,
        ColorScheme.Rebrickable => RebrickableId,
        ColorScheme.Lego => LegoId,
        ColorScheme.LDraw => LDrawId,
        _ => throw new ArgumentOutOfRangeException(nameof(scheme), scheme, "Unknown colour scheme."),
    };
}

/// <summary>
/// One entry of the reverse map: "code <paramref name="Code"/> in
/// <paramref name="Scheme"/> means this Rebrickable colour".
/// </summary>
/// <remarks>
/// Stored explicitly in the vendored reference rather than derived at runtime. The mapping
/// is genuinely many-to-one and includes alias codes, so working it out needs the whole
/// catalogue in view at once; doing it at generation time keeps every decision auditable
/// and makes the runtime lookup a plain dictionary hit.
/// </remarks>
public sealed record ColorCodeMapping(ColorScheme Scheme, int Code, int RebrickableId);

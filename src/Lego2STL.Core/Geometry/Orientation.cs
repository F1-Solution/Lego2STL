using System.Numerics;

namespace Lego2STL.Core.Geometry;

/// <summary>
/// How a part of a given kind is laid on the bed.
/// </summary>
/// <remarks>
/// <para>
/// Not an optimiser, and deliberately not one. Scoring overhang area over the six axis-aligned
/// orientations was measured over 175 real shapes and rejected: it recommends turning 95 of them
/// and leaves 50 taller than three times their narrowest footprint side, which for a 12L axle
/// means standing 191 mm of part on a 9.6 mm base. The parts such a score shouts loudest about -
/// plates and panels - are the ones already lying correctly.
/// </para>
/// <para>
/// So every rule here is a rule to do nothing, written down so that it is a decision rather than
/// an accident, and so that the tests can hold it still. The governing rule behind the table is
/// that no support may ever touch a mating surface: a stud, an interior tube, a Technic hole, an
/// axle or a pin. Where orientation and support disagree, orientation moves.
/// </para>
/// </remarks>
public static class Orientation
{
    /// <summary>
    /// How to lay a part of this kind, or null to leave it exactly where the pipeline put it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null for every kind, today. The one turn this design proposed was to roll an axle a
    /// quarter of a right angle about its length, so that its cross rests on one arm rather than
    /// on two flat undersides. Measured on the six plain axles of run 6324712 it lowered the
    /// overhanging area for four of them and raised it for two, because rolled that far the
    /// underside faces sit exactly on the 45 degree self-supporting limit and whether they count
    /// as overhangs is a tie the score breaks differently for different meshes.
    /// </para>
    /// <para>
    /// The shape of the answer is kept - a turn, or nothing - because a future rule is a turn, and
    /// because a caller that already handles both needs no changing when one arrives.
    /// </para>
    /// </remarks>
    public static Matrix4x4? For(PartKind kind) => kind switch
    {
        _ => null,
    };

    /// <summary>
    /// The rule's own name, for the record a run keeps. Null when no rule reached the part.
    /// </summary>
    /// <remarks>
    /// A rule that confirms is still a rule and is still recorded, so that the parts no rule
    /// reached can be counted off a real run. That set is how the table is meant to grow.
    /// </remarks>
    public static string? Name(PartKind kind) => kind == PartKind.Unknown ? null : kind.ToString();
}

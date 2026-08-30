using Lego2STL.Core.Colors;
using Lego2STL.Core.Run;

namespace Lego2STL.Core.Catalogue;

/// <summary>
/// Folds the answers given about unread entries back into the parts list.
/// </summary>
/// <remarks>
/// <para>
/// Rows are matched on <see cref="PartEntry.Key"/>, the project's existing answer to "is this
/// the same row", so an answer naming a part already on the list adds to its quantity instead
/// of printing it twice.
/// </para>
/// <para>
/// An answer that does not name a part, a colour and a quantity changes nothing. The dialogue
/// is free to record a partial answer - a part number read cleanly beside a colour that was
/// not - and this is where partial means the list is left alone rather than half corrected.
/// </para>
/// </remarks>
public static class PartsListCorrection
{
    /// <summary>The list as the answers leave it, renumbered from one.</summary>
    public static PartsList Apply(PartsList list, IReadOnlyList<ReviewAnswer> answers)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(answers);

        var entries = new List<PartEntry>(list.Entries);
        var changed = false;

        foreach (var answer in answers)
        {
            if (Added(entries, answer))
            {
                changed = true;
            }
        }

        // Built rather than copied with: the list works its distinct part numbers out once, in
        // its own constructor, and a copy would carry the old set beside the new rows.
        return changed
            ? new PartsList([.. entries.Select((e, i) => e with { Id = i + 1 })], list.Notes)
            : list;
    }

    /// <summary>Puts one answer on the list, and says whether it changed anything.</summary>
    private static bool Added(List<PartEntry> entries, ReviewAnswer answer)
    {
        if (answer.NotAnEntry
            || answer.PartNumber is not { Length: > 0 } part
            || answer.ColorCode is not { } code
            || answer.Quantity is not { } quantity
            || quantity <= 0
            || !ColorReference.Table.TryGet(ColorScheme.BrickLink, code, out var colour))
        {
            return false;
        }

        var key = (part.ToLowerInvariant(), code);
        var at = entries.FindIndex(e => e.Key == key);

        if (at >= 0)
        {
            entries[at] = entries[at] with { Quantity = entries[at].Quantity + quantity };
            return true;
        }

        entries.Add(new PartEntry(entries.Count + 1, part, code, colour.Name, colour.Rgb, quantity));
        return true;
    }
}

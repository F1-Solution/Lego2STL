using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Rebrickable;

namespace Lego2STL.Core.Pipeline;

/// <summary>
/// Builds a parts list from a set number, by asking what is in the box.
/// </summary>
/// <remarks>
/// <para>
/// The shortest route in, when the set is a catalogued one: no document to read, no pages to
/// find, no text to recognise. It needs a key and a connection, which reading a document does
/// not, so it is an alternative rather than a replacement.
/// </para>
/// <para>
/// Spare pieces are left out unless asked for. A set's inventory lists the handful of extras
/// in the bag alongside the pieces the model uses, and printing those is usually not what
/// anyone wants.
/// </para>
/// </remarks>
public static class SetInventory
{
    public static async Task<PartsList> FetchAsync(
        string setNumber,
        ColorTable colors,
        bool includeSpares = false,
        string? apiKey = null,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setNumber);
        ArgumentNullException.ThrowIfNull(colors);

        var say = log ?? (_ => { });
        var normalised = RebrickableClient.NormaliseSetNumber(setNumber);

        using var client = new RebrickableClient(RebrickableApiKey.Require(apiKey));

        say($"Looking up set {normalised}.");
        var inventory = await client.GetSetPartsAsync(normalised, cancellationToken).ConfigureAwait(false);

        var notes = new List<string>();
        var entries = new List<PartEntry>();
        var byKey = new Dictionary<(string, int), int>();
        var spares = 0;
        var unmapped = 0;

        foreach (var line in inventory)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (line.IsSpare && !includeSpares)
            {
                spares++;
                continue;
            }

            if (line.Part is not { } part || line.Color is not { } colour)
            {
                continue;
            }

            if (!colors.TryGetByRebrickableId(colour.Id, out var known))
            {
                unmapped++;
                notes.Add(
                    $"{part.PartNum} is listed in colour {colour.Id} ({colour.Name}), which is not " +
                    "in the colour cross-reference; the piece was left out. Regenerate it with " +
                    "'lego2stl refresh-colors'.");
                continue;
            }

            if (known.BrickLinkId is not { } brickLinkId)
            {
                unmapped++;
                notes.Add(
                    $"{part.PartNum} is '{known.Name}', which has no BrickLink colour number, so " +
                    "it has no value for that column; the piece was left out.");
                continue;
            }

            var key = (part.PartNum.ToLowerInvariant(), brickLinkId);

            if (byKey.TryGetValue(key, out var at))
            {
                entries[at] = entries[at] with { Quantity = entries[at].Quantity + line.Quantity };
                continue;
            }

            byKey[key] = entries.Count;
            entries.Add(new PartEntry(
                Id: 0,
                PartNumber: part.PartNum,
                BrickLinkColorCode: brickLinkId,
                ColorName: known.Name,
                Rgb: known.Rgb,
                Quantity: line.Quantity));
        }

        if (entries.Count == 0)
        {
            throw new InvalidOperationException(
                $"Set {normalised} came back with nothing usable. Check the number: Rebrickable " +
                "wants the variant suffix, so 42100 means 42100-1.");
        }

        if (spares > 0)
        {
            notes.Add($"{spares} spare piece(s) left out. Ask for --include-spares to keep them.");
        }

        if (unmapped > 0)
        {
            notes.Add($"{unmapped} line(s) could not be given a BrickLink colour and were left out.");
        }

        say($"Set {normalised}: {entries.Count} entries, {entries.Sum(e => e.Quantity)} pieces.");

        var numbered = entries.Select((entry, index) => entry with { Id = index + 1 }).ToList();
        return new PartsList(numbered, notes);
    }
}

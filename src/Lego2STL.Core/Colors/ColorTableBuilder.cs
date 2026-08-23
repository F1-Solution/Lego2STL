using Lego2STL.Core.Rebrickable;

namespace Lego2STL.Core.Colors;

/// <summary>
/// Builds the colour cross-reference from the Rebrickable API, deciding once and for all
/// which colour each external code resolves to.
/// </summary>
/// <remarks>
/// The mapping is not one-to-one in the live data, which is the whole reason this class
/// exists. Measured on 2026-08-22 across 275 colours:
/// <list type="bullet">
///   <item>216 have a BrickLink id (214 distinct) — BrickLink 77 and 72 are each claimed by two colours</item>
///   <item>191 have a LEGO id, 13 of them more than one — three LEGO codes are claimed twice</item>
///   <item>169 have an LDraw code, 4 of them more than one — LDraw 112 and 216 are claimed twice</item>
///   <item>the "[Unknown]" sentinel claims 17 LDraw codes, including meta-colours 16 and 24</item>
/// </list>
/// Alias codes count too: black carries LEGO [26, 342] and LDraw [0, 256], so a reverse
/// lookup has to answer for 342 and 256 as well as the primaries. Every contest is settled
/// by <see cref="ChooseWinner"/> and every decision is reported so it can be audited.
/// </remarks>
public static class ColorTableBuilder
{
    /// <summary>Fetches the colours and resolves them into a shippable table.</summary>
    public static async Task<ColorTableBuildResult> BuildAsync(
        RebrickableClient client,
        IReadOnlyDictionary<int, int>? partCounts = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        var raw = await client.GetColorsAsync(ct).ConfigureAwait(false);
        return Build(raw, partCounts);
    }

    internal static ColorTableBuildResult Build(
        IReadOnlyList<RbColor> raw,
        IReadOnlyDictionary<int, int>? partCounts)
    {
        var notes = new List<string>();

        var drafts = raw.Select(c => new Draft(
            Id: c.Id,
            Name: c.Name,
            Rgb: ParseRgb(c, notes),
            IsTranslucent: c.IsTrans,
            PartCount: partCounts is not null && partCounts.TryGetValue(c.Id, out var n) ? n : 0,
            External: ReadExternal(c))).ToList();

        var colors = drafts.Select(d => new LegoColor(
            RebrickableId: d.Id,
            Name: d.Name,
            Rgb: d.Rgb,
            IsTranslucent: d.IsTranslucent,
            BrickLinkId: d.PrimaryCode(ColorScheme.BrickLink),
            LegoId: d.PrimaryCode(ColorScheme.Lego),
            LDrawId: d.PrimaryCode(ColorScheme.LDraw),
            PartCount: d.PartCount)).ToList();

        var mappings = BuildReverseMap(drafts, notes);

        return new ColorTableBuildResult(ColorTable.Create(colors, mappings), colors, mappings, notes);
    }

    private static List<ColorCodeMapping> BuildReverseMap(List<Draft> drafts, List<string> notes)
    {
        var mappings = new List<ColorCodeMapping>();

        // Rebrickable ids are the primary key, so they map to themselves.
        mappings.AddRange(drafts.Select(d => new ColorCodeMapping(ColorScheme.Rebrickable, d.Id, d.Id)));

        foreach (var scheme in new[] { ColorScheme.BrickLink, ColorScheme.Lego, ColorScheme.LDraw })
        {
            // Every code any colour claims, primary or alias. The sentinel is excluded
            // outright: it claims LDraw's meta-colours and would poison those lookups.
            var claims = drafts
                .Where(d => d.Id != LegoColor.UnknownRebrickableId)
                .Where(d => d.External.ContainsKey(scheme))
                .SelectMany(d => d.External[scheme].Codes.Select(code => (Draft: d, Code: code)))
                .GroupBy(x => x.Code);

            foreach (var group in claims)
            {
                var contenders = group.Select(x => x.Draft).ToList();
                var winner = contenders.Count == 1
                    ? contenders[0]
                    : ChooseWinner(scheme, group.Key, contenders, notes);

                mappings.Add(new ColorCodeMapping(scheme, group.Key, winner.Id));
            }
        }

        return mappings;
    }

    /// <summary>
    /// Decides which colour a contested external code resolves to.
    /// </summary>
    /// <remarks>
    /// Applied in order; the first rule that separates them wins:
    /// <list type="number">
    ///   <item>
    ///     the code is that colour's <b>primary</b> id rather than an alias — this settles
    ///     LDraw 112, where Medium Bluish Violet lists 112 first while Medium Violet lists
    ///     it second
    ///   </item>
    ///   <item>
    ///     the colour's name matches the catalogue's own name for the code — this settles
    ///     BrickLink 77 (both list it first, but only Pearl Dark Gray is what BrickLink
    ///     calls 77), BrickLink 72 and LDraw 216
    ///   </item>
    ///   <item>more parts known in that colour, so the widely-used one wins</item>
    ///   <item>lower Rebrickable id, purely so the result is deterministic</item>
    /// </list>
    /// </remarks>
    private static Draft ChooseWinner(ColorScheme scheme, int code, List<Draft> contenders, List<string> notes)
    {
        var remaining = Narrow(contenders, d => d.PrimaryCode(scheme) == code);
        var rule = "it is that colour's primary id";

        if (remaining.Count > 1)
        {
            remaining = Narrow(remaining, d => NameMatchesCatalogue(d, scheme, code));
            rule = "its name matches the catalogue's own name for the code";
        }

        if (remaining.Count > 1)
        {
            var best = remaining.Max(d => d.PartCount);
            remaining = Narrow(remaining, d => d.PartCount == best);
            rule = $"it has more parts in that colour ({best})";
        }

        if (remaining.Count > 1)
        {
            var lowest = remaining.Min(d => d.Id);
            remaining = Narrow(remaining, d => d.Id == lowest);
            rule = "it has the lowest Rebrickable id (arbitrary, but deterministic)";
        }

        var winner = remaining[0];
        var losers = string.Join(", ", contenders.Where(d => d.Id != winner.Id).Select(d => $"{d.Name} (rb{d.Id})"));
        notes.Add(
            $"{scheme} {code} is claimed by {contenders.Count} colours; resolved to " +
            $"'{winner.Name}' (rb{winner.Id}) because {rule}. Not chosen: {losers}.");

        return winner;
    }

    /// <summary>Applies a filter, but keeps the original list if the filter would eliminate everything.</summary>
    private static List<Draft> Narrow(List<Draft> candidates, Func<Draft, bool> keep)
    {
        var filtered = candidates.Where(keep).ToList();
        return filtered.Count == 0 ? candidates : filtered;
    }

    private static bool NameMatchesCatalogue(Draft d, ColorScheme scheme, int code)
    {
        var external = d.External[scheme];
        var index = external.Codes.IndexOf(code);
        if (index < 0 || index >= external.Names.Count)
        {
            return false;
        }

        return external.Names[index].Any(name => NameEquals(name, d.Name));
    }

    /// <summary>LDraw writes names with underscores ("Pearl_Dark_Gray"); compare loosely.</summary>
    private static bool NameEquals(string a, string b) =>
        string.Equals(
            a.Replace('_', ' ').Trim(),
            b.Replace('_', ' ').Trim(),
            StringComparison.OrdinalIgnoreCase);

    private static Rgb24 ParseRgb(RbColor c, List<string> notes)
    {
        if (Rgb24.TryParse(c.Rgb, out var rgb))
        {
            return rgb;
        }

        notes.Add($"Colour '{c.Name}' (rb{c.Id}) has an unreadable RGB value '{c.Rgb}'; using black.");
        return new Rgb24(0, 0, 0);
    }

    private static Dictionary<ColorScheme, ExternalRefs> ReadExternal(RbColor c)
    {
        var result = new Dictionary<ColorScheme, ExternalRefs>();
        if (c.ExternalIds is null)
        {
            return result;
        }

        foreach (var (key, scheme) in new[]
                 {
                     ("BrickLink", ColorScheme.BrickLink),
                     ("LEGO", ColorScheme.Lego),
                     ("LDraw", ColorScheme.LDraw),
                 })
        {
            if (!c.ExternalIds.TryGetValue(key, out var e) || e.ExtIds is null)
            {
                continue;
            }

            // Drop null codes while keeping codes and their names index-aligned:
            // NameMatchesCatalogue looks a name up by the code's position.
            // Peeron really does return "ext_ids": [null] for some colours.
            var codes = new List<int>(e.ExtIds.Count);
            var names = new List<List<string>>(e.ExtIds.Count);

            for (var i = 0; i < e.ExtIds.Count; i++)
            {
                if (e.ExtIds[i] is not { } code)
                {
                    continue;
                }

                codes.Add(code);
                names.Add(e.ExtDescrs is not null && i < e.ExtDescrs.Count ? e.ExtDescrs[i] ?? [] : []);
            }

            if (codes.Count > 0)
            {
                result[scheme] = new ExternalRefs(codes, names);
            }
        }

        return result;
    }

    private sealed record ExternalRefs(List<int> Codes, List<List<string>> Names);

    private sealed record Draft(
        int Id,
        string Name,
        Rgb24 Rgb,
        bool IsTranslucent,
        int PartCount,
        Dictionary<ColorScheme, ExternalRefs> External)
    {
        /// <summary>The first code listed for a scheme; later ones are aliases.</summary>
        public int? PrimaryCode(ColorScheme scheme) =>
            External.TryGetValue(scheme, out var e) && e.Codes.Count > 0 ? e.Codes[0] : null;
    }
}

/// <summary>The generated table, plus every resolution decision so it can be audited.</summary>
public sealed record ColorTableBuildResult(
    ColorTable Table,
    IReadOnlyList<LegoColor> Colors,
    IReadOnlyList<ColorCodeMapping> Mappings,
    IReadOnlyList<string> Notes);

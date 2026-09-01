using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lego2STL.Core.Run;

/// <summary>
/// A clearance that was measured once, under the name whoever measured it chose.
/// </summary>
/// <param name="Name">
/// Chosen, not composed. A key of printer and nozzle and material cannot express two spools of
/// the same material that behave differently, or a machine that has drifted since January; a
/// name someone wrote can.
/// </param>
/// <param name="Millimetres">The clearance itself.</param>
/// <param name="Preferred">Whether a build with nothing else to go on should use this one.</param>
public sealed record TolerancePreset(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("millimetres")] double Millimetres,
    [property: JsonPropertyName("preferred")] bool Preferred,
    [property: JsonPropertyName("savedAt")] DateTimeOffset SavedAt);

/// <summary>
/// Where a measured clearance is kept, for both the command line and the window.
/// </summary>
/// <remarks>
/// <para>
/// In Core and not beside the window's other preferences, because the command line does not
/// reference the window's assembly and so cannot read its file. This is the one thing about the
/// design that was forced rather than chosen.
/// </para>
/// <para>
/// A file that cannot be read is treated as no presets. Losing a preference is a far smaller
/// problem than refusing to run, and it is how the window's own preferences already behave.
/// </para>
/// </remarks>
public static class TolerancePresets
{
    public static string FilePath => AppDataDirectory.File("tolerances.json");

    private static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    public static IReadOnlyList<TolerancePreset> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            return JsonSerializer.Deserialize<List<TolerancePreset>>(File.ReadAllText(FilePath)) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static void Save(IReadOnlyList<TolerancePreset> presets)
    {
        ArgumentNullException.ThrowIfNull(presets);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(OnlyOnePreferred(presets), Format));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not being able to remember a measurement is not worth interrupting anyone over.
        }
    }

    /// <summary>Records a figure under a name, replacing any preset already using that name.</summary>
    public static IReadOnlyList<TolerancePreset> Remember(string name, double millimetres, bool preferred)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var kept = Load().Where(p => !Matches(p, name)).ToList();

        if (preferred)
        {
            kept = [.. kept.Select(p => p with { Preferred = false })];
        }

        kept.Add(new TolerancePreset(name.Trim(), millimetres, preferred, DateTimeOffset.UtcNow));

        var ordered = Ordered(kept);
        Save(ordered);
        return ordered;
    }

    public static IReadOnlyList<TolerancePreset> Prefer(string name)
    {
        var updated = Ordered([.. Load().Select(p => p with { Preferred = Matches(p, name) })]);
        Save(updated);
        return updated;
    }

    public static IReadOnlyList<TolerancePreset> Forget(string name)
    {
        var updated = Ordered([.. Load().Where(p => !Matches(p, name))]);
        Save(updated);
        return updated;
    }

    /// <summary>The preset going by this name, matched the way a person would match it.</summary>
    public static TolerancePreset? Find(IReadOnlyList<TolerancePreset> presets, string? name)
    {
        ArgumentNullException.ThrowIfNull(presets);

        return string.IsNullOrWhiteSpace(name) ? null : presets.FirstOrDefault(p => Matches(p, name));
    }

    private static bool Matches(TolerancePreset preset, string name) =>
        string.Equals(preset.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>By name, so the list reads the same however it was built up.</summary>
    private static IReadOnlyList<TolerancePreset> Ordered(IEnumerable<TolerancePreset> presets) =>
        [.. presets.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// The store's own guarantee, applied on the way out.
    /// </summary>
    /// <remarks>
    /// A build silently picking one of two preferred presets would apply a number nobody chose,
    /// so the last one wins here rather than the question being left open.
    /// </remarks>
    private static IReadOnlyList<TolerancePreset> OnlyOnePreferred(IReadOnlyList<TolerancePreset> presets)
    {
        var winner = presets.LastOrDefault(p => p.Preferred);

        return winner is null
            ? presets
            : [.. presets.Select(p => p with { Preferred = ReferenceEquals(p, winner) })];
    }
}

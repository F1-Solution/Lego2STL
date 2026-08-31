using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lego2STL.Core.Plates;

/// <summary>
/// The slicer settings that go beside a plate, as a preset the slicer can import.
/// </summary>
/// <remarks>
/// <para>
/// A preset and not a project file. The 3MF stays a plate any reader understands: writing a
/// slicer's own project format into it would trade away the one thing this tool's 3MF was written
/// for, which is that it depends on no library and so has nothing to go stale.
/// </para>
/// <para>
/// It asserts only what this tool knows. It knows the bed, because it packs onto one, and it knows
/// what it built - small interlocking parts whose accuracy matters more than their speed, laid
/// down so that no support is needed. It does not know the nozzle, the filament, the spool or the
/// room, so it says nothing about any of them; those are in the sheet beside it, as advice.
/// </para>
/// <para>
/// Every key, every value type and every spelling below was checked on 2026-08-31 against the
/// profiles Bambu Studio ships and loads, by resolving each inheritance chain down to
/// fdm_process_common. A guessed key imports cleanly with the setting silently missing, which is
/// the worst failure available here: it looks like it worked.
/// </para>
/// </remarks>
public static class ProcessPreset
{
    /// <summary>
    /// What each printer's preset inherits from, and how many slots its per-extruder settings take.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read off a real installation. Three things here are not guessable. The family name is not
    /// uniform - the A1, P1 and X1 lines call the 0.16 mm profile Optimal while the H2 line calls
    /// it Standard. The P1S has no profiles of its own at all and borrows the P1P's. And the
    /// number of slots a per-extruder setting takes is not the number of extruders: the P1P and
    /// the X1C have one each and their profiles carry two values, while the H2D carries seven.
    /// </para>
    /// <para>The names are also per nozzle; these are the default 0.4 mm ones.</para>
    /// </remarks>
    private static readonly Dictionary<string, (string Base, int Slots)> Bases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["A1"] = ("0.16mm Optimal @BBL A1", 1),
            ["A1mini"] = ("0.16mm Optimal @BBL A1M", 1),
            ["P1P"] = ("0.16mm Optimal @BBL P1P", 2),
            ["P1S"] = ("0.16mm Optimal @BBL P1P", 2),
            ["X1C"] = ("0.16mm Optimal @BBL X1C", 2),
            ["H2D"] = ("0.16mm Standard @BBL H2D", 7),
        };

    /// <summary>
    /// Printers with no profiles of their own, and whose they use instead.
    /// </summary>
    /// <remarks>
    /// Declared rather than worked out from the names. Inferring it - "the base does not mention
    /// this printer, so it must be borrowed" - gets the A1 mini wrong, because its own profiles are
    /// named A1M.
    /// </remarks>
    private static readonly Dictionary<string, string> Borrowings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["P1S"] = "P1P",
    };

    /// <summary>Whose profile this printer borrows, or null when it has its own.</summary>
    public static string? BorrowedFrom(string? printer) =>
        Key(printer) is { } key && Borrowings.TryGetValue(key, out var lender) ? lender : null;

    /// <summary>The base this printer's preset sits on, or null when there is none to sit on.</summary>
    public static string? BaseFor(string? printer) =>
        Entry(printer)?.Base;

    /// <summary>
    /// The preset, or null for a printer with no known base.
    /// </summary>
    /// <remarks>
    /// Null rather than a guess: a preset whose inherited name does not exist fails to import, and
    /// a failed import looks like a broken tool where an absent file only looks like a quiet one.
    /// </remarks>
    public static string? For(string? printer)
    {
        if (Entry(printer) is not { } machine)
        {
            return null;
        }

        var preset = new JsonObject
        {
            ["type"] = "process",
            ["name"] = $"Lego2STL 0.16mm @BBL {printer!.Trim()}",
            ["from"] = "User",
            ["is_custom_defined"] = "1",
            ["inherits"] = machine.Base,

            // Every value is a string, which is how the slicer writes its own.
            ["enable_support"] = "0",
            ["brim_type"] = "auto_brim",
            ["elefant_foot_compensation"] = "0.15",
            ["wall_loops"] = "3",
            ["top_shell_layers"] = "5",
            ["bottom_shell_layers"] = "5",
            ["sparse_infill_density"] = "15%",
            ["sparse_infill_pattern"] = "gyroid",

            // A per-extruder setting takes one value per slot its base takes, and a small
            // perimeter is stated as a share of the outer wall because that is the only form the
            // slicer's own profiles ever give it.
            ["outer_wall_speed"] = PerSlot("35", machine.Slots),
            ["small_perimeter_speed"] = PerSlot("70%", machine.Slots),
        };

        return preset.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonArray PerSlot(string value, int slots) =>
        [.. Enumerable.Repeat(0, slots).Select(_ => JsonValue.Create(value))];

    private static (string Base, int Slots)? Entry(string? printer) =>
        Key(printer) is { } key && Bases.TryGetValue(key, out var found) ? found : null;

    /// <summary>The name with its spaces closed up, so "A1 mini" finds the A1 mini.</summary>
    private static string? Key(string? printer) =>
        string.IsNullOrWhiteSpace(printer)
            ? null
            : printer.Replace(" ", string.Empty, StringComparison.Ordinal);
}

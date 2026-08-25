using Lego2STL.Core.Text;

namespace Lego2STL.Core.Colors;

/// <summary>
/// A colour's name in the language the tool is speaking.
/// </summary>
/// <remarks>
/// <para>
/// The cross-reference carries Rebrickable's names, and those are English. They are also what
/// everything inside the tool holds: a parts list entry, a plate and a report all keep the
/// English name and it is translated at the moment it is written or shown. One canonical form
/// is what lets a list written in Italian be read back anywhere and come out in the language
/// asked for, rather than staying in the one it happened to be written in.
/// </para>
/// <para>
/// A colour with no wording in a language comes back as it stands rather than blank, so a
/// cross-reference refreshed with a new colour upstream still names it. The suite checks every
/// table against the shipped cross-reference, which turns that gap into a build failure here
/// instead of a surprise in someone's parts list.
/// </para>
/// </remarks>
public static class ColorNames
{
    /// <summary>The language the stored names are already in.</summary>
    public const DisplayLanguage Canonical = DisplayLanguage.English;

    private static readonly Dictionary<DisplayLanguage, IReadOnlyDictionary<string, string>> Tables =
        new()
        {
            [DisplayLanguage.Italian] = ItalianNames.Table,
        };

    /// <summary>
    /// Every wording back to the name the tool stores. Built from the tables rather than kept
    /// by hand, so a translation cannot be recognised in one direction and not the other.
    /// </summary>
    private static readonly Lazy<Dictionary<string, string>> Canonicalise = new(() =>
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in Tables.Values)
        {
            foreach (var (canonical, wording) in table)
            {
                map.TryAdd(canonical, canonical);
                map.TryAdd(wording, canonical);
            }
        }

        return map;
    });

    /// <summary>The colour's name as this language says it.</summary>
    public static string For(DisplayLanguage language, string canonicalName)
    {
        ArgumentNullException.ThrowIfNull(canonicalName);

        return Tables.TryGetValue(language, out var table)
               && table.TryGetValue(canonicalName, out var wording)
            ? wording
            : canonicalName;
    }

    /// <summary>
    /// The name the tool stores, given a name in any language it speaks. Anything it does not
    /// recognise - a colour named by hand in a parts list, say - is kept as it was written.
    /// </summary>
    public static string ToCanonical(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();
        return Canonicalise.Value.TryGetValue(trimmed, out var canonical) ? canonical : trimmed;
    }

    /// <summary>
    /// Whether this language has a wording of its own for a colour. Always true of the
    /// canonical language, whose wording is the stored name.
    /// </summary>
    public static bool Knows(DisplayLanguage language, string canonicalName) =>
        language == Canonical
        || (Tables.TryGetValue(language, out var table) && table.ContainsKey(canonicalName));

    /// <summary>
    /// Reached through a property so the table is set up on its own, on first use, whatever
    /// order the rest of this type is initialised in.
    /// </summary>
    private static class ItalianNames
    {
        internal static readonly Dictionary<string, string> Table = new(StringComparer.OrdinalIgnoreCase)
        {
            ["[Unknown]"] = "[Sconosciuto]",
            ["Black"] = "Nero",
            ["Blue"] = "Blu",
            ["Green"] = "Verde",
            ["Dark Turquoise"] = "Turchese Scuro",
            ["Red"] = "Rosso",
            ["Dark Pink"] = "Rosa Scuro",
            ["Brown"] = "Marrone",
            ["Light Gray"] = "Grigio Chiaro",
            ["Dark Gray"] = "Grigio Scuro",
            ["Light Blue"] = "Blu Chiaro",
            ["Bright Green"] = "Verde Brillante",
            ["Light Turquoise"] = "Turchese Chiaro",
            ["Salmon"] = "Salmone",
            ["Pink"] = "Rosa",
            ["Yellow"] = "Giallo",
            ["White"] = "Bianco",
            ["Light Green"] = "Verde Chiaro",
            ["Light Yellow"] = "Giallo Chiaro",
            ["Tan"] = "Beige",
            ["Light Violet"] = "Violetto Chiaro",
            ["Glow In Dark Opaque"] = "Fosforescente Opaco",
            ["Purple"] = "Viola",
            ["Dark Blue-Violet"] = "Blu-Viola Scuro",
            ["Orange"] = "Arancione",
            ["Magenta"] = "Magenta",
            ["Lime"] = "Lime",
            ["Dark Tan"] = "Beige Scuro",
            ["Bright Pink"] = "Rosa Brillante",
            ["Medium Lavender"] = "Lavanda Medio",
            ["Lavender"] = "Lavanda",
            ["Trans-Black IR Lens"] = "Trasparente Nero Lente IR",
            ["Trans-Dark Blue"] = "Trasparente Blu Scuro",
            ["Trans-Green"] = "Trasparente Verde",
            ["Trans-Bright Green"] = "Trasparente Verde Brillante",
            ["Trans-Red"] = "Trasparente Rosso",
            ["Trans-Brown"] = "Trasparente Marrone",
            ["Trans-Light Blue"] = "Trasparente Blu Chiaro",
            ["Trans-Neon Green"] = "Trasparente Verde Neon",
            ["Trans-Very Lt Blue"] = "Trasparente Blu Molto Chiaro",
            ["Trans-Dark Pink"] = "Trasparente Rosa Scuro",
            ["Trans-Yellow"] = "Trasparente Giallo",
            ["Trans-Clear"] = "Trasparente Incolore",
            ["Trans-Purple"] = "Trasparente Viola",
            ["Trans-Neon Yellow"] = "Trasparente Giallo Neon",
            ["Trans-Neon Orange"] = "Trasparente Arancione Neon",
            ["Chrome Antique Brass"] = "Cromato Ottone Antico",
            ["Chrome Blue"] = "Cromato Blu",
            ["Chrome Green"] = "Cromato Verde",
            ["Chrome Pink"] = "Cromato Rosa",
            ["Chrome Black"] = "Cromato Nero",
            ["Very Light Orange"] = "Arancione Molto Chiaro",
            ["Light Purple"] = "Viola Chiaro",
            ["Reddish Brown"] = "Marrone Rossastro",
            ["Light Bluish Gray"] = "Grigio Bluastro Chiaro",
            ["Dark Bluish Gray"] = "Grigio Bluastro Scuro",
            ["Medium Blue"] = "Blu Medio",
            ["Medium Green"] = "Verde Medio",
            ["Speckle Black-Copper"] = "Screziato Nero-Rame",
            ["Speckle DBGray-Silver"] = "Screziato Grigio Scuro-Argento",
            ["Light Pink"] = "Rosa Chiaro",
            ["Light Nougat"] = "Nougat Chiaro",
            ["Milky White"] = "Bianco Latteo",
            ["Metallic Silver"] = "Argento Metallizzato",
            ["Metallic Green"] = "Verde Metallizzato",
            ["Metallic Gold"] = "Oro Metallizzato",
            ["Medium Nougat"] = "Nougat Medio",
            ["Dark Purple"] = "Viola Scuro",
            ["Light Brown"] = "Marrone Chiaro",
            ["Royal Blue"] = "Blu Reale",
            ["Nougat"] = "Nougat",
            ["Light Salmon"] = "Salmone Chiaro",
            ["Violet"] = "Violetto",
            ["Medium Bluish Violet"] = "Violetto Bluastro Medio",
            ["Glitter Trans-Dark Pink"] = "Trasparente Glitterato Rosa Scuro",
            ["Medium Lime"] = "Lime Medio",
            ["Glitter Trans-Clear"] = "Trasparente Glitterato Incolore",
            ["Aqua"] = "Acquamarina",
            ["Light Lime"] = "Lime Chiaro",
            ["Light Orange"] = "Arancione Chiaro",
            ["Glitter Trans-Purple"] = "Trasparente Glitterato Viola",
            ["Speckle Black-Silver"] = "Screziato Nero-Argento",
            ["Speckle Black-Gold"] = "Screziato Nero-Oro",
            ["Copper"] = "Rame",
            ["Pearl Light Gray"] = "Grigio Chiaro Perlato",
            ["Pearl Sand Blue"] = "Blu Sabbia Perlato",
            ["Pearl Light Gold"] = "Oro Chiaro Perlato",
            ["Trans-Medium Blue"] = "Trasparente Blu Medio",
            ["Pearl Dark Gray"] = "Grigio Scuro Perlato",
            ["Pearl Very Light Gray"] = "Grigio Molto Chiaro Perlato",
            ["Very Light Bluish Gray"] = "Grigio Bluastro Molto Chiaro",
            ["Yellowish Green"] = "Verde Giallastro",
            ["Flat Dark Gold"] = "Oro Scuro Opaco",
            ["Flat Silver"] = "Argento Opaco",
            ["Trans-Orange"] = "Trasparente Arancione",
            ["Pearl White"] = "Bianco Perlato",
            ["Bright Light Orange"] = "Arancione Chiaro Brillante",
            ["Bright Light Blue"] = "Blu Chiaro Brillante",
            ["Rust"] = "Ruggine",
            ["Bright Light Yellow"] = "Giallo Chiaro Brillante",
            ["Trans-Pink"] = "Trasparente Rosa",
            ["Sky Blue"] = "Celeste",
            ["Trans-Light Purple"] = "Trasparente Viola Chiaro",
            ["Dark Blue"] = "Blu Scuro",
            ["Dark Green"] = "Verde Scuro",
            ["Glow In Dark Trans"] = "Fosforescente Trasparente",
            ["Pearl Gold"] = "Oro Perlato",
            ["Dark Brown"] = "Marrone Scuro",
            ["Maersk Blue"] = "Blu Maersk",
            ["Dark Red"] = "Rosso Scuro",
            ["Dark Azure"] = "Azzurro Scuro",
            ["Medium Azure"] = "Azzurro Medio",
            ["Light Aqua"] = "Acquamarina Chiaro",
            ["Olive Green"] = "Verde Oliva",
            ["Chrome Gold"] = "Cromato Oro",
            ["Sand Red"] = "Rosso Sabbia",
            ["Medium Dark Pink"] = "Rosa Medio Scuro",
            ["Earth Orange"] = "Arancione Terra",
            ["Sand Purple"] = "Viola Sabbia",
            ["Sand Green"] = "Verde Sabbia",
            ["Sand Blue"] = "Blu Sabbia",
            ["Chrome Silver"] = "Cromato Argento",
            ["Fabuland Brown"] = "Marrone Fabuland",
            ["Medium Orange"] = "Arancione Medio",
            ["Dark Orange"] = "Arancione Scuro",
            ["Very Light Gray"] = "Grigio Molto Chiaro",
            ["Glow in Dark White"] = "Bianco Fosforescente",
            ["Medium Violet"] = "Violetto Medio",
            ["Glitter Trans-Neon Green"] = "Trasparente Glitterato Verde Neon",
            ["Glitter Trans-Light Blue"] = "Trasparente Glitterato Blu Chiaro",
            ["Trans-Flame Yellowish Orange"] = "Trasparente Arancione Giallastro Fiamma",
            ["Trans-Fire Yellow"] = "Trasparente Giallo Fuoco",
            ["Trans-Light Royal Blue"] = "Trasparente Blu Reale Chiaro",
            ["Reddish Lilac"] = "Lilla Rossastro",
            ["Vintage Blue"] = "Blu Vintage",
            ["Vintage Green"] = "Verde Vintage",
            ["Vintage Red"] = "Rosso Vintage",
            ["Vintage Yellow"] = "Giallo Vintage",
            ["Fabuland Orange"] = "Arancione Fabuland",
            ["Modulex White"] = "Bianco Modulex",
            ["Modulex Light Bluish Gray"] = "Grigio Bluastro Chiaro Modulex",
            ["Modulex Light Gray"] = "Grigio Chiaro Modulex",
            ["Modulex Charcoal Gray"] = "Grigio Antracite Modulex",
            ["Modulex Tile Gray"] = "Grigio Piastrella Modulex",
            ["Modulex Black"] = "Nero Modulex",
            ["Modulex Tile Brown"] = "Marrone Piastrella Modulex",
            ["Modulex Terracotta"] = "Terracotta Modulex",
            ["Modulex Brown"] = "Marrone Modulex",
            ["Modulex Buff"] = "Camoscio Modulex",
            ["Modulex Red"] = "Rosso Modulex",
            ["Modulex Pink Red"] = "Rosso Rosato Modulex",
            ["Modulex Orange"] = "Arancione Modulex",
            ["Modulex Light Orange"] = "Arancione Chiaro Modulex",
            ["Modulex Light Yellow"] = "Giallo Chiaro Modulex",
            ["Modulex Ochre Yellow"] = "Giallo Ocra Modulex",
            ["Modulex Lemon"] = "Limone Modulex",
            ["Modulex Pastel Green"] = "Verde Pastello Modulex",
            ["Modulex Olive Green"] = "Verde Oliva Modulex",
            ["Modulex Aqua Green"] = "Verde Acqua Modulex",
            ["Modulex Teal Blue"] = "Blu Petrolio Modulex",
            ["Modulex Tile Blue"] = "Blu Piastrella Modulex",
            ["Modulex Medium Blue"] = "Blu Medio Modulex",
            ["Modulex Pastel Blue"] = "Blu Pastello Modulex",
            ["Modulex Violet"] = "Violetto Modulex",
            ["Modulex Pink"] = "Rosa Modulex",
            ["Modulex Clear"] = "Incolore Modulex",
            ["Modulex Foil Dark Gray"] = "Grigio Scuro Laminato Modulex",
            ["Modulex Foil Light Gray"] = "Grigio Chiaro Laminato Modulex",
            ["Modulex Foil Dark Green"] = "Verde Scuro Laminato Modulex",
            ["Modulex Foil Light Green"] = "Verde Chiaro Laminato Modulex",
            ["Modulex Foil Dark Blue"] = "Blu Scuro Laminato Modulex",
            ["Modulex Foil Light Blue"] = "Blu Chiaro Laminato Modulex",
            ["Modulex Foil Violet"] = "Violetto Laminato Modulex",
            ["Modulex Foil Red"] = "Rosso Laminato Modulex",
            ["Modulex Foil Yellow"] = "Giallo Laminato Modulex",
            ["Modulex Foil Orange"] = "Arancione Laminato Modulex",
            ["Coral"] = "Corallo",
            ["Pastel Blue"] = "Blu Pastello",
            ["Glitter Trans-Orange"] = "Trasparente Glitterato Arancione",
            ["Opal Trans-Light Blue"] = "Trasparente Opalescente Blu Chiaro",
            ["Opal Trans-Dark Pink"] = "Trasparente Opalescente Rosa Scuro",
            ["Opal Trans-Clear"] = "Trasparente Opalescente Incolore",
            ["Opal Trans-Brown"] = "Trasparente Opalescente Marrone",
            ["Trans-Light Bright Green"] = "Trasparente Verde Brillante Chiaro",
            ["Trans-Light Green"] = "Trasparente Verde Chiaro",
            ["Opal Trans-Purple"] = "Trasparente Opalescente Viola",
            ["Opal Trans-Bright Green"] = "Trasparente Opalescente Verde Brillante",
            ["Opal Trans-Dark Blue"] = "Trasparente Opalescente Blu Scuro",
            ["Vibrant Yellow"] = "Giallo Vivace",
            ["Pearl Copper"] = "Rame Perlato",
            ["Fabuland Red"] = "Rosso Fabuland",
            ["Reddish Gold"] = "Oro Rossastro",
            ["Curry"] = "Curry",
            ["Dark Nougat"] = "Nougat Scuro",
            ["Bright Reddish Orange"] = "Arancione Rossastro Brillante",
            ["Pearl Red"] = "Rosso Perlato",
            ["Pearl Blue"] = "Blu Perlato",
            ["Pearl Green"] = "Verde Perlato",
            ["Pearl Brown"] = "Marrone Perlato",
            ["Pearl Black"] = "Nero Perlato",
            ["Duplo Blue"] = "Blu Duplo",
            ["Duplo Medium Blue"] = "Blu Medio Duplo",
            ["Duplo Lime"] = "Lime Duplo",
            ["Fabuland Lime"] = "Lime Fabuland",
            ["Duplo Medium Green"] = "Verde Medio Duplo",
            ["Duplo Light Green"] = "Verde Chiaro Duplo",
            ["Light Tan"] = "Beige Chiaro",
            ["Rust Orange"] = "Arancione Ruggine",
            ["Clikits Pink"] = "Rosa Clikits",
            ["Two-tone Copper"] = "Rame Bicolore",
            ["Two-tone Gold"] = "Oro Bicolore",
            ["Two-tone Silver"] = "Argento Bicolore",
            ["Pearl Lime"] = "Lime Perlato",
            ["Duplo Pink"] = "Rosa Duplo",
            ["Medium Brown"] = "Marrone Medio",
            ["Warm Tan"] = "Beige Caldo",
            ["Duplo Turquoise"] = "Turchese Duplo",
            ["Warm Yellowish Orange"] = "Arancione Giallastro Caldo",
            ["Metallic Copper"] = "Rame Metallizzato",
            ["Light Lilac"] = "Lilla Chiaro",
            ["Trans-Medium Purple"] = "Trasparente Viola Medio",
            ["Trans-Black"] = "Trasparente Nero",
            ["Glitter Trans-Bright Green"] = "Trasparente Glitterato Verde Brillante",
            ["Glitter Trans-Medium Purple"] = "Trasparente Glitterato Viola Medio",
            ["Glitter Trans-Green"] = "Trasparente Glitterato Verde",
            ["Glitter Trans-Pink"] = "Trasparente Glitterato Rosa",
            ["Clikits Yellow"] = "Giallo Clikits",
            ["Duplo Dark Purple"] = "Viola Scuro Duplo",
            ["Trans-Neon Red"] = "Trasparente Rosso Neon",
            ["Pearl Titanium"] = "Titanio Perlato",
            ["HO Aqua"] = "Acquamarina HO",
            ["HO Azure"] = "Azzurro HO",
            ["HO Blue-gray"] = "Grigio-Blu HO",
            ["HO Cyan"] = "Ciano HO",
            ["HO Dark Aqua"] = "Acquamarina Scuro HO",
            ["HO Dark Blue"] = "Blu Scuro HO",
            ["HO Dark Gray"] = "Grigio Scuro HO",
            ["HO Dark Green"] = "Verde Scuro HO",
            ["HO Dark Lime"] = "Lime Scuro HO",
            ["HO Dark Red"] = "Rosso Scuro HO",
            ["HO Dark Sand Green"] = "Verde Sabbia Scuro HO",
            ["HO Dark Turquoise"] = "Turchese Scuro HO",
            ["HO Earth Orange"] = "Arancione Terra HO",
            ["HO Gold"] = "Oro HO",
            ["HO Light Aqua"] = "Acquamarina Chiaro HO",
            ["HO Light Brown"] = "Marrone Chiaro HO",
            ["HO Light Gold"] = "Oro Chiaro HO",
            ["HO Light Tan"] = "Beige Chiaro HO",
            ["HO Light Yellow"] = "Giallo Chiaro HO",
            ["HO Medium Blue"] = "Blu Medio HO",
            ["HO Medium Red"] = "Rosso Medio HO",
            ["HO Metallic Blue"] = "Blu Metallizzato HO",
            ["HO Metallic Dark Gray"] = "Grigio Scuro Metallizzato HO",
            ["HO Metallic Green"] = "Verde Metallizzato HO",
            ["HO Metallic Sand Blue"] = "Blu Sabbia Metallizzato HO",
            ["HO Olive Green"] = "Verde Oliva HO",
            ["HO Rose"] = "Rosa HO",
            ["HO Sand Blue"] = "Blu Sabbia HO",
            ["HO Sand Green"] = "Verde Sabbia HO",
            ["HO Tan"] = "Beige HO",
            ["HO Titanium"] = "Titanio HO",
            ["Metal"] = "Metallo",
            ["Reddish Orange"] = "Arancione Rossastro",
            ["Sienna Brown"] = "Marrone Siena",
            ["Umber Brown"] = "Marrone Ombra",
            ["Opal Trans-Yellow"] = "Trasparente Opalescente Giallo",
            ["Neon Orange"] = "Arancione Neon",
            ["Neon Green"] = "Verde Neon",
            ["Dark Olive Green"] = "Verde Oliva Scuro",
            ["Glitter Milky White"] = "Bianco Latteo Glitterato",
            ["Chrome Red"] = "Cromato Rosso",
            ["Ochre Yellow"] = "Giallo Ocra",
            ["Warm Pink"] = "Rosa Caldo",
            ["Blue Violet"] = "Blu Violetto",
            ["[No Color/Any Color]"] = "[Nessun colore/Qualsiasi colore]",
        };
    }
}

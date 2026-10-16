using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using Lego2STL.Core.Colors;

namespace Lego2STL.Core.Plates;

/// <summary>A shape in the combined file, and which plate its copies sit on.</summary>
internal sealed record ProjectObject(int Id, string Name, int Plate, int Instances);

/// <summary>
/// The combined plate file as a Bambu Studio or OrcaSlicer project, each colour plate one of the
/// slicer's own plates (#12).
/// </summary>
/// <remarks>
/// It names system presets and states nothing about them, so the slicer takes them from the user's
/// own installation; parts sit where the slicer lays out its plates, since that decides which plate
/// they are on.
/// </remarks>
public sealed class SlicerProject
{
    /// <summary>Both slicers stop at this many plates.</summary>
    public const int PlateLimit = 36;

    /// <summary>The gap the slicers leave between plates, as a share of the bed.</summary>
    private const float PlateGap = 1f / 5f;

    /// <summary>Bambu Studio 2.0's format: no 2.x release calls it newer, and no 1.x conversion applies.</summary>
    private const string BambuFormatVersion = "02.00.00.00";

    /// <summary>The first OrcaSlicer to mark its own projects as Orca's, which is what this claims.</summary>
    private const string OrcaFormatVersion = "2.3.2";

    /// <summary>Every printer the tool knows ships with this plate, so it is the one the project asks for.</summary>
    private const string BedType = "Textured PEI Plate";

    private sealed record Presets(
        string Printer, string Model, string Process, string Filament, int Extruders = 1);

    /// <summary>The 0.4 mm system presets for each printer; the P1S has no process of its own and uses the X1C's.</summary>
    private static readonly Dictionary<string, Presets> Bambu = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A1"] = new("Bambu Lab A1 0.4 nozzle", "Bambu Lab A1", "0.16mm Optimal @BBL A1", "Bambu PLA Basic @BBL A1"),
        ["A1mini"] = new("Bambu Lab A1 mini 0.4 nozzle", "Bambu Lab A1 mini", "0.16mm Optimal @BBL A1M", "Bambu PLA Basic @BBL A1M"),
        ["P1P"] = new("Bambu Lab P1P 0.4 nozzle", "Bambu Lab P1P", "0.16mm Optimal @BBL P1P", "Bambu PLA Basic @BBL P1P"),
        ["P1S"] = new("Bambu Lab P1S 0.4 nozzle", "Bambu Lab P1S", "0.16mm Optimal @BBL X1C", "Bambu PLA Basic @BBL P1S 0.4 nozzle"),
        ["X1C"] = new("Bambu Lab X1 Carbon 0.4 nozzle", "Bambu Lab X1 Carbon", "0.16mm Optimal @BBL X1C", "Bambu PLA Basic @BBL X1C"),
        ["H2D"] = new("Bambu Lab H2D 0.4 nozzle", "Bambu Lab H2D", "0.16mm Standard @BBL H2D", "Bambu PLA Basic @BBL H2D", 2),
    };

    /// <summary>OrcaSlicer ships the same profiles, except that its P1S uses the X1C's filament.</summary>
    private static readonly Dictionary<string, Presets> Orca = new(Bambu, StringComparer.OrdinalIgnoreCase)
    {
        ["P1S"] = Bambu["P1S"] with { Filament = "Bambu PLA Basic @BBL X1C" },
    };

    private readonly Presets _presets;

    private SlicerProject(ProjectFormat format, PrintBed bed, Presets presets)
    {
        Format = format;
        Bed = bed;
        _presets = presets;
    }

    public ProjectFormat Format { get; }

    /// <summary>The printer's own bed, which is what the slicer sizes its plates by.</summary>
    public PrintBed Bed { get; }

    /// <summary>How many filaments a project may carry: 32 in Bambu Studio, 16 in OrcaSlicer.</summary>
    public int FilamentLimit => Format == ProjectFormat.OrcaSlicer ? 16 : 32;

    /// <summary>
    /// The project, or null for a plain 3MF: asked for, no presets to name, or plates packed for a
    /// bed other than the printer's own, which is what a plate size for another machine means.
    /// </summary>
    public static SlicerProject? For(ProjectFormat format, string? printer, PrintBed packedOn)
    {
        ArgumentNullException.ThrowIfNull(packedOn);

        var table = format switch
        {
            ProjectFormat.BambuStudio => Bambu,
            ProjectFormat.OrcaSlicer => Orca,
            _ => null,
        };

        if (table is null
            || !PrintBeds.TryGetByName(printer, out var bed)
            || !table.TryGetValue(bed.Name.Replace(" ", string.Empty, StringComparison.Ordinal), out var presets)
            || Math.Abs(packedOn.Width - bed.Width) > 0.01f
            || Math.Abs(packedOn.Depth - bed.Depth) > 0.01f)
        {
            return null;
        }

        return new SlicerProject(format, bed, presets);
    }

    /// <summary>Where the slicer puts the near-left corner of plate <paramref name="index"/> of <paramref name="count"/>.</summary>
    public Vector2 PlateOrigin(int index, int count)
    {
        var columns = Columns(count);

        return new Vector2(
            index % columns * Bed.Width * (1 + PlateGap),
            -(index / columns) * Bed.Depth * (1 + PlateGap));
    }

    /// <summary>The slicers' own column count: the square root, rounded up.</summary>
    internal static int Columns(int count)
    {
        var root = MathF.Sqrt(Math.Max(count, 1));
        var rounded = MathF.Round(root, MidpointRounding.AwayFromZero);

        return (int)(root > rounded ? rounded + 1 : rounded);
    }

    /// <summary>The metadata that tells the slicer the file is one of its projects.</summary>
    internal IEnumerable<(string Name, string Value)> ModelMetadata()
    {
        yield return ("Application", "BambuStudio-" + BambuFormatVersion);
        yield return ("BambuStudio:3mfVersion", "1");

        if (Format == ProjectFormat.OrcaSlicer)
        {
            yield return ("OrcaSlicer", OrcaFormatVersion);
        }
    }

    /// <summary>One filament per colour, and each plate's; past the limit a colour takes its nearest one's.</summary>
    internal (IReadOnlyList<Rgb24> Filaments, IReadOnlyList<int> PlateFilament) Filaments(
        IReadOnlyList<PlateContents> plates)
    {
        var filaments = new List<Rgb24>();
        var chosen = new List<int>(plates.Count);

        foreach (var plate in plates)
        {
            var index = filaments.IndexOf(plate.Rgb);

            if (index < 0 && filaments.Count < FilamentLimit)
            {
                filaments.Add(plate.Rgb);
                index = filaments.Count - 1;
            }
            else if (index < 0)
            {
                index = Nearest(filaments, plate.Rgb);
            }

            chosen.Add(index);
        }

        return (filaments, chosen);
    }

    private static int Nearest(List<Rgb24> colours, Rgb24 wanted) =>
        colours
            .Select((colour, index) => (index, distance: Square(colour.R - wanted.R)
                                                          + Square(colour.G - wanted.G)
                                                          + Square(colour.B - wanted.B)))
            .MinBy(pair => pair.distance)
            .index;

    private static int Square(int value) => value * value;

    /// <summary>The plates, named after their colour, and the filament each object prints in.</summary>
    internal string ModelSettings(
        IReadOnlyList<PlateContents> plates,
        IReadOnlyList<ProjectObject> objects,
        IReadOnlyList<int> plateFilament)
    {
        var names = PlateNames(plates);

        return ThreeMfWriter.WriteXml(w =>
        {
            w.WriteStartElement("config");

            foreach (var part in objects)
            {
                w.WriteStartElement("object");
                w.WriteAttributeString("id", Number(part.Id));
                Metadata(w, "name", part.Name);
                Metadata(w, "extruder", Number(plateFilament[part.Plate] + 1));
                w.WriteEndElement();
            }

            var identity = 0;

            for (var p = 0; p < plates.Count; p++)
            {
                w.WriteStartElement("plate");
                Metadata(w, "plater_id", Number(p + 1));
                Metadata(w, "plater_name", names[p]);
                Metadata(w, "locked", "false");

                foreach (var part in objects.Where(o => o.Plate == p))
                {
                    for (var instance = 0; instance < part.Instances; instance++)
                    {
                        w.WriteStartElement("model_instance");
                        Metadata(w, "object_id", Number(part.Id));
                        Metadata(w, "instance_id", Number(instance));
                        Metadata(w, "identify_id", Number(++identity));
                        w.WriteEndElement();
                    }
                }

                w.WriteEndElement();
            }

            w.WriteEndElement();
        });
    }

    /// <summary>
    /// The presets by name and the colour of each filament, and nothing else.
    /// </summary>
    /// <remarks>
    /// Every "different settings" entry is left empty: the slicer then takes each named preset
    /// exactly as the user's installation has it, and opens it unmodified.
    /// </remarks>
    internal string ProjectSettings(IReadOnlyList<Rgb24> filaments)
    {
        var count = filaments.Count;

        var settings = new JsonObject
        {
            ["curr_bed_type"] = BedType,
            ["different_settings_to_system"] = Strings(Enumerable.Repeat(string.Empty, count + 2)),
            ["filament_colour"] = Strings(filaments.Select(Hex)),

            // The slicers cut every filament list to this one's length, preset names included.
            ["filament_diameter"] = Strings(Enumerable.Repeat("1.75", count)),
            ["filament_settings_id"] = Strings(Enumerable.Repeat(_presets.Filament, count)),
            ["from"] = "project",
            ["inherits_group"] = Strings(Enumerable.Repeat(string.Empty, count + 2)),
            ["name"] = "project_settings",
            ["nozzle_diameter"] = Strings(Enumerable.Repeat("0.4", _presets.Extruders)),
            ["print_settings_id"] = _presets.Process,
            ["printer_model"] = _presets.Model,
            ["printer_settings_id"] = _presets.Printer,
            ["printer_technology"] = "FFF",
            ["version"] = BambuFormatVersion,
        };

        return settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>A colour's name, numbered when it fills more than one plate.</summary>
    private static List<string> PlateNames(IReadOnlyList<PlateContents> plates)
    {
        var totals = plates
            .GroupBy(p => p.ColorName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);

        return
        [
            .. plates.Select(plate =>
            {
                if (totals[plate.ColorName] == 1)
                {
                    return plate.ColorName;
                }

                seen[plate.ColorName] = seen.GetValueOrDefault(plate.ColorName) + 1;
                return string.Create(CultureInfo.InvariantCulture, $"{plate.ColorName} {seen[plate.ColorName]}");
            }),
        ];
    }

    private static JsonArray Strings(IEnumerable<string> values) =>
        [.. values.Select(value => JsonValue.Create(value))];

    private static string Hex(Rgb24 rgb) =>
        string.Create(CultureInfo.InvariantCulture, $"#{rgb.R:X2}{rgb.G:X2}{rgb.B:X2}");

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static void Metadata(XmlWriter w, string key, string value)
    {
        w.WriteStartElement("metadata");
        w.WriteAttributeString("key", key);
        w.WriteAttributeString("value", value);
        w.WriteEndElement();
    }
}

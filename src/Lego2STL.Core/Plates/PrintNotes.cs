using System.Text;
using Lego2STL.Core.Text;

namespace Lego2STL.Core.Plates;

/// <summary>
/// The sheet that goes beside a run's plates, saying how to print them.
/// </summary>
/// <remarks>
/// <para>
/// The same reasoning as the note the calibration command writes beside its shapes: by the time a
/// folder of files is printed, the command line that made them is long gone.
/// </para>
/// <para>
/// This is the primary deliverable and the preset beside it is the convenience. A text file is
/// still correct in five years, while a preset depends on names the slicer may rename; so the
/// sheet carries every setting, including the ones the preset also carries, and a printer with no
/// preset still gets everything it needs.
/// </para>
/// </remarks>
public static class PrintNotes
{
    /// <summary>One setting, as Bambu Studio labels it in English and in Italian, and its value.</summary>
    private sealed record Setting(string LabelEn, string LabelIt, string ValueEn, string ValueIt)
    {
        public Setting(string labelEn, string labelIt, string value)
            : this(labelEn, labelIt, value, value)
        {
        }
    }

    /// <summary>A group of settings, and the page and group the slicer shows them under.</summary>
    private sealed record Group(string PlaceEn, string PlaceIt, Setting[] Settings);

    /// <summary>
    /// The starting profile, grouped and labelled the way Bambu Studio shows it.
    /// </summary>
    /// <remarks>
    /// Literal values, because these are advice rather than anything derived: they describe a
    /// spool this tool has never seen. The labels are the slicer's own, not this tool's words,
    /// because they are what the reader is hunting for on screen - checked against Bambu Studio's
    /// PrintConfig.cpp, Tab.cpp and Italian catalogue on 2026-10-13 (#4).
    /// </remarks>
    private static readonly Group[] Profile =
    [
        new("Process > Quality > Layer height", "Processo > Qualità > Altezza layer",
        [
            new("Layer height", "Altezza layer", "0.16 mm"),
            new("Initial layer height", "Altezza layer iniziale", "0.20 mm"),
        ]),
        new("Process > Quality > Precision", "Processo > Qualità > Precisione",
        [
            new("Elephant foot compensation", "Compensazione zampa d'elefante", "0.15 mm"),
        ]),
        new("Process > Strength > Walls", "Processo > Resistenza > Pareti",
        [
            new("Wall loops", "Loop pareti", "3"),
        ]),
        new("Process > Strength > Top/bottom shells", "Processo > Resistenza > Gusci superiori/inferiori",
        [
            new("Top shell layers", "Layer guscio superiore", "5"),
            new("Bottom shell layers", "Layer guscio inferiore", "5"),
        ]),
        new("Process > Strength > Sparse infill", "Processo > Resistenza > Riempimento",
        [
            new("Sparse infill density", "Densità riempimento", "15%"),
            new("Sparse infill pattern", "Trama riempimento", "Gyroid", "Giroide"),
        ]),
        new("Process > Speed > Initial layer speed", "Processo > Velocità > Velocità layer iniziale",
        [
            new("Initial layer", "Layer iniziale", "20 mm/s"),
        ]),
        new("Process > Speed > Other layers speed", "Processo > Velocità > Velocità altri layer",
        [
            new("Outer wall", "Parete esterna", "35 mm/s"),
            new("Inner wall", "Parete interna", "50 mm/s"),
            new("Top surface", "Superficie superiore", "30 mm/s"),
            new("Small perimeters", "Perimetri piccoli", "70% (about 25 mm/s)", "70% (circa 25 mm/s)"),
            new("Sparse infill", "Riempimento", "60 mm/s"),
        ]),
        new("Process > Support > Support", "Processo > Supporto > Supporto",
        [
            new("Enable support", "Abilita supporti", "off", "disattivato"),
        ]),
        new("Process > Others > Bed adhension", "Processo > Altro > Adesione al piano",
        [
            new("Brim type", "Tipo brim", "Auto", "Automatico"),
        ]),
        new("Filament > Print temperature", "Filamento > Temperatura stampa",
        [
            new("Nozzle - Initial layer", "Nozzle - Layer iniziale", "220 C"),
            new("Nozzle - Other layers", "Nozzle - Altri layer", "215 C"),
            new("<your plate> - Initial layer", "<il tuo piano> - Layer iniziale", "60 C"),
            new("<your plate> - Other layers", "<il tuo piano> - Altri layer", "55 C"),
        ]),
        new("Filament > Volumetric speed limitation", "Filamento > Limitazione velocità volumetrica",
        [
            new("Max volumetric speed", "Massima velocità volumetrica", "10 mm3/s"),
        ]),
        new("Filament > Cooling > Part cooling fan", "Filamento > Raffreddamento > Ventola di raffreddamento oggetto",
        [
            new("Initial layer fan - For the first ... layers", "Ventola layer iniziale - Per il primo ... layers", "1"),
            new("Min/Max fan speed threshold - Fan speed", "Velocità minima/massima ventola - Velocità ventola", "100%"),
        ]),
    ];

    /// <summary>The sheet, for this printer, in these words.</summary>
    public static string Write(string? printer, Strings words)
    {
        ArgumentNullException.ThrowIfNull(words);

        var name = string.IsNullOrWhiteSpace(printer) ? "?" : printer.Trim();
        var sheet = new StringBuilder();

        var title = words[TextKey.PrintNotesTitle];
        sheet.AppendLine(title).AppendLine(new string('=', title.Length)).AppendLine();
        sheet.AppendLine(words[TextKey.PrintNotesStartingPoint]).AppendLine();

        if (ProcessPreset.BaseFor(name) is not null)
        {
            sheet.AppendLine(words.Format(TextKey.PrintNotesImport, "Lego2STL.json"));
            sheet.AppendLine(words[TextKey.PrintNotesNozzle]);

            if (ProcessPreset.BorrowedFrom(name) is { } lender)
            {
                sheet.AppendLine(words.Format(TextKey.PrintNotesBorrowedProfile, name, lender));
            }
        }
        else
        {
            sheet.AppendLine(words.Format(TextKey.PrintNotesNoPreset, name));
        }

        sheet.AppendLine().Append(Settings(words));

        sheet.AppendLine().AppendLine(words[TextKey.PrintNotesCalibration]).AppendLine();
        sheet.AppendLine(words[TextKey.PrintNotesCalibrationSteps]);

        return sheet.ToString();
    }

    /// <summary>
    /// The starting profile on its own, for a sheet that is not this one.
    /// </summary>
    /// <remarks>
    /// A calibration folder keeps one sheet rather than two, so it composes this into a document
    /// of its own instead of getting a second file beside it.
    /// </remarks>
    public static string Settings(Strings words)
    {
        ArgumentNullException.ThrowIfNull(words);

        var block = new StringBuilder();
        block.AppendLine(words[TextKey.PrintNotesSettings]).AppendLine();

        var italian = words.Language == DisplayLanguage.Italian;
        block.AppendLine(words[TextKey.PrintNotesWhereToFind]);

        var width = Profile.SelectMany(g => g.Settings)
            .Max(x => (italian ? x.LabelIt : x.LabelEn).Length) + 2;

        foreach (var group in Profile)
        {
            block.AppendLine().Append("  ").AppendLine(italian ? group.PlaceIt : group.PlaceEn);

            foreach (var x in group.Settings)
            {
                block.Append("    ")
                    .Append((italian ? x.LabelIt : x.LabelEn).PadRight(width))
                    .AppendLine(italian ? x.ValueIt : x.ValueEn);
            }
        }

        return block.ToString();
    }
}

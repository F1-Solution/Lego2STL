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
    /// <summary>
    /// The starting profile.
    /// </summary>
    /// <remarks>
    /// Literal values, because these are advice rather than anything derived: they describe a
    /// spool this tool has never seen. The setting names are left in the slicer's own English
    /// because they are what the reader is hunting for on screen.
    /// </remarks>
    private static readonly (string Setting, string Value)[] Profile =
    [
        ("Nozzle temperature", "215 C"),
        ("Nozzle temperature, first layer", "220 C"),
        ("Bed temperature", "55 C"),
        ("Bed temperature, first layer", "60 C"),
        ("Part cooling fan", "0% first layer, 100% from the third"),
        ("Layer height", "0.16 mm"),
        ("First layer height", "0.20 mm"),
        ("First layer speed", "20 mm/s"),
        ("Outer wall speed", "35 mm/s"),
        ("Inner wall speed", "50 mm/s"),
        ("Top surface speed", "30 mm/s"),
        ("Small perimeter speed", "70% of the outer wall, about 25 mm/s"),
        ("Sparse infill speed", "60 mm/s"),
        ("Max volumetric speed", "10 mm3/s"),
        ("Walls", "3"),
        ("Top shell layers", "5"),
        ("Bottom shell layers", "5"),
        ("Sparse infill", "15% gyroid"),
        ("Elephant foot compensation", "0.15 mm"),
        ("Brim", "auto"),
        ("Supports", "off"),
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

        sheet.AppendLine().AppendLine(words[TextKey.PrintNotesSettings]).AppendLine();

        foreach (var (setting, value) in Profile)
        {
            sheet.Append("  ").Append(setting.PadRight(34)).AppendLine(value);
        }

        sheet.AppendLine().AppendLine(words[TextKey.PrintNotesCalibration]).AppendLine();
        sheet.AppendLine(words[TextKey.PrintNotesCalibrationSteps]);

        return sheet.ToString();
    }
}

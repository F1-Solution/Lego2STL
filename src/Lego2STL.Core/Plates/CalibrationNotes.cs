using System.Globalization;
using System.Text;
using Lego2STL.Core.Text;

namespace Lego2STL.Core.Plates;

/// <summary>
/// The single sheet beside a calibration plate.
/// </summary>
/// <remarks>
/// <para>
/// One sheet and not two. A build plate's folder gets how-to-print.txt beside its preset; a
/// calibration folder keeps its own sheet and that sheet carries the print settings as well,
/// because leaving two overlapping instruction files in one folder is the confusion the note was
/// written to prevent in the first place.
/// </para>
/// <para>
/// The map is built from where the packer actually put things. The packer sorts by depth, then
/// width, then label, and a clearance changes a footprint - so the order the pieces were handed
/// over is not the order they sit in, and a map that assumed it would send someone to the wrong
/// piece, which they would then measure and believe.
/// </para>
/// </remarks>
public static class CalibrationNotes
{
    /// <summary>Placements within this many millimetres of each other count as one row.</summary>
    private const float SameRow = 1f;

    public static string Write(
        PackedPlate plate,
        IReadOnlyList<string> missing,
        string? printer,
        Strings words)
    {
        ArgumentNullException.ThrowIfNull(plate);
        ArgumentNullException.ThrowIfNull(missing);
        ArgumentNullException.ThrowIfNull(words);

        var sheet = new StringBuilder();

        sheet.AppendLine(words[TextKey.CalibrationTitle]);
        sheet.AppendLine(new string('-', 70)).AppendLine();
        sheet.AppendLine(words[TextKey.CalibrationHow2]).AppendLine();

        if (missing.Count > 0)
        {
            sheet.AppendLine(words.Format(TextKey.CalibrationMissing, string.Join(", ", missing)));
            sheet.AppendLine();
        }

        sheet.AppendLine(words[TextKey.CalibrationMap]).AppendLine();
        AppendMap(sheet, plate);

        sheet.AppendLine().AppendLine(words[TextKey.CalibrationWitness]).AppendLine();
        sheet.AppendLine(words[TextKey.CalibrationThen]).AppendLine();
        sheet.AppendLine(words[TextKey.CalibrationSaveIt]).AppendLine();

        sheet.AppendLine(PrintNotes.Settings(words));

        if (ProcessPreset.BaseFor(printer) is not null)
        {
            sheet.AppendLine(words.Format(TextKey.PrintNotesImport, "Lego2STL.json"));
        }

        return sheet.ToString();
    }

    /// <summary>Rows from the front of the bed backwards, each row left to right.</summary>
    private static void AppendMap(StringBuilder sheet, PackedPlate plate)
    {
        var rows = plate.Items
            .OrderBy(i => i.Y)
            .ThenBy(i => i.X)
            .GroupBy(i => MathF.Round(i.Y / SameRow))
            .OrderBy(g => g.Key);

        var number = 1;

        foreach (var row in rows)
        {
            sheet.Append(string.Create(CultureInfo.InvariantCulture, $"  {number,2}. "));
            sheet.AppendLine(string.Join(
                "   ",
                row.OrderBy(i => i.X).Select(i => i.Item.PartNumber)));
            number++;
        }
    }
}

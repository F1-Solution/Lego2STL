using System.Globalization;
using System.Text;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;

namespace Lego2STL.Cli.Commands;

/// <summary>
/// Writes the account of what the build stage produced.
/// </summary>
/// <remarks>
/// Worth its own file because it is the answer to "can I trust these?". It names every shape
/// that is not closed, every retired part number whose shape came from a replacement, and
/// every part left off a plate, so that none of those is discovered at the printer.
/// </remarks>
internal static class BuildReport
{
    public static async Task WriteAsync(
        RunLayout layout,
        Strings words,
        IReadOnlyList<PreparedMesh> prepared,
        IReadOnlyList<(string Part, string Reason)> failed,
        PlateBuildResult? plates,
        string geometrySource,
        MeshPipelineOptions options,
        PrintBed bed,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        WriteHeader(sb, words, geometrySource, options, bed);
        WriteShapes(sb, words, prepared);
        WriteNotes(sb, words, prepared, failed);
        WritePlates(sb, words, plates);
        WritePrintingNote(sb, words, options);

        await File.WriteAllTextAsync(
                layout.ReportPath, sb.ToString(), new UTF8Encoding(true), cancellationToken)
            .ConfigureAwait(false);
    }

    private static void WriteHeader(
        StringBuilder sb, Strings words, string geometrySource, MeshPipelineOptions options, PrintBed bed)
    {
        sb.AppendLine(words[TextKey.ReportShapeTitle]);
        sb.AppendLine(new string('-', 78));
        sb.AppendLine($"{words[TextKey.ReportGeometryFrom],-16}: {geometrySource}");
        sb.AppendLine($"{words[TextKey.ReportUnits],-16}: {words[TextKey.Millimetres]}, " +
                      (options.PlaceOnBed
                          ? words[TextKey.ReportStandingOnZero]
                          : words[TextKey.ReportOriginalOrigin]));
        sb.AppendLine(Line(words[TextKey.ReportScale], $"{options.ScalePercent:0.##}%"));
        sb.AppendLine(Line(
            words[TextKey.ReportSeamRepair],
            options.RepairSeams ? words[TextKey.On] : words[TextKey.Off]));
        sb.AppendLine(Line(
            words[TextKey.ReportClearance],
            options.ClearanceMillimetres > 0f
                ? string.Create(CultureInfo.InvariantCulture, $"{options.ClearanceMillimetres:0.###} mm")
                : words[TextKey.None]));
        sb.AppendLine($"{words[TextKey.ReportPlateColumnPlate],-16}: {bed.Name} ({bed})");
        sb.AppendLine();
    }

    private static void WriteShapes(StringBuilder sb, Strings words, IReadOnlyList<PreparedMesh> prepared)
    {
        sb.AppendLine(
            $"{words[TextKey.ReportColumnPart],-10}" +
            $"{words[TextKey.ReportColumnTriangles],8}" +
            $"{words[TextKey.ReportColumnOpen],7}" +
            $"{words[TextKey.ReportColumnSeams],7}" +
            $"{words[TextKey.ReportColumnGaps],7}  " +
            words[TextKey.ReportColumnSize]);

        foreach (var p in prepared.OrderBy(p => p.PartNumber, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine(
                $"{p.PartNumber,-10}{p.Mesh.TriangleCount,8}{p.Quality.OpenEdgeCount,7}" +
                $"{p.SeamsClosed,7}{p.GapsFilled,7}  {p.DescribeSize()}" +
                (p.Title is null ? string.Empty : $"  {p.Title}"));
        }

        sb.AppendLine();

        var closed = prepared.Count(p => p.Quality.IsClosed);
        sb.AppendLine(words.Format(TextKey.ReportShapesClosed, closed, prepared.Count));

        var seamTotal = prepared.Sum(p => p.SeamsClosed);
        var fixedByRepair = prepared.Count(p => !p.QualityBeforeRepair.IsClosed && p.Quality.IsClosed);
        sb.AppendLine(words.Format(TextKey.ReportSeamsClosedSummary, seamTotal, fixedByRepair));

        var gapTotal = prepared.Sum(p => p.GapsFilled);
        if (gapTotal > 0)
        {
            sb.AppendLine(words.Format(
                TextKey.ReportGapsFilledSummary, gapTotal, prepared.Count(p => p.GapsFilled > 0)));
        }

        sb.AppendLine();
    }

    private static void WriteNotes(
        StringBuilder sb,
        Strings words,
        IReadOnlyList<PreparedMesh> prepared,
        IReadOnlyList<(string Part, string Reason)> failed)
    {
        var redirected = prepared.Where(p => p.MovedTo is not null).ToList();
        if (redirected.Count > 0)
        {
            sb.AppendLine(words[TextKey.ReportRetiredNumbers]);
            foreach (var p in redirected)
            {
                sb.AppendLine($"  {p.PartNumber} -> {p.MovedTo}");
            }

            sb.AppendLine();
        }

        var withMissing = prepared.Where(p => p.MissingReferences.Count > 0).ToList();
        if (withMissing.Count > 0)
        {
            sb.AppendLine(words[TextKey.ReportBuiltWithSomethingMissing]);
            foreach (var p in withMissing)
            {
                sb.AppendLine($"  {p.PartNumber}: {string.Join(", ", p.MissingReferences.Take(6))}");
            }

            sb.AppendLine();
        }

        if (failed.Count > 0)
        {
            sb.AppendLine(words[TextKey.ReportProducedNothing]);
            foreach (var (part, reason) in failed)
            {
                sb.AppendLine($"  {part}: {reason}");
            }

            sb.AppendLine();
        }
    }

    private static void WritePlates(StringBuilder sb, Strings words, PlateBuildResult? plates)
    {
        if (plates is null || plates.Plates.Count == 0)
        {
            return;
        }

        sb.AppendLine(words[TextKey.ReportPlateTitle]);
        sb.AppendLine(
            $"{words[TextKey.ReportPlateColumnPlate],-22}" +
            $"{words[TextKey.ReportPlateColumnColour],-24}" +
            $"{words[TextKey.ReportPlateColumnPieces],7}  " +
            words[TextKey.ReportPlateColumnFootprint]);

        foreach (var plate in plates.Plates)
        {
            sb.AppendLine(
                $"{plate.FileName,-22}{plate.ColorName,-24}{plate.PieceCount,7}  {plate.Footprint}");
        }

        sb.AppendLine();
        sb.AppendLine(words.Format(
            TextKey.ReportPlateSummary, plates.Plates.Count, plates.ColorCount, plates.PieceCount));

        if (plates.Skipped.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(words[TextKey.ReportPlateDidNotFit]);
            foreach (var note in plates.Skipped)
            {
                sb.AppendLine("  " + note);
            }
        }

        sb.AppendLine();
    }

    private static void WritePrintingNote(StringBuilder sb, Strings words, MeshPipelineOptions options)
    {
        sb.AppendLine(words[TextKey.ReportPrintingNoteTitle]);

        var body = options.ClearanceMillimetres > 0f
            ? words.Format(
                TextKey.ReportPrintingNoteWithClearance,
                options.ClearanceMillimetres.ToString("0.###", CultureInfo.InvariantCulture))
            : words[TextKey.ReportPrintingNoteBody];

        foreach (var line in Wrap(body, 76))
        {
            sb.AppendLine("  " + line);
        }
    }

    private static string Line(string label, string value) => $"{label,-16}: {value}";

    /// <summary>Breaks a paragraph into lines that fit, so the report reads in any terminal.</summary>
    private static IEnumerable<string> Wrap(string text, int width)
    {
        var line = new StringBuilder();

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                yield return line.ToString();
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            yield return line.ToString();
        }
    }
}

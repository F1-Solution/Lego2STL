using System.Globalization;
using System.Text;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;

namespace Lego2STL.Core.Pipeline;

/// <summary>
/// Writes the account of what a run produced.
/// </summary>
/// <remarks>
/// This is the answer to "can I trust these?". It names every shape that is not closed, every
/// retired part number whose shape came from a replacement, every part left off a plate and
/// every entry that could not be read, so that none of those is discovered at the printer.
/// </remarks>
public static class RunReport
{
    public static async Task WriteAsync(
        RunLayout layout,
        RunOutcome outcome,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(outcome);

        await File.WriteAllTextAsync(
                layout.ReportPath, Compose(outcome), new UTF8Encoding(true), cancellationToken)
            .ConfigureAwait(false);
    }

    public static string Compose(RunOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var words = Strings.For(outcome.Settings.Language);
        var sb = new StringBuilder();

        Header(sb, words, outcome);
        Totals(sb, words, outcome);
        Unread(sb, words, outcome);
        Shapes(sb, words, outcome);
        NotPrinted(sb, words, outcome);
        Notes(sb, words, outcome);
        Plates(sb, words, outcome);
        PrintingNote(sb, words, outcome.Settings);

        return sb.ToString();
    }

    private static void Header(StringBuilder sb, Strings words, RunOutcome outcome)
    {
        var s = outcome.Settings;

        sb.AppendLine(words[TextKey.ReportRunTitle]);
        sb.AppendLine(new string('-', 78));

        sb.AppendLine(Line(words[TextKey.ReportInput], s.Kind switch
        {
            InputKind.SetNumber => s.SetNumber ?? string.Empty,
            _ => s.InputPath ?? string.Empty,
        }));

        if (s.Kind == InputKind.Document)
        {
            sb.AppendLine(Line(words[TextKey.ReportPages], s.Pages ?? words[TextKey.None]));
            sb.AppendLine(Line(words[TextKey.ReportColourNumbering], s.ColorScheme.ToString()));
        }

        if (outcome.Shapes.Count > 0)
        {
            sb.AppendLine(Line(words[TextKey.ReportGeometryFrom], outcome.GeometrySource));
            sb.AppendLine(Line(
                words[TextKey.ReportUnits],
                words[TextKey.Millimetres] + ", " + (s.KeepOrigin
                    ? words[TextKey.ReportOriginalOrigin]
                    : words[TextKey.ReportStandingOnZero])));
            sb.AppendLine(Line(words[TextKey.ReportScale], $"{s.ScalePercent:0.##}%"));
            sb.AppendLine(Line(
                words[TextKey.ReportSeamRepair],
                s.NoSeamRepair ? words[TextKey.Off] : words[TextKey.On]));
            sb.AppendLine(Line(
                words[TextKey.ReportClearance],
                s.Clearance > 0
                    ? string.Create(CultureInfo.InvariantCulture, $"{s.Clearance:0.###} mm")
                    : words[TextKey.None]));
        }

        if (outcome.Plates is not null)
        {
            sb.AppendLine(Line(words[TextKey.ReportPrinter], $"{s.Bed.Name} ({s.Bed})"));
        }

        sb.AppendLine();
    }

    private static void Totals(StringBuilder sb, Strings words, RunOutcome outcome)
    {
        if (outcome.PartsList is not { } list)
        {
            return;
        }

        sb.AppendLine(words[TextKey.ReportTotals]);
        sb.AppendLine("  " + words.Format(TextKey.ReportTotalEntries, list.Entries.Count));
        sb.AppendLine("  " + words.Format(TextKey.ReportTotalPieces, list.TotalPieces));
        sb.AppendLine("  " + words.Format(
            TextKey.ReportTotalDistinctParts, list.DistinctPartNumbers.Count));
        sb.AppendLine();

        if (outcome.Notes.Count > 0)
        {
            sb.AppendLine(words[TextKey.ReportPartsList]);
            foreach (var note in outcome.Notes)
            {
                sb.AppendLine("  " + note);
            }

            sb.AppendLine();
        }
    }

    private static void Unread(StringBuilder sb, Strings words, RunOutcome outcome)
    {
        if (outcome.Unread.Count == 0)
        {
            return;
        }

        sb.AppendLine(words[TextKey.ReportCouldNotBeRead]);
        foreach (var entry in outcome.Unread)
        {
            sb.AppendLine($"  {entry.Page} @ {entry.Bounds}: {entry.Reason}");
            sb.AppendLine("    " + words.Format(
                TextKey.ReportRecogniserReturned,
                entry.RawText.Replace('\n', '/').Replace("\r", string.Empty)));
        }

        sb.AppendLine();
    }

    private static void Shapes(StringBuilder sb, Strings words, RunOutcome outcome)
    {
        if (outcome.Shapes.Count == 0)
        {
            return;
        }

        sb.AppendLine(
            $"{words[TextKey.ReportColumnPart],-10}" +
            $"{words[TextKey.ReportColumnTriangles],8}" +
            $"{words[TextKey.ReportColumnOpen],7}" +
            $"{words[TextKey.ReportColumnSeams],7}" +
            $"{words[TextKey.ReportColumnGaps],7}  " +
            words[TextKey.ReportColumnSize]);

        foreach (var p in outcome.Shapes.OrderBy(p => p.PartNumber, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine(
                $"{p.PartNumber,-10}{p.Mesh.TriangleCount,8}{p.Quality.OpenEdgeCount,7}" +
                $"{p.SeamsClosed,7}{p.GapsFilled,7}  {p.DescribeSize()}" +
                (p.Title is null ? string.Empty : $"  {p.Title}"));
        }

        sb.AppendLine();
        sb.AppendLine(words.Format(
            TextKey.ReportShapesClosed, outcome.ClosedShapeCount, outcome.Shapes.Count));

        // A shape only counts as completed by seam repair when no gaps had to be covered on
        // it afterwards. Covering runs only on what is still open once the seams are closed,
        // so a shape with gaps covered was, by definition, not finished by the seams alone.
        sb.AppendLine(words.Format(
            TextKey.ReportSeamsClosedSummary,
            outcome.Shapes.Sum(p => p.SeamsClosed),
            outcome.Shapes.Count(p =>
                !p.QualityBeforeRepair.IsClosed && p.Quality.IsClosed && p.GapsFilled == 0)));

        var gaps = outcome.Shapes.Sum(p => p.GapsFilled);
        if (gaps > 0)
        {
            sb.AppendLine(words.Format(
                TextKey.ReportGapsFilledSummary, gaps, outcome.Shapes.Count(p => p.GapsFilled > 0)));
        }

        if (outcome.Settings.Clearance > 0)
        {
            sb.AppendLine(words.Format(
                TextKey.MsgClearanceApplied,
                outcome.ClearedShapeCount,
                outcome.Shapes.Count,
                outcome.Settings.Clearance.ToString("0.###", CultureInfo.InvariantCulture)));

            if (outcome.Settings.ClearanceFrom is { Length: > 0 } preset)
            {
                sb.AppendLine(words.Format(
                    TextKey.MsgClearanceFromPreset, outcome.Settings.Clearance, preset));
            }

            foreach (var refused in outcome.Shapes.Where(p => p.ClearanceRefusedBecause is not null))
            {
                sb.AppendLine($"  {refused.PartNumber}: {Describe(words, refused)}");
            }
        }

        sb.AppendLine();
    }

    private static string Describe(Strings words, PreparedMesh shape) =>
        shape.ClearanceRefusedBecause == "open"
            ? words.Format(TextKey.ErrClearanceNeedsClosedShape, shape.PartNumber, shape.Quality.OpenEdgeCount)
            : words.Format(
                TextKey.ErrPartTooSmallForClearance,
                shape.PartNumber,
                ClearanceOffset.ThinnestSpan(shape.Mesh).ToString("0.##", CultureInfo.InvariantCulture),
                "the amount asked for");

    private static void Notes(StringBuilder sb, Strings words, RunOutcome outcome)
    {
        var redirected = outcome.Shapes.Where(p => p.MovedTo is not null).ToList();
        if (redirected.Count > 0)
        {
            sb.AppendLine(words[TextKey.ReportRetiredNumbers]);
            foreach (var p in redirected)
            {
                sb.AppendLine($"  {p.PartNumber} -> {p.MovedTo}");
            }

            sb.AppendLine();
        }

        var withMissing = outcome.Shapes.Where(p => p.MissingReferences.Count > 0).ToList();
        if (withMissing.Count > 0)
        {
            sb.AppendLine(words[TextKey.ReportBuiltWithSomethingMissing]);
            foreach (var p in withMissing)
            {
                sb.AppendLine($"  {p.PartNumber}: {string.Join(", ", p.MissingReferences.Take(6))}");
            }

            sb.AppendLine();
        }

        if (outcome.Failed.Count > 0)
        {
            sb.AppendLine(words[TextKey.ReportProducedNothing]);
            foreach (var failure in outcome.Failed)
            {
                sb.AppendLine($"  {failure.PartNumber}: {failure.Reason}");
            }

            sb.AppendLine();
        }
    }

    /// <summary>The parts left unbuilt on purpose, each with what ruled it out.</summary>
    private static void NotPrinted(StringBuilder sb, Strings words, RunOutcome outcome)
    {
        if (outcome.NotPrinted.Count == 0)
        {
            return;
        }

        sb.AppendLine(words[TextKey.ReportNotPrintedTitle]);

        foreach (var part in outcome.NotPrinted)
        {
            var fact = outcome.PartFacts.GetValueOrDefault(part);

            sb.AppendLine("  " + (Printability.Of(fact) is Printable.NotItsMaterial
                ? words.Format(TextKey.MsgNotPrintedMaterial, part, fact!.Material)
                : words.Format(TextKey.MsgNotPrintedKind, part)));
        }

        sb.AppendLine();
    }

    private static void Plates(StringBuilder sb, Strings words, RunOutcome outcome)
    {
        if (outcome.Plates is not { Plates.Count: > 0 } plates)
        {
            return;
        }

        // Measured rather than fixed: a colour's name and the file named after it are as long
        // as the language makes them, and a column too narrow for either runs the table
        // together in exactly the place someone is comparing plates.
        var fileColumn = Widest(words[TextKey.ReportPlateColumnPlate], plates.Plates.Select(p => p.FileName));
        var colourColumn = Widest(words[TextKey.ReportPlateColumnColour], plates.Plates.Select(p => p.ColorName));

        sb.AppendLine(words[TextKey.ReportPlateTitle]);
        sb.AppendLine(
            words[TextKey.ReportPlateColumnPlate].PadRight(fileColumn) +
            words[TextKey.ReportPlateColumnColour].PadRight(colourColumn) +
            $"{words[TextKey.ReportPlateColumnPieces],7}  " +
            words[TextKey.ReportPlateColumnFootprint]);

        foreach (var plate in plates.Plates)
        {
            sb.AppendLine(
                plate.FileName.PadRight(fileColumn) +
                plate.ColorName.PadRight(colourColumn) +
                $"{plate.PieceCount,7}  {plate.Footprint}");
        }

        sb.AppendLine();
        sb.AppendLine(words.Format(
            TextKey.ReportPlateSummary, plates.Plates.Count, plates.ColorCount, plates.PieceCount));

        // Said here as well as in the shapes section above, because a plate table on its own
        // reads as the whole set and this is the page someone prints from.
        if (outcome.Failed.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(words.Format(TextKey.ReportPlateMissingParts, outcome.Failed.Count));
        }

        if (plates.Skipped.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(words[TextKey.ReportPlateDidNotFit]);

            foreach (var part in plates.Skipped)
            {
                sb.AppendLine("  " + PlateBuilder.Describe(part, words, outcome.Settings.Bed));
            }
        }

        sb.AppendLine();
    }

    private static void PrintingNote(StringBuilder sb, Strings words, RunSettings settings)
    {
        if (!settings.WantsShapes)
        {
            return;
        }

        sb.AppendLine(words[TextKey.ReportPrintingNoteTitle]);

        var body = settings.Clearance > 0
            ? words.Format(
                TextKey.ReportPrintingNoteWithClearance,
                settings.Clearance.ToString("0.###", CultureInfo.InvariantCulture))
            : words[TextKey.ReportPrintingNoteBody];

        foreach (var line in Wrap(body, 76))
        {
            sb.AppendLine("  " + line);
        }
    }

    /// <summary>
    /// A labelled line. The label column is wide enough for the longest wording in any
    /// language, so the colons still line up when the words are not English.
    /// </summary>
    private const int LabelWidth = 26;

    private static string Line(string label, string value) => $"{label.PadRight(LabelWidth)}: {value}";

    /// <summary>A column wide enough for its heading and everything under it, plus a gap.</summary>
    private static int Widest(string heading, IEnumerable<string> values) =>
        Math.Max(heading.Length, values.Max(v => v.Length)) + 2;

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

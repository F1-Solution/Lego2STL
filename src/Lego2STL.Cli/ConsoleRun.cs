using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;

namespace Lego2STL.Cli;

/// <summary>
/// Drives a run from the terminal: shows what is happening, then says what came of it.
/// </summary>
/// <remarks>
/// All the deciding happens in the pipeline; this only turns what it says into lines on a
/// screen and an exit code. Keeping that split is what lets the window use the identical run
/// without inheriting anything about consoles.
/// </remarks>
internal static class ConsoleRun
{
    public static async Task<int> ExecuteAsync(RunSettings settings, CancellationToken cancellationToken)
    {
        var words = Strings.For(settings.Language);

        using var log = RunLogFile.Open(settings.LogFile);

        // Before the run, not after: a run that is killed still has a row in the history.
        if (RunLayout.Plan(settings) is { } planned)
        {
            RunIndex.Record(planned);
        }

        void Say(string message)
        {
            log?.WriteLine(message);

            if (!settings.Quiet)
            {
                Console.WriteLine(message);
            }
        }

        var runner = new PipelineRunner(Say);
        var outcome = await runner.RunAsync(settings, cancellationToken).ConfigureAwait(false);

        log?.Flush();

        return Summarise(outcome, words);
    }

    private static int Summarise(RunOutcome outcome, Strings words)
    {
        if (outcome.Result == RunResult.Failed)
        {
            Console.Error.WriteLine($"{words[TextKey.MsgError]}: {outcome.Error}");
            return Program.ExitFailure;
        }

        Console.WriteLine();

        if (outcome.PartsList is { } list)
        {
            Console.WriteLine(words.Format(
                TextKey.MsgEntriesSummary,
                list.Entries.Count,
                list.TotalPieces,
                list.DistinctPartNumbers.Count));
        }

        if (outcome.Shapes.Count > 0)
        {
            Console.WriteLine("  " + words.Format(
                TextKey.MsgClosedAndOpen,
                outcome.ClosedShapeCount,
                outcome.Shapes.Count - outcome.ClosedShapeCount));
        }

        ReportClearance(outcome, words);

        if (outcome.Plates is { } plates)
        {
            Console.WriteLine("  " + words.Format(
                TextKey.ReportPlateSummary,
                plates.Plates.Count,
                plates.ColorCount,
                plates.PieceCount));
        }

        if (outcome.Layout is { } layout)
        {
            Console.WriteLine(words.Format(TextKey.MsgReportWritten, layout.ReportPath));
        }

        if (outcome.Unread.Count > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(words.Format(
                outcome.Unread.Count == 1
                    ? TextKey.MsgCouldNotReadEntriesOne
                    : TextKey.MsgCouldNotReadEntriesMany,
                outcome.Unread.Count));

            foreach (var entry in outcome.Unread)
            {
                Console.Error.WriteLine("  " + words.Format(
                    TextKey.MsgUnreadEntryAt, entry.Page, entry.Bounds, entry.Reason));
            }

            Console.Error.WriteLine(words[TextKey.MsgWrittenWithoutThem]);
        }

        if (outcome.Failed.Count > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(words.Format(TextKey.MsgProducedNothing, outcome.Failed.Count));
            foreach (var failure in outcome.Failed)
            {
                Console.Error.WriteLine($"  {failure.PartNumber}: {failure.Reason}");
            }
        }

        return outcome.Result == RunResult.Unverified ? Program.ExitUnverified : Program.ExitOk;
    }

    /// <summary>
    /// One line for the clearance, not one paragraph per part. Which parts were refused, and
    /// why, is in the report; the console says how many and what to do about it.
    /// </summary>
    private static void ReportClearance(RunOutcome outcome, Strings words)
    {
        if (outcome.Settings.Clearance <= 0 || outcome.Shapes.Count == 0)
        {
            return;
        }

        Console.WriteLine("  " + words.Format(
            TextKey.MsgClearanceApplied,
            outcome.ClearedShapeCount,
            outcome.Shapes.Count,
            outcome.Settings.Clearance));

        var open = outcome.Shapes.Count(s => s.ClearanceRefusedBecause == "open");
        var thin = outcome.Shapes.Count(s => s.ClearanceRefusedBecause == "thin");

        if (open > 0)
        {
            Console.WriteLine("  " + words.Format(
                outcome.Settings.FillGaps
                    ? TextKey.MsgClearanceRefusedStillOpen
                    : TextKey.MsgClearanceRefusedOpen,
                open));
        }

        if (thin > 0)
        {
            Console.WriteLine("  " + words.Format(TextKey.MsgClearanceRefusedThin, thin));
        }
    }
}

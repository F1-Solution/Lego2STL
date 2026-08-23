namespace Lego2STL.Core.Pipeline;

/// <summary>The steps a run goes through, in order.</summary>
/// <remarks>
/// Named rather than counted so that a progress bar can say what is happening as well as how
/// far along it is. "Reading page 3 of 4" is worth far more than a bar at 40 percent, because
/// the steps here take very different lengths of time: fetching a shape library can take
/// minutes on a first run and nothing at all afterwards.
/// </remarks>
public enum RunStage
{
    Starting,
    ReadingDocument,
    LookingUpSet,
    ReadingPartsList,
    WritingPartsList,
    GatheringShapes,
    BuildingShapes,
    ArrangingPlates,
    WritingReport,
    Finished,
}

/// <summary>How far along a run is, and what it is doing.</summary>
/// <param name="Stage">Which step.</param>
/// <param name="Completed">How many items of this step are done.</param>
/// <param name="Total">How many there are, or zero when it is not yet known.</param>
/// <param name="Detail">What is being worked on right now, for showing beside the bar.</param>
public sealed record RunProgress(RunStage Stage, int Completed = 0, int Total = 0, string? Detail = null)
{
    /// <summary>
    /// How far through the whole run, from 0 to 1.
    /// </summary>
    /// <remarks>
    /// Each step gets a share of the bar, and progress within a step fills its share. The
    /// shares are weighted by how long the steps actually take on the reference document
    /// rather than being equal, so the bar moves at something like a steady rate instead of
    /// sitting at a third for most of the run.
    /// </remarks>
    public double Fraction
    {
        get
        {
            var (start, span) = Share(Stage);
            var within = Total > 0 ? Math.Clamp((double)Completed / Total, 0, 1) : 0;
            return Math.Clamp(start + (span * within), 0, 1);
        }
    }

    private static (double Start, double Span) Share(RunStage stage) => stage switch
    {
        RunStage.Starting => (0.00, 0.00),
        RunStage.ReadingDocument => (0.00, 0.35),
        RunStage.LookingUpSet => (0.00, 0.35),
        RunStage.ReadingPartsList => (0.00, 0.05),
        RunStage.WritingPartsList => (0.35, 0.03),
        RunStage.GatheringShapes => (0.38, 0.12),
        RunStage.BuildingShapes => (0.50, 0.38),
        RunStage.ArrangingPlates => (0.88, 0.08),
        RunStage.WritingReport => (0.96, 0.03),
        RunStage.Finished => (1.00, 0.00),
        _ => (0.00, 0.00),
    };
}

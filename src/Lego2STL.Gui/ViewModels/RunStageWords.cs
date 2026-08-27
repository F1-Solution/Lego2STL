using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Text;
using Lego2STL.Gui.Localization;

namespace Lego2STL.Gui.ViewModels;

/// <summary>
/// The steps of a run, named for a screen.
/// </summary>
/// <remarks>
/// One naming, shared by a run's own page and by its row in the history, so "stopped while
/// building the shapes" reads identically wherever it is said. Two copies would drift the first
/// time a step was reworded in one of them.
/// </remarks>
internal static class RunStageWords
{
    public static string For(RunStage stage) => Loc.Current.Text(stage switch
    {
        RunStage.ReadingDocument => TextKey.UiStageReadingDocument,
        RunStage.LookingUpSet => TextKey.UiStageLookingUpSet,
        RunStage.ReadingPartsList => TextKey.UiStageReadingPartsList,
        RunStage.WritingPartsList => TextKey.UiStageWritingPartsList,
        RunStage.GatheringShapes => TextKey.UiStageGatheringShapes,
        RunStage.BuildingShapes => TextKey.UiStageBuildingShapes,
        RunStage.ArrangingPlates => TextKey.UiStageArrangingPlates,
        RunStage.WritingReport => TextKey.UiStageWritingReport,
        RunStage.Finished => TextKey.UiDone,
        _ => TextKey.UiIdle,
    });
}

using Lego2STL.Core.Pipeline;

namespace Lego2STL.Core.Run;

/// <summary>
/// Decides where a run's folder goes.
/// </summary>
/// <remarks>
/// A desktop run puts it beside the input, which is what every command line has always done.
/// A sandboxed application cannot: the document arrives from a picker and there is nowhere
/// beside it to write. The decision therefore belongs to whoever is running the pipeline,
/// the same way the recogniser does.
/// </remarks>
public interface IRunHome
{
    /// <summary>The folder this run will use, or null when the settings do not yet name one.</summary>
    RunLayout? Plan(RunSettings settings);
}

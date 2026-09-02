using Lego2STL.Core.Pipeline;

namespace Lego2STL.Core.Run;

/// <summary>The original behaviour: one folder beside the input file.</summary>
public sealed class BesideTheInputRunHome : IRunHome
{
    public RunLayout? Plan(RunSettings settings) => RunLayout.Plan(settings);
}

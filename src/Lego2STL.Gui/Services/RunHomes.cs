using Lego2STL.Core.Run;

namespace Lego2STL.Gui.Services;

/// <summary>Which home this application's runs use: the desktop's, unless a head says otherwise.</summary>
public static class RunHomes
{
    public static IRunHome Current { get; set; } = new BesideTheInputRunHome();
}

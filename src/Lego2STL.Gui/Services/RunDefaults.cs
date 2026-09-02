namespace Lego2STL.Gui.Services;

/// <summary>Defaults a head can set before the window starts, read by the run options it shows.</summary>
public static class RunDefaults
{
    /// <summary>Whether a run may download the whole library; false on a phone.</summary>
    public static bool AllowFullArchive { get; set; } = true;
}

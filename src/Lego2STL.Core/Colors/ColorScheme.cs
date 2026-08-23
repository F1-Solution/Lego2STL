namespace Lego2STL.Core.Colors;

/// <summary>
/// Which catalogue's numbering a colour code belongs to.
/// The same colour has different numbers in each: black is BrickLink 11,
/// Rebrickable 0, LDraw 0 and LEGO 26.
/// </summary>
public enum ColorScheme
{
    /// <summary>BrickLink numbering. What LEGO instruction PDFs normally print.</summary>
    BrickLink,

    /// <summary>Rebrickable's own ids, as used by its API and CSV dumps.</summary>
    Rebrickable,

    /// <summary>LEGO's official internal colour ids.</summary>
    Lego,

    /// <summary>LDraw colour codes, as used in .dat files and LDConfig.ldr.</summary>
    LDraw,
}

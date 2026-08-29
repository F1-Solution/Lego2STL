using System.Globalization;

namespace Lego2STL.Core.Catalogue;

/// <summary>
/// What can be told about an element number from its digits alone.
/// </summary>
/// <remarks>
/// <para>
/// LEGO's older element numbers are a design number with a two-digit colour appended: 370726
/// is design 3707 in colour 26, black. Numbers issued since are opaque - 6177114 says nothing
/// about either - so this reads only the old shape, and only when nothing better is available.
/// </para>
/// <para>
/// It is worth having because it is exact about the colour and usually right about the
/// design, and it needs neither a downloaded table nor a network. It is not worth preferring,
/// because "usually" is real: 306926 is design 3069<em>b</em>, not 3069, and 614321 is
/// catalogued as 3941 rather than the 6143 its digits give. Both those come back correct from
/// a table or the API, which is why this sits last.
/// </para>
/// </remarks>
public static class ElementNumber
{
    /// <summary>The shortest and longest design number this can be confident about.</summary>
    private const int ClassicLength = 6;

    /// <summary>
    /// Splits a classic element number into its design number and LEGO colour code.
    /// </summary>
    /// <returns>False for anything that is not six digits, which includes every modern number.</returns>
    public static bool TrySplitClassic(string? elementId, out string designId, out int legoColorCode)
    {
        designId = "";
        legoColorCode = 0;

        if (elementId is not { Length: ClassicLength } || !elementId.All(char.IsAsciiDigit))
        {
            return false;
        }

        designId = elementId[..4];

        return int.TryParse(
            elementId[4..],
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out legoColorCode);
    }
}

using System.Globalization;

namespace Lego2STL.Core.Plates;

/// <summary>
/// What a plate's file is called.
/// </summary>
/// <remarks>
/// Written once and read from both sides. The rule used to live only where plates are written,
/// and the catalogue kept a second, slightly different copy in order to find them again: it
/// lower-cased and swapped spaces for hyphens, which agrees for "Light Bluish Gray" and not
/// for a colour with a comma or a slash in it. Worse, it looked the colour up by its stored
/// English name while the file had been named in the run's own language, so an Italian run
/// matched nothing at all.
/// </remarks>
public static class PlateFileName
{
    /// <param name="colorName">The colour as the run words it, which is what the file is named after.</param>
    /// <param name="number">Which plate of this colour, counting from one.</param>
    /// <param name="total">How many plates this colour needed; a single plate goes unnumbered.</param>
    public static string For(string colorName, int number, int total)
    {
        var slug = Slug(colorName);

        return total == 1
            ? $"{slug}.3mf"
            : string.Create(CultureInfo.InvariantCulture, $"{slug}-{number}.3mf");
    }

    /// <summary>A colour's name as a file name: lower case, words joined by hyphens.</summary>
    public static string Slug(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var slug = new string([.. name
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')]);

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        slug = slug.Trim('-');
        return slug.Length == 0 ? "colour" : slug;
    }

    /// <summary>
    /// Whether a file on the disk is a plate of this colour, however many there turned out to be.
    /// </summary>
    /// <remarks>
    /// One plate is "nero.3mf" and several are "nero-1.3mf", so the test is the slug followed
    /// by either nothing or a number - not merely "starts with", which would let "nero" claim
    /// "nero-perlato-1.3mf".
    /// </remarks>
    public static bool IsPlateOf(string fileName, string colorName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var slug = Slug(colorName);

        if (!stem.StartsWith(slug, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = stem[slug.Length..];

        return rest.Length == 0
               || (rest[0] == '-' && rest.Length > 1 && rest[1..].All(char.IsAsciiDigit));
    }
}

using System.Globalization;

namespace Lego2STL.Core.Text;

/// <summary>Which language the tool speaks.</summary>
public enum DisplayLanguage
{
    English,
    Italian,
}

/// <summary>
/// Works out which language to use, and names them for the command line and the interface.
/// </summary>
/// <remarks>
/// The machine's own language is the default, because someone running an Italian Windows
/// almost certainly wants Italian and should not have to ask for it. Anything not covered
/// falls back to English rather than guessing, and an explicit choice always wins, which is
/// what keeps output reproducible when it matters.
/// </remarks>
public static class DisplayLanguages
{
    public const DisplayLanguage Fallback = DisplayLanguage.English;

    /// <summary>The two-letter tag, as accepted on the command line.</summary>
    public static string Tag(this DisplayLanguage language) => language switch
    {
        DisplayLanguage.English => "en",
        DisplayLanguage.Italian => "it",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown language."),
    };

    /// <summary>The language's own name for itself, for a menu that offers the choice.</summary>
    public static string NativeName(this DisplayLanguage language) => language switch
    {
        DisplayLanguage.English => "English",
        DisplayLanguage.Italian => "Italiano",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown language."),
    };

    public static IReadOnlyList<DisplayLanguage> All { get; } = Enum.GetValues<DisplayLanguage>();

    /// <summary>The language the machine is set to, or English when it is not one we speak.</summary>
    public static DisplayLanguage FromEnvironment() => FromCulture(CultureInfo.CurrentUICulture);

    /// <summary>The language matching a culture, by its two-letter code.</summary>
    public static DisplayLanguage FromCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return TryParse(culture.TwoLetterISOLanguageName, out var language) ? language : Fallback;
    }

    /// <summary>Reads a tag such as "it", "IT" or "it-CH".</summary>
    public static bool TryParse(string? tag, out DisplayLanguage language)
    {
        language = Fallback;

        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var head = tag.Trim().Split('-', '_')[0];

        foreach (var candidate in All)
        {
            if (string.Equals(head, candidate.Tag(), StringComparison.OrdinalIgnoreCase))
            {
                language = candidate;
                return true;
            }
        }

        return false;
    }
}

using System.Globalization;

namespace Lego2STL.Core.Text;

/// <summary>
/// The tool's words, in one language.
/// </summary>
/// <remarks>
/// <para>
/// A table per language, looked up by <see cref="TextKey"/>. Keeping the key an enum is what
/// makes an untranslated phrase impossible to ship: the suite walks every value and checks
/// that each language has it, and that the two agree on how many placeholders it takes, so a
/// translation cannot quietly drop a number out of a sentence.
/// </para>
/// <para>
/// Formatting uses the invariant culture on purpose. The numbers here are counts, millimetres
/// and part numbers, and a decimal comma in a file name or a measurement causes more trouble
/// than it solves; the language chooses the words, not the arithmetic.
/// </para>
/// </remarks>
public sealed partial class Strings
{
    private readonly IReadOnlyDictionary<TextKey, string> _table;

    private Strings(DisplayLanguage language, IReadOnlyDictionary<TextKey, string> table)
    {
        Language = language;
        _table = table;
    }

    public DisplayLanguage Language { get; }

    /// <remarks>
    /// Each table sits in a class of its own, in the other halves of this file, and is reached
    /// through a property. That is deliberate: the order in which one partial file's static
    /// fields are set up relative to another's is not defined, so reading a sibling field
    /// directly from an initialiser here would find it empty on some builds and full on
    /// others. A type of its own is set up the first time it is touched, whenever that is.
    /// </remarks>
    public static Strings English { get; } = new(DisplayLanguage.English, EnglishTable);

    public static Strings Italian { get; } = new(DisplayLanguage.Italian, ItalianTable);

    public static Strings For(DisplayLanguage language) => language switch
    {
        DisplayLanguage.English => English,
        DisplayLanguage.Italian => Italian,
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown language."),
    };

    /// <summary>The words for the machine's own language.</summary>
    public static Strings ForEnvironment() => For(DisplayLanguages.FromEnvironment());

    public string this[TextKey key] =>
        _table.TryGetValue(key, out var text)
            ? text
            : throw new KeyNotFoundException(
                $"No {Language} wording for {key}. Every key needs one in every language.");

    /// <summary>The phrase with its placeholders filled in.</summary>
    public string Format(TextKey key, params object?[] arguments) =>
        string.Format(CultureInfo.InvariantCulture, this[key], arguments);

    /// <summary>Every key this language defines. Used by the completeness test.</summary>
    public IReadOnlyCollection<TextKey> Keys => (IReadOnlyCollection<TextKey>)_table.Keys;
}

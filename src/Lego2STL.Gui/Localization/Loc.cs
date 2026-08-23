using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Lego2STL.Core.Text;

namespace Lego2STL.Gui.Localization;

/// <summary>
/// The window's words, switchable while it is open.
/// </summary>
/// <remarks>
/// <para>
/// Every label binds through the indexer here rather than holding its own copy of a phrase.
/// Changing the language raises a change for the indexer, which is a signal every binding
/// through it listens to, so the whole window re-reads itself at once. Without that, changing
/// the language would only take effect the next time the application started, which is not
/// what anyone means by a language setting.
/// </para>
/// <para>
/// A key that does not name a phrase comes back as itself rather than as an empty label. A
/// mistyped key then shows up on screen as its own name, which is obvious the first time the
/// screen is looked at; blank space is not.
/// </para>
/// </remarks>
public sealed partial class Loc : ObservableObject
{
    /// <summary>The one instance the whole window binds to.</summary>
    public static Loc Current { get; } = new();

    private Strings _words = Strings.ForEnvironment();

    private Loc() => Language = _words.Language;

    /// <summary>
    /// The name a binding through an indexer listens on. Raising a change for it is what makes
    /// every label in the window re-read itself at once.
    /// </summary>
    private const string IndexerName = "Item[]";

    public DisplayLanguage Language { get; private set; }

    /// <summary>The languages on offer, for a menu that lets one be picked.</summary>
    public static IReadOnlyList<LanguageChoice> Choices { get; } =
        [.. DisplayLanguages.All.Select(l => new LanguageChoice(l))];

    public string this[string key] =>
        Enum.TryParse<TextKey>(key, ignoreCase: false, out var parsed) ? _words[parsed] : key;

    public string Text(TextKey key) => _words[key];

    public string Format(TextKey key, params object?[] arguments) => _words.Format(key, arguments);

    /// <summary>The current words, for code that formats a whole sentence at once.</summary>
    public Strings Words => _words;

    public void Use(DisplayLanguage language)
    {
        if (language == Language)
        {
            return;
        }

        Language = language;
        _words = Strings.For(language);

        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(Words));

        // Two signals, because a binding through an indexer does not always listen on the
        // indexer's own name. The empty name means "everything on this object has changed",
        // which every binding honours, and is what actually re-reads the whole window.
        OnPropertyChanged(IndexerName);
        OnPropertyChanged(string.Empty);
    }
}

/// <summary>A language as it appears in the menu: in its own words.</summary>
public sealed record LanguageChoice(DisplayLanguage Language)
{
    public string Name => Language.NativeName();

    public override string ToString() => Name;
}
